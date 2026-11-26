"""Modelos tipados do gate E2E (Jev + validação determinística)."""
from __future__ import annotations

from datetime import datetime, timezone
from enum import Enum
from typing import Optional

from pydantic import BaseModel, Field


def utcnow() -> str:
    return datetime.now(timezone.utc).isoformat()


class FlowStatus(str, Enum):
    PASSED = "passed"
    FAILED = "failed"
    SKIPPED = "skipped"
    UNCOVERED = "uncovered"  # previsto no inventário, sem teste mapeado


class Cause(str, Enum):
    BUG_APP = "bug_app"
    TEST_ERROR = "test_error"
    ENVIRONMENT = "environment"
    FLAKY = "flaky"
    UNKNOWN = "unknown"


class FlowResult(BaseModel):
    flow_id: str
    journey: str
    status: FlowStatus
    tests: list[str] = Field(default_factory=list)
    failed_tests: list[str] = Field(default_factory=list)
    error: Optional[str] = None
    duration_s: float = 0.0
    cause: Optional[Cause] = None
    severity: Optional[float] = None
    needs_human: bool = False
    jev_confidence: Optional[float] = None


class CheckResult(BaseModel):
    name: str  # build | lint | unit | integration | e2e | coverage | rate
    ok: bool
    detail: str = ""


class E2eReport(BaseModel):
    schema_version: int = 1
    project: str
    branch: str
    commit: str
    run_id: str
    generated_at: str = Field(default_factory=utcnow)
    checks: list[CheckResult] = Field(default_factory=list)
    flows: list[FlowResult] = Field(default_factory=list)

    @property
    def planned(self) -> int:
        return len(self.flows)

    @property
    def approved(self) -> int:
        return sum(1 for f in self.flows if f.status == FlowStatus.PASSED)

    @property
    def rate(self) -> float:
        return self.approved / self.planned if self.planned else 0.0


class JevVerdict(BaseModel):
    cause: Cause
    screen_broken: float
    needs_human: float
    severity: float
    confidence: float
