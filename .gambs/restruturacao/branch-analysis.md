# Análise de branches para reestruturação Gambs

Gerado em UTC: 2026-11-26T04:03:14Z
Base: `main`

Critério: consolidar apenas conteúdo único, ainda não mergeado e com merge limpo. Branches grandes, conflitantes ou ambíguas ficam para triagem manual. Nada aqui autoriza merge na `main`.

| Branch | Ahead | Behind | Arquivos | Merge | Decisão | Motivo |
|---|---:|---:|---:|---|---|---|
| `dev` | 8 | 0 | 0 | not_needed | descartar | diff final vazio apesar de commits administrativos/de merge |
| `feat/integracao-himarshall-vagas-int` | 98 | 0 | 240 | clean | integrar | conteúdo único com merge limpo; PR aberto existe, mas ainda não está mergeado: #122 |

## Detalhes dos commits candidatos

### `dev`
- `b83f4c0` 2026-11-02 — feat: merge pull request #111
- `82fd72b` 2026-11-02 — feat: merge pull request #89
- `7a02bae` 2026-10-22 — feat: merge pull request #72
- `b371486` 2026-10-22 — feat: merge pull request #69
- `3c4d797` 2026-10-20 — feat: merge pull request #65
- `0bd1f8b` 2026-10-19 — feat: merge pull request #60
- `aaf2ef6` 2026-10-19 — feat: merge pull request #40
- `4640b9e` 2026-10-18 — feat: merge pull request #35

### `feat/integracao-himarshall-vagas-int`
- `196c0da` 2026-11-25 — test(e2e): revalidacao do gate aprovada (13/13 fluxos)
- `c213439` 2026-11-25 — test(e2e): revalidacao do gate aprovada (13/13 fluxos)
- `b23ff28` 2026-11-25 — test(e2e): revalidacao do gate aprovada (13/13 fluxos)
- `d0a9f24` 2026-11-25 — test(e2e): revalidacao do gate aprovada (13/13 fluxos)
- `89d1daf` 2026-11-25 — test(e2e): nova revalidacao do gate aprovada (13/13 fluxos)
- `4a522d1` 2026-11-25 — fix(gate): contador de tentativas zera em ciclo aprovado + ciclo aprovado (13/13)
- `cea6004` 2026-11-25 — test(e2e): ciclo 5 do gate aprovado (13/13 fluxos)
- `34e9a27` 2026-11-24 — fix(testes): espera pela tela destino no aviso de alteracoes nao salvas + ciclo 4 aprovado
- `a933d09` 2026-11-24 — test(e2e): gate de validacao com triagem Jev e docs de telas
- `02b4dfb` 2026-11-24 — fix(testes): asserts deterministicos e tolerancia no E2E
- `21363c0` 2026-11-24 — feat: merge feature/ui-mobile-first na feat/integracao-himarshall-vagas-int
- `86a1ca4` 2026-11-24 — feat(ui): move publicidade local para o topo da coluna lateral da vaga
