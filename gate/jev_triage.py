"""Triagem TypeSafe Jev: /v1/decide com perguntas tipadas (seções 7-8 da missão).

Uma chamada por falha de fluxo: state sanitizado + 4 perguntas tipadas.
Sem TYPESAFE_API_KEY a triagem é pulada e a causa fica 'unknown'.
"""
from __future__ import annotations

import os

import httpx

from models import Cause, JevVerdict
from sanitize import scrub_state

DECIDE_URL = os.environ.get(
    "TYPESAFE_DECIDE_URL", "https://api.typesafe.ai/v1/systemone")
MODEL = os.environ.get("TYPESAFE_MODEL", "jev-latest")
TIMEOUT = float(os.environ.get("TYPESAFE_TIMEOUT_S", "60"))


def _questions() -> dict:
    return {
        "cause": {
            "type": "choice",
            "instructions": "Qual a causa raiz mais provável desta falha E2E?",
            "criteria": {
                "bug_app": "Bug real no aplicativo sob teste (lógica, UI, API ou dados).",
                "test_error": "Erro no próprio teste: seletor quebrado, expectativa errada, seed ausente.",
                "environment": "Falha de ambiente: serviço fora do ar, porta, banco, dependência externa.",
                "flaky": "Teste não determinístico: timing, race, ordem, espera fraca.",
                "unknown": "Evidência insuficiente para classificar.",
            },
        },
        "screen_broken": {
            "type": "noul",
            "instructions": "Probabilidade de a tela/fluxo estar realmente quebrado para o usuário final.",
            "criteria": {
                "true": "A falha indica tela inacessível, ação sem efeito ou dado errado para o usuário.",
                "false": "A falha é de teste, timing ou ambiente, sem impacto real ao usuário.",
            },
        },
        "needs_human": {
            "type": "noul",
            "instructions": "Esta falha exige decisão humana (produto, credencial, custo, dado sensível)?",
            "criteria": {
                "true": "Requer credencial, segredo, decisão de produto/negócio ou acesso externo.",
                "false": "Um agente pode investigar e corrigir com o código e o ambiente locais.",
            },
        },
        "severity": {
            "type": "score",
            "instructions": "Severidade para o usuário final, de 0 (cosmético) a 3 (bloqueante).",
            "criteria": [
                "Cosmético ou interno; usuário quase não percebe.",
                "Funcionalidade degradada com contorno possível.",
                "Fluxo importante quebrado; sem contorno simples.",
                "Bloqueante: login, dados perdidos, risco financeiro ou segurança.",
            ],
        },
    }


def triage(state: dict, api_key: str | None = None) -> JevVerdict | None:
    key = api_key or os.environ.get("TYPESAFE_API_KEY")
    if not key:
        return None
    body = {
        "model": MODEL,
        "state": scrub_state(state),
        "questions": _questions(),
    }
    try:
        with httpx.Client(timeout=TIMEOUT) as client:
            resp = client.post(
                DECIDE_URL,
                json=body,
                headers={"Authorization": f"Bearer {key}",
                         "Content-Type": "application/json"},
            )
            if resp.status_code != 200:
                print(f"[jev] HTTP {resp.status_code}: {resp.text[:200]}")
                return None
            data = resp.json()
    except httpx.HTTPError as exc:
        print(f"[jev] indisponivel: {exc}")
        return None

    answers = data.get("answers", data)
    cause = answers.get("cause", {})
    try:
        cause_value = Cause(cause.get("choice", "unknown"))
    except ValueError:
        cause_value = Cause.UNKNOWN
    noul = lambda name: float(answers.get(name, {}).get(
        "noul", answers.get(name, {}).get("score", 0.0)) or 0.0)
    return JevVerdict(
        cause=cause_value,
        screen_broken=noul("screen_broken"),
        needs_human=noul("needs_human"),
        severity=noul("severity"),
        confidence=float(cause.get("confidence", 0.0)),
    )
