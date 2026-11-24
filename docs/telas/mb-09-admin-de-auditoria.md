# Admin de auditoria

- **Id do fluxo:** MB-09
- **Escopo (telas/rotas):** trilha de auditoria
- **Jornada associada:** Admin de auditoria

## Perfis e permissões
Cobertos pelos casos da spec `AdminAuditTests` (candidato, anunciante, admin e visitante
conforme o escopo). Ver `docs/fluxos.md`.

## Pré-condições
App em `http://localhost:5095` (ou `E2E_BASE_URL`) com banco descartável;
usuários/seeds criados pela própria spec.

## Ações e regras de negócio
Exercitadas pela spec `AdminAuditTests`.

## Estados

| Estado | Observado |
|--------|-----------|
| Inicial | Renderização coberta pelos casos da spec |
| Loading | Esperas determinísticas do Playwright |
| Vazio | Coberto onde a spec exerce estado inicial |
| Sucesso | 5 caso(s) de teste passaram |
| Erro | Validações e bloqueios cobertos pela spec |
| Responsivo | Coberto pelos testes de shell/interação |

## Testes associados
- Spec: `AdminAuditTests`
- Casos: 5
- Resultado: **passed** (9s)

## Evidências da validação
- Run: `cycle-2-7a5dfb0b` em `2026-11-24T21:54:53.237969+00:00` (branch `HEAD` @ `21363c0`)
- Relatório: `reports/e2e.json` (MB-09 = passed)
- TRX: `reports/trx/e2e.trx`

## Limitações conhecidas
Nenhuma registrada nesta execução.

## Issues relacionadas
Nenhuma.
