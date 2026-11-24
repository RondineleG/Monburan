"""Orquestrador do ciclo de validação E2E com triagem Jev (seções 12-15).

Uso: python gate/run_cycle.py [--cycle N] [--skip-e2e]
Exit codes: 0 aprovado | 2 qualidade abaixo | 3 relatório/infra inválido | 4 condição sensível/humana.
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import time
import uuid
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

import jev_triage
from models import CheckResult, Cause, E2eReport, FlowStatus
from trx_report import parse_trx

REPO = Path(__file__).resolve().parent.parent
CFG = json.loads((REPO / "gate" / "config.json").read_text(encoding="utf-8"))
REPORTS = REPO / "reports"
STATE = REPORTS / "gate-state.json"

MAX_ATTEMPTS = int(os.environ.get("GATE_MAX_ATTEMPTS", "5"))
MIN_RATE = float(CFG.get("min_rate", 0.9))
MAX_DISTINCT_CAUSES = 3


def run(cmd: list[str], name: str, timeout: int = 1800) -> CheckResult:
    print(f"[gate] {name}: {' '.join(cmd)}", flush=True)
    # No Windows, o lookup do executavel usa o PATH do processo pai:
    # precisamos mutar os.environ, nao so o env do filho.
    os.environ["PATH"] = CFG.get("dotnet_dir", "") + os.pathsep + os.environ["PATH"]
    env = dict(os.environ)
    env["DOTNET_ROOT"] = CFG.get("dotnet_root", env.get("DOTNET_ROOT", ""))
    cmd = list(cmd)
    if cmd[0] == "dotnet":
        cmd[0] = os.path.join(CFG.get("dotnet_dir", ""), "dotnet.exe")
    try:
        proc = subprocess.run(
            cmd, cwd=REPO, env=env, capture_output=True, text=True,
            timeout=timeout, encoding="utf-8", errors="replace")
        tail = (proc.stdout + proc.stderr)[-2000:]
        ok = proc.returncode == 0
        print(f"[gate] {name}: {'OK' if ok else 'FALHOU'} ({proc.returncode})")
        if not ok:
            print(tail[-1200:])
        (REPORTS / f"{name}.log").write_text(proc.stdout + proc.stderr, encoding="utf-8")
        return CheckResult(name=name, ok=ok, detail=tail[-500:])
    except subprocess.TimeoutExpired:
        return CheckResult(name=name, ok=False, detail=f"timeout {timeout}s")


def git(*args: str) -> str:
    return subprocess.run(
        ["git", *args], cwd=REPO, capture_output=True, text=True).stdout.strip()


def load_state() -> dict:
    if STATE.exists():
        return json.loads(STATE.read_text(encoding="utf-8"))
    return {"attempts": 0, "flow_failures": {}}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--skip-e2e", action="store_true")
    args = ap.parse_args()
    REPORTS.mkdir(exist_ok=True)
    state = load_state()
    state["attempts"] += 1
    attempt = state["attempts"]
    if attempt > MAX_ATTEMPTS:
        print(f"[gate] MAX_ATTEMPTS ({MAX_ATTEMPTS}) atingido — decisão humana.")
        return 4

    report = E2eReport(
        project=CFG["project"], branch=git("rev-parse", "--abbrev-ref", "HEAD"),
        commit=git("rev-parse", "--short", "HEAD"),
        run_id=f"cycle-{attempt}-{uuid.uuid4().hex[:8]}")

    # 1) etapas determinísticas
    for step in CFG["steps"]:
        check = run(step["cmd"], step["name"], step.get("timeout", 1800))
        report.checks.append(check)

    # 2) E2E + normalização
    if not args.skip_e2e:
        trx_dir = REPORTS / "trx"
        trx_dir.mkdir(exist_ok=True)
        for e2e in CFG["e2e"]:
            trx = trx_dir / f"{e2e['name']}.trx"
            cmd = e2e["cmd"] + [
                "--results-directory", str(trx_dir),
                "--logger", f"trx;LogFileName={trx.name}"]
            check = run(cmd, e2e["name"], e2e.get("timeout", 3600))
            report.checks.append(check)
            if trx.exists():
                flows = parse_trx(trx, CFG["flows"])
                for f in flows:
                    if f.status != FlowStatus.UNCOVERED or f.flow_id not in {
                            x.flow_id for x in report.flows}:
                        report.flows.append(f)
            else:
                check.ok = False
                check.detail += " | trx não gerado"

    if not report.flows:
        print("[gate] relatório inválido: nenhum fluxo coletado")
        return 3

    # 3) triagem Jev das falhas
    for f in report.flows:
        if f.status != FlowStatus.FAILED:
            continue
        verdict = jev_triage.triage({
            "project": CFG["project"], "flow": f.journey,
            "failed_tests": ", ".join(f.failed_tests)[:1500],
            "error": f.error or ""})
        hist = state["flow_failures"].setdefault(f.flow_id, {"causes": [], "count": 0})
        hist["count"] += 1
        if verdict:
            f.cause = verdict.cause
            f.severity = verdict.severity
            f.needs_human = verdict.needs_human >= 0.5 or verdict.screen_broken >= 0.9
            f.jev_confidence = verdict.confidence
            if verdict.cause.value not in hist["causes"]:
                hist["causes"].append(verdict.cause.value)
            print(f"[jev] {f.flow_id} {f.journey}: {verdict.cause.value} "
                  f"sev={verdict.severity:.1f} human={verdict.needs_human:.2f}")
        else:
            f.cause = Cause.UNKNOWN
            if "unknown" not in hist["causes"]:
                hist["causes"].append("unknown")
        if len(hist["causes"]) >= MAX_DISTINCT_CAUSES:
            f.needs_human = True

    triage_file = REPO / "gate" / "triage.json"
    triage_file.write_text(json.dumps({
        "run_id": report.run_id,
        "generated_at": report.generated_at,
        "verdicts": [
            {
                "flow_id": f.flow_id, "journey": f.journey,
                "status": f.status.value,
                "cause": f.cause.value if f.cause else None,
                "severity": f.severity, "needs_human": f.needs_human,
                "jev_confidence": f.jev_confidence,
            } for f in report.flows
        ]}, indent=2, ensure_ascii=False), encoding="utf-8")

    # 4) veredito
    all_steps_ok = all(c.ok for c in report.checks)
    uncovered = [f.flow_id for f in report.flows if f.status == FlowStatus.UNCOVERED]
    needs_human = [f.flow_id for f in report.flows if f.needs_human]
    print(f"[gate] ciclo {attempt}: {report.approved}/{report.planned} "
          f"({report.rate:.0%}) — etapas {'OK' if all_steps_ok else 'com falha'}")

    (REPORTS / "e2e.json").write_text(
        report.model_dump_json(indent=2), encoding="utf-8")
    STATE.write_text(json.dumps(state, indent=2), encoding="utf-8")
    _write_progress(report, attempt, needs_human)

    if needs_human:
        print(f"[gate] condição sensível/humana: {needs_human}")
        return 4
    if uncovered:
        print(f"[gate] fluxos sem cobertura: {uncovered}")
    if not all_steps_ok or report.rate < MIN_RATE:
        print(f"[gate] qualidade abaixo do gate (meta {MIN_RATE:.0%})")
        return 2
    print("[gate] APROVADO")
    return 0


def _write_progress(report: E2eReport, attempt: int, needs_human: list[str]) -> None:
    path = REPO / "docs" / "progresso.md"
    lines = [
        f"## Ciclo {attempt} — {report.generated_at}",
        f"- branch `{report.branch}` @ `{report.commit}`",
        f"- taxa: **{report.approved}/{report.planned} ({report.rate:.0%})** — meta {MIN_RATE:.0%}",
        "- etapas: " + ", ".join(f"{c.name}={'ok' if c.ok else 'FALHA'}" for c in report.checks),
    ]
    for f in report.flows:
        if f.status == FlowStatus.FAILED:
            lines.append(f"- falha {f.flow_id} {f.journey}: causa={f.cause} sev={f.severity}")
    if needs_human:
        lines.append(f"- requer humano: {', '.join(needs_human)}")
    path.parent.mkdir(exist_ok=True)
    existing = path.read_text(encoding="utf-8") if path.exists() else "# Progresso do gate E2E\n"
    path.write_text(existing.rstrip() + "\n\n" + "\n".join(lines) + "\n", encoding="utf-8")


if __name__ == "__main__":
    sys.exit(main())
