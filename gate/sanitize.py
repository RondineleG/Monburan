"""Sanitização de qualquer payload antes de sair da VM (seção 10 da missão)."""
from __future__ import annotations

import re

PATTERNS = [
    (re.compile(r"(?i)(bearer|token|api[_-]?key|secret|password|passwd|pwd|authorization)"
                r"([\s\"'=:]+)([^\s\"',;}]+)"), r"\1\2<redacted>"),
    (re.compile(r"(?i)(set-cookie|cookie)([\s\"'=:]+)([^\s\"',;}]+)"), r"\1\2<redacted>"),
    (re.compile(r"(?i)(server|host|data source)\s*=\s*[^;\s\"']+"), r"\1=<host>"),
    (re.compile(r"postgres(ql)?://[^\s\"']+"), "postgresql://<conn>"),
    (re.compile(r"sk-[A-Za-z0-9_\-]{8,}"), "<redacted>"),
    (re.compile(r"mbi_[A-Za-z0-9_\-]{4,}"), "<token>"),
    (re.compile(r"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"), "<email>"),
    (re.compile(r"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"), "<guid>"),
    (re.compile(r"[A-Za-z]:\\[^\s\"'<>|]+"), "<path>"),
    (re.compile(r"/home/[^\s\"'<>|]+|/tmp/[^\s\"'<>|]+"), "<path>"),
    (re.compile(r"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b"), "<ip>"),
]


def scrub(text: object, max_len: int = 4000) -> str:
    s = str(text)
    for rx, repl in PATTERNS:
        s = rx.sub(repl, s)
    return s[:max_len]


def scrub_state(state: dict) -> dict:
    return {k: scrub(v) for k, v in state.items()}
