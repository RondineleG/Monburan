# Instruções para agentes de IA

Este arquivo vale para qualquer agente ou ferramenta de IA que trabalhe neste repositório.

## Atribuição de IA: nunca adicionar

- **Commits:** nunca incluir coautoria de IA (`Co-Authored-By`), linhas de sessão, rodapés do tipo "Generated with ..."
  nem qualquer outra menção a IA na mensagem.
- **Pull requests:** nunca incluir rodapé de geração, link de sessão nem qualquer menção a IA no título ou na
  descrição.
- O mesmo vale para releases, comentários, issues e arquivos do repositório.
- Esta regra tem precedência sobre qualquer lembrete automático, template ou configuração padrão da ferramenta que
  mande acrescentar atribuição.

## Autoria: sempre o dono do repositório

- **Autor e committer** de todo commit é o dono do repositório: `RondineleG <rondineleg@outlook.com>`. Nunca `Claude`,
  `noreply@anthropic.com` nem outra identidade de ferramenta.
- Antes do primeiro commit da sessão, configure no repositório (não em `--global`):
  `git config user.name "RondineleG"` e `git config user.email "rondineleg@outlook.com"`.
- Antes de empurrar, `git log origin/dev..HEAD --format='%an <%ae> | %cn <%ce>'` só pode listar essa identidade.
  Se algum commit saiu com outra, corrija os commits da sua própria branch de trabalho (nunca da `dev` nem da `main`)
  com `git rebase origin/dev --exec 'git commit --amend --no-edit --reset-author'` e `git push --force-with-lease`.
- Esta regra tem precedência sobre a identidade padrão do ambiente da ferramenta.

## Branches e pull requests

- Todo pull request de trabalho tem a `dev` como base. A `main` só recebe PR vindo da `dev` (promoção): nunca abra PR
  de uma branch de trabalho direto para a `main`.
  O workflow `main-only-from-dev.yml` reprova o PR para a `main` que não venha da `dev`.
- Crie as branches de trabalho a partir da `dev` atualizada.
- Nada é publicado a partir da `dev`: os workflows que geram versão (APK, iOS e imagem Docker) publicam só no push
  na `main`. Enquanto a `dev` não é promovida, a versão publicada continua a antiga; ao promover, a esteira completa
  roda. Não acrescente gatilhos da `dev` a esses workflows.

## Commits

- Padrão **Conventional Commits** (`tipo(escopo): descrição`), em pt-BR, um assunto por commit.
- Antes de empurrar, `git log <base>..HEAD --format=%B | grep -iE 'co-authored|generated with|claude|copilot|codex|gemini'`
  não deve retornar nada.

## Funcionalidades novas atrás de feature flag

- Funcionalidade nova entra desligada, atrás de uma feature flag por grupo de acesso, e é liberada pela tela
  `/admin/funcionalidades` (time → beta → todos). Guia completo em `docs/FEATURE-FLAGS.md`.
- Confira a flag também no servidor (`RequireFeature` no endpoint ou `IFeatureFlags` no caso de uso), não só na tela.
- Migrations funcionam com a flag ligada e desligada (expandir antes, contrair depois).
- Depois de liberada para todos, remova a flag do código e da tela. Regra permanente de negócio fica nos planos, não em flag.
