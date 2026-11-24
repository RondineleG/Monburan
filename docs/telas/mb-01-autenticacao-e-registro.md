# Autenticacao e registro

- **Id do fluxo:** MB-01
- **Escopo (telas/rotas):** login, cadastro, logout
- **Jornada associada:** Autenticacao e registro

## Perfis e permissões
Cobertos pelos casos da spec `AuthenticationTests` (candidato, anunciante, admin e visitante
conforme o escopo). Ver `docs/fluxos.md`.

## Pré-condições
App em `http://localhost:5095` (ou `E2E_BASE_URL`) com banco descartável;
usuários/seeds criados pela própria spec.

## Ações e regras de negócio
Exercitadas pela spec `AuthenticationTests`.

## Estados

| Estado | Observado |
|--------|-----------|
| Inicial | Renderização coberta pelos casos da spec |
| Loading | Esperas determinísticas do Playwright |
| Vazio | Coberto onde a spec exerce estado inicial |
| Sucesso | 9 caso(s) de teste passaram |
| Erro | Validações e bloqueios cobertos pela spec |
| Responsivo | Coberto pelos testes de shell/interação |

## Testes associados
- Spec: `AuthenticationTests`
- Casos: 9
- Resultado: **passed** (6s)

## Evidências da validação
- Run: `cycle-2-7a5dfb0b` em `2026-11-24T21:54:53.237969+00:00` (branch `HEAD` @ `21363c0`)
- Relatório: `reports/e2e.json` (MB-01 = passed)
- TRX: `reports/trx/e2e.trx`

## Limitações conhecidas
Nenhuma registrada nesta execução.

## Issues relacionadas
Nenhuma.
