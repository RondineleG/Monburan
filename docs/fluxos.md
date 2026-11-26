# Inventario de fluxos — Monburan

Cobertura planejada do gate E2E (`gate/`, taxa = aprovados / previstos ≥ 90%).
Cada fluxo é validado por specs Playwright em `tests/Monburan.E2ETests`
(app em `http://localhost:5095`, `E2E_BASE_URL` sobrescreve).

| Id | Jornada | Escopo | Spec |
|----|---------|--------|------|
| MB-01 | Autenticacao e registro | login, cadastro, logout | AuthenticationTests |
| MB-02 | Paginas publicas | home, vagas publicas, SEO | PublicPagesTests |
| MB-03 | Experiencia de vagas | busca, filtros, detalhe | JobsExperienceTests |
| MB-04 | Fluxo do candidato | perfil, candidatura, internacional | CandidateFlowTests |
| MB-05 | Fluxo do anunciante | publicar vaga, publicidade local | AdvertiserFlowTests |
| MB-06 | Admin de vagas | moderacao e gestao | AdminJobsTests |
| MB-07 | Admin de contas | usuarios e papeis | AdminAccountsTests |
| MB-08 | Admin de provedores de IA | chaves e providers | AdminAiProvidersTests |
| MB-09 | Admin de auditoria | trilha de auditoria | AdminAuditTests |
| MB-10 | Hardening de seguranca | headers, auth, isolamento | SecurityHardeningTests |
| MB-11 | CSP enforcement | politica de conteudo | CspEnforcementTests |
| MB-12 | Interacao do shell | navegacao e layout | ShellInteractionTests |
| MB-13 | Varredura completa | jornada de ponta a ponta | FullSweepTests |

Legenda de status no `reports/e2e.json`: passed | failed | skipped | uncovered.
