"""Converte resultados dotnet test (.trx) em FlowResult por fluxo mapeado."""
from __future__ import annotations

import xml.etree.ElementTree as ET
from pathlib import Path

from models import FlowResult, FlowStatus

NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"


def parse_trx(trx_path: Path, flows_cfg: dict[str, dict]) -> list[FlowResult]:
    """flows_cfg: flow_id -> {journey, matchers: [substring do nome do teste]}."""
    results = {
        fid: FlowResult(flow_id=fid, journey=cfg["journey"], status=FlowStatus.UNCOVERED)
        for fid, cfg in flows_cfg.items()
    }
    tree = ET.parse(trx_path)
    root = tree.getroot()

    for unit in root.iter(f"{NS}UnitTestResult"):
        name = unit.get("testName", "")
        outcome = unit.get("outcome", "")
        duration = unit.get("duration", "0:00:00")
        seconds = _hms(duration)
        error = ""
        if outcome != "Passed":
            info = unit.find(f"{NS}Output/{NS}ErrorInfo/{NS}Message")
            error = (info.text or "") if info is not None else ""

        for fid, cfg in flows_cfg.items():
            if any(m in name for m in cfg.get("matchers", [])):
                fr = results[fid]
                fr.tests.append(name)
                fr.duration_s += seconds
                if outcome == "Passed":
                    if fr.status in (FlowStatus.UNCOVERED, FlowStatus.SKIPPED):
                        fr.status = FlowStatus.PASSED
                elif outcome in ("NotExecuted",):
                    if fr.status == FlowStatus.UNCOVERED:
                        fr.status = FlowStatus.SKIPPED
                else:
                    fr.status = FlowStatus.FAILED
                    fr.failed_tests.append(name)
                    fr.error = (fr.error + "\n" + error).strip() if fr.error else error
                break

    for fr in results.values():
        if fr.tests and fr.status == FlowStatus.UNCOVERED:
            fr.status = FlowStatus.SKIPPED
    return list(results.values())


def _hms(value: str) -> float:
    try:
        parts = value.split(":")
        if len(parts) == 3:
            return int(parts[0]) * 3600 + int(parts[1]) * 60 + float(parts[2])
        return float(value)
    except (ValueError, IndexError):
        return 0.0
