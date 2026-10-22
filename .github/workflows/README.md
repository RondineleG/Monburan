# Workflows — visão geral e proteção da branch main

Este diretório concentra as esteiras do Monburan. Este documento descreve o contrato de cada
workflow e as **configurações manuais** que precisam ser feitas uma única vez no GitHub
(Settings > Rules > Rulesets) para que a main só receba código validado.

## Gatilhos

| Workflow | push main | push dev | PR para main | Outros |
|---|---|---|---|---|
| `ci-cd.yml` | build+testes+scan (deploy Azure se configurado) | build+testes+scan, **sem E2E** | build+testes+scan+**E2E** | `workflow_dispatch` |
| `main-only-from-dev.yml` | — | — | reprova PR que não venha da dev | — |
| `codeql.yml` | sim | sim | sim | agenda semanal |
| `docker-ghcr.yml` | build imagem GHCR + **deploy VPS** | não | não | `workflow_dispatch`, rebuild semanal |
| `android-release.yml` / `ios-build.yml` | sim (paths de mobile) | não | sim (paths de mobile) | `workflow_dispatch` |
| `infra-scripts.yml` | sim (paths de infra/vps) | não | sim (paths de infra/vps) | `workflow_dispatch` |

Pontos de segurança já garantidos no código dos workflows:

- Nenhum job do `ci-cd.yml` publica em produção a partir da dev. `deploy-production` exige
  `github.ref == 'refs/heads/main' && github.event_name == 'push'`.
- O deploy na VPS (`docker-ghcr.yml` → `deploy-vps`) só roda na main, com `environment: prod`
  e concorrência serializada (`group: deploy-vps`, sem cancelamento).
- `GITHUB_TOKEN` com permissão mínima: `contents: read` no nível do workflow em todos os
  workflows de validação; escrita apenas onde é imprescindível (`docker-ghcr.yml` publica
  pacote, `android-release.yml` cria release, `codeql.yml` grava alertas).
- Segredos passam por `env` e nunca são interpolados diretamente em scripts do shell; o
  `security-scan` procura formatos de chave/token versionados e reprova o PR.

## Checks de CI do `ci-cd.yml`

| Job | Quando roda | Papel |
|---|---|---|
| `build-and-test` | push main/dev, PR main, dispatch | restore, build da solução, testes unit, integração (Testcontainers/Postgres) e de API; publica o artefato `monburan-app` e os relatórios de cobertura/TRX |
| `e2e` | PR main e dispatch | suíte Playwright completa (app + Postgres de serviço + Chromium) |
| `security-scan` | push main/dev, PR main, dispatch | varredura de segredos versionados + pacotes NuGet vulneráveis |
| `deploy-staging` / `deploy-production` | push (dev/main) com Azure configurado | só rodam depois de `build-and-test` + `security-scan` verdes |

Falhas sempre propagam: nenhum job de teste usa `continue-on-error`; os únicos
`continue-on-error` são geração/upload do relatório de cobertura (diagnóstico auxiliar).

## Configuração manual no GitHub (Settings > Rules > Rulesets)

Criar um ruleset com as seguintes definições (não é possível fazê-lo pelos workflows):

1. **Nome:** `main — validação obrigatória`
2. **Escopo (Enforcement):** branch `main`, `Enforced`
3. **Regras a adicionar:**
   - **Require status checks to pass before merging** com os checks:
     - `build-and-test`
     - `e2e`
     - `security-scan`
     - `origem-do-pr` (workflow `main-only-from-dev.yml`: garante que só a dev promove para a main)
     - `Analyze (csharp)`, `Analyze (javascript-typescript)`, `Analyze (actions)` (CodeQL)
   - **Require status checks to pass before merging** → marcar
     *“Require branches to be up to date before merging”* (evita merge com base obsoleta).
   - **Require a pull request before merging** com no mínimo **1 aprovação**
     (opcional, mas recomendado).
   - **Do not allow bypassing the above settings** (impede admin de furar a validação).
4. **Exceções:** adicionar uma *bypass list* apenas se houver uma conta/service account
   explicitamente autorizada a quebrar o bloqueio (normalmente nenhuma).

Observações:

- O E2E só roda em PRs para a main, então o check `e2e` existe em todo PR relevante; em
  eventos de push ele aparece como *skipped*, o que o GitHub conta como sucesso — por isso a
  regra funciona tanto no merge quanto no dia a dia da dev.
- Depois de salvar o ruleset, **valide com um PR de teste**: um PR sem aprovação ou com teste
  vermelho deve ficar bloqueado no botão de merge.
