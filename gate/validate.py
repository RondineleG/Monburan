"""Validação determinística do relatório E2E (seção 9 da missão).

Uso: python gate/validate.py
Verifica: existência do relatório, integridade do JSON, conformidade com o
contrato Pydantic, correspondência entre fluxos do relatório e do config,
recência da execução e consistência do veredito.
Exit codes: 0 válido | 3 inválido.
"""
from __future__ import annotations

import json
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from models import E2eReport, FlowStatus

REPO = Path(__file__).resolve().parent.parent
CFG = json.loads((REPO / "gate" / "config.json").read_text(encoding="utf-8"))
REPORT = REPO / "reports" / "e2e.json"
MAX_AGE = timedelta(hours=24)


def fail(msg: str) -> int:
    print(f"[validate] inválido: {msg}")
    return 3


def main() -> int:
    if not REPORT.exists():
        return fail("reports/e2e.json não existe")
    try:
        raw = json.loads(REPORT.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        return fail(f"JSON inválido: {exc}")
    try:
        report = E2eReport.model_validate(raw)
    except Exception as exc:  # pydantic ValidationError
        return fail(f"fora do contrato: {exc}")

    if report.project != CFG["project"]:
        return fail(f"projeto '{report.project}' != '{CFG['project']}'")
    if not report.run_id or not report.generated_at:
        return fail("run_id/generated_at ausentes")

    gen = datetime.fromisoformat(report.generated_at)
    if gen.tzinfo is None:
        gen = gen.replace(tzinfo=timezone.utc)
    if datetime.now(timezone.utc) - gen > MAX_AGE:
        return fail(f"relatório antigo: {gen.isoformat()}")

    planned = set(CFG["flows"])
    seen = {f.flow_id for f in report.flows}
    if missing := planned - seen:
        return fail(f"fluxos previstos ausentes no relatório: {sorted(missing)}")
    if extra := seen - planned:
        return fail(f"fluxos não previstos no relatório: {sorted(extra)}")

    status_map = {f.flow_id: f.status for f in report.flows}
    approved = sum(1 for s in status_map.values() if s == FlowStatus.PASSED)
    if report.rate != approved / len(planned):
        return fail("taxa do relatório não corresponde aos fluxos")

    failed = [fid for fid, s in status_map.items() if s == FlowStatus.FAILED]
    if failed:
        print(f"[validate] relatório válido com falhas: {failed}")
    else:
        print(f"[validate] relatório válido: {approved}/{len(planned)} "
              f"({report.rate:.0%}) em {report.run_id}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
