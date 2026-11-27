# CSP enforcement

- **Id do fluxo:** MB-11
- **Escopo (telas/rotas):** politica de conteudo
- **Jornada associada:** CSP enforcement

## Perfis e permissões
Cobertos pelos casos da spec `CspEnforcementTests` (candidato, anunciante, admin e visitante
conforme o escopo). Ver `docs/fluxos.md`.

## Pré-condições
App em `http://localhost:5095` (ou `E2E_BASE_URL`) com banco descartável;
usuários/seeds criados pela própria spec.

## Ações e regras de negócio
Exercitadas pela spec `CspEnforcementTests`.

## Estados

| Estado | Observado |
|--------|-----------|
| Inicial | Renderização coberta pelos casos da spec |
| Loading | Esperas determinísticas do Playwright |
| Vazio | Coberto onde a spec exerce estado inicial |
| Sucesso | 7 caso(s) de teste passaram |
| Erro | Validações e bloqueios cobertos pela spec |
| Responsivo | Coberto pelos testes de shell/interação |

## Testes associados
- Spec: `CspEnforcementTests`
- Casos: 7
- Resultado: **passed** (26s)

## Evidências da validação
- Run: `cycle-2-7a5dfb0b` em `2026-11-24T21:54:53.237969+00:00` (branch `HEAD` @ `21363c0`)
- Relatório: `reports/e2e.json` (MB-11 = passed)
- TRX: `reports/trx/e2e.trx`

## Limitações conhecidas
Nenhuma registrada nesta execução.

## Issues relacionadas
Nenhuma.
