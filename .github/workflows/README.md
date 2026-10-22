# Workflows — visão geral, fluxo de validação e proteção das branches

Este diretório concentra as esteiras do Monburan. Este documento descreve o contrato de cada
workflow e as **configurações manuais** que precisam ser feitas uma única vez no GitHub
(Settings > Rules > Rulesets) para que dev e main só recebam código validado.

## Fluxo de desenvolvimento

```
Feature branch (criada a partir da dev)
    ↓
Pull Request para dev
    ↓
build-and-test + security-scan            ← validação rápida, SEM E2E
    ↓
Merge na dev
    ↓
Validação de push na dev (segunda camada) ← build-and-test + security-scan, SEM E2E
    ↓
Pull Request da dev para main (promoção)
    ↓
build-and-test + security-scan + e2e      ← validação completa, COM E2E (Playwright)
    ↓
Merge na main
    ↓
Publicação e deploy conforme workflows existentes
    (docker-ghcr.yml: imagem GHCR → deploy na VPS, só na main)
```

## Gatilhos

| Workflow | push main | push dev | PR para dev | PR para main | Outros |
|---|---|---|---|---|---|
| `ci-cd.yml` | build+testes+scan (deploy Azure de produção, se configurado), sem E2E | build+testes+scan, **sem E2E** | build+testes+scan, **sem E2E** | build+testes+scan+**E2E** | `workflow_dispatch` (com E2E) |
| `main-only-from-dev.yml` | — | — | — | reprova PR que não venha da dev | — |
| `codeql.yml` | sim | sim | sim | sim | agenda semanal |
| `docker-ghcr.yml` | build imagem GHCR + **deploy VPS** (só com mudança em `src/**`, `infra/vps/**` ou no próprio arquivo) | não | não | não | `workflow_dispatch`, disparo pelo `rebuild-semanal` |
| `rebuild-semanal.yml` | não | não | não | não | cron semanal (segunda 03:00 BRT); dispara o `docker-ghcr.yml` por `workflow_dispatch` |
| `android-release.yml` / `ios-build.yml` | sim (paths de mobile) | não | não | sim (paths de mobile) | `workflow_dispatch` |
| `infra-scripts.yml` | sim (paths de infra/vps) | não | não | sim (paths de infra/vps) | `workflow_dispatch` |

Pushes em feature branches **não** disparam o `ci-cd.yml`: a validação da feature acontece
no próprio PR (para a dev), então não há gatilho de push além de `main` e `dev`.

### Custo/benefício da validação duplicada (PR para dev + push na dev)

Após o merge de um PR na dev, o mesmo commit é validado duas vezes: no `pull_request` e no
`push` subsequente. Mantemos as duas camadas de propósito:

- O PR reprova a integração **antes** do merge (feedback ao autor);
- O push na dev garante que a dev só avança com código verde, mesmo em merge administrativo,
  fast-forward ou correção de conflito — o estado final da branch é sempre testado.

Custo: ~2 execuções de `build-and-test` + `security-scan` por integração (cada uma de
poucos minutos, com cache de NuGet). Benefício: a dev nunca acumula commit não validado.
Alternativa avaliada e descartada nesta etapa: remover o push da dev, o que abriria uma janela
de integração sem validação.

## Pontos de segurança garantidos no código dos workflows

- Deploy **nunca** roda a partir de PR: `deploy-production` exige `github.event_name == 'push'`.
- `deploy-production` (Azure) exige `github.ref == 'refs/heads/main'` (push na main) e a
  variável `vars.AZURE_RESOURCE_GROUP`. Push na dev e PR para main não satisfazem a condição.
- Não há job de deploy na dev: nada é publicado a partir da `dev` (ver `AGENTS.md`). O deploy
  de produção hoje é a VPS (`docker-ghcr.yml` → `deploy-vps`).
- O deploy na VPS (`docker-ghcr.yml` → `deploy-vps`) só roda na main, com `environment: prod`
  e concorrência serializada (`group: deploy-vps`, sem cancelamento). Esse workflow permanece
  intacto.
- `GITHUB_TOKEN` com permissão mínima: `contents: read` nos workflows de validação. Escrita só onde é necessária: `docker-ghcr.yml` (pacote, por job), `android-release.yml` (release), `ios-build.yml` (job `ipa`, anexa o .ipa à release), `codeql.yml` (alertas) e `rebuild-semanal.yml` (`actions: write`, para disparar o `docker-ghcr.yml`).
- Segredos passam por `env` e nunca são interpolados diretamente em scripts do shell; o
  `security-scan` procura formatos de chave/token versionados e reprova o PR.

## Checks de CI do `ci-cd.yml`

| Job | Quando roda | Papel |
|---|---|---|
| `build-and-test` | push main/dev, PR dev/main, dispatch | restore, build da solução, testes unit, integração (Testcontainers/Postgres) e de API; publica o artefato `monburan-app` e os relatórios de cobertura/TRX |
| `e2e` | **PR para main** e dispatch | suíte Playwright completa (app + Postgres de serviço + Chromium) |
| `security-scan` | push main/dev, PR dev/main, dispatch | varredura de segredos versionados + pacotes NuGet vulneráveis |
| `deploy-production` | push na main com Azure configurado | só roda depois de `build-and-test` + `security-scan` verdes; nunca em PR |

Falhas sempre propagam: nenhum job de teste usa `continue-on-error`; os únicos
`continue-on-error` são geração/upload do relatório de cobertura (diagnóstico auxiliar).

Confiabilidade e custo:

- **Cache de NuGet** (`actions/cache` em `~/.nuget/packages`, chave por `**/*.csproj` +
  `NuGet.config`) nos jobs `build-and-test`, `e2e` e `security-scan`.
- **Testcontainers** (integração): usa o Docker do próprio runner `ubuntu-latest` — nenhum
  serviço extra configurado.
- **Timeouts:** 30 min em `build-and-test` e `e2e`, 20 min em `security-scan`.
- **Concorrência:** execução obsoleta de PR é cancelada (`cancel-in-progress` só em
  `pull_request`); pushes em andamento terminam normalmente.
- **Resultados:** TRX dos testes (`test-results-trx`, 7 dias) e do E2E (no artefato
  `e2e-app-log`) são anexados mesmo em falha (`if: always()`); cobertura HTML em
  `coverage-report`.

## Configuração manual no GitHub (Settings > Rules > Rulesets)

Dois rulesets precisam ser criados (não é possível fazê-lo pelos workflows):

### Ruleset da dev

1. **Nome:** `dev — validação obrigatória`
2. **Escopo (Enforcement):** branch `dev`, `Enforced`
3. **Regras:**
   - **Require status checks to pass before merging** com os checks:
     - `build-and-test`
     - `security-scan`
   - **Não incluir `e2e`** — a suíte completa não roda em PRs para a dev; exigir o check
     bloquearia todo merge (o GitHub aguardaria um check que nunca será reportado).
   - Marcar *“Require branches to be up to date before merging”*.
   - **Require a pull request before merging** com no mínimo 1 aprovação (recomendado).

### Ruleset da main

1. **Nome:** `main — validação obrigatória`
2. **Escopo (Enforcement):** branch `main`, `Enforced`
3. **Regras:**
   - **Require status checks to pass before merging** com os checks:
     - `build-and-test`
     - `e2e`
     - `security-scan`
     - `origem-do-pr` (workflow `main-only-from-dev.yml`: garante que só a dev promove para a main)
     - `Analyze (csharp)`, `Analyze (javascript-typescript)`, `Analyze (actions)` (CodeQL)
   - Marcar *“Require branches to be up to date before merging”*.
   - **Require a pull request before merging** com no mínimo 1 aprovação (recomendado).
   - **Do not allow bypassing the above settings** (impede admin de furar a validação).

Observações:

- O `e2e` só roda em PRs para a main e em dispatch manual; em push ele aparece como
  *skipped*, o que o GitHub conta como sucesso — por isso pode ser check obrigatório da main.
- O CodeQL (`codeql.yml`) roda também em PRs para a dev; os checks `Analyze (...)` podem ser
  adicionados ao ruleset da dev se desejado, mas o essential para a dev é `build-and-test` +
  `security-scan`.
- Depois de salvar cada ruleset, **valide com um PR de teste**: sem aprovação ou com teste
  vermelho, o botão de merge deve ficar bloqueado.
