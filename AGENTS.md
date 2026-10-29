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

## Branches e pull requests

- Todo pull request de trabalho tem a `dev` como base. A `main` só recebe PR vindo da `dev` (promoção): nunca abra PR
  de uma branch de trabalho direto para a `main`.
  O workflow `main-only-from-dev.yml` reprova o PR para a `main` que não venha da `dev`.
- Crie as branches de trabalho a partir da `dev` atualizada.
- Nada é publicado a partir da `dev`: os workflows que geram versão (APK, iOS e imagem Docker) publicam só no push
  na `main`. Enquanto a `dev` não é promovida, a versão publicada continua a antiga; ao promover, a esteira completa
  roda. Não acrescente gatilhos da `dev` a esses workflows.
- Exceção: o `deploy-dev.yml` publica a `dev` no ambiente de homologação (https://app-dev-monburan.gearhost.io, banco
  próprio e massa de demonstração), para testar antes de promover. Ele não gera versão nem toca em produção; segredos só
  no environment `dev` do GitHub, nunca no repositório (arquivos `*.publishsettings` ficam fora do Git).

## Commits

- Padrão **Conventional Commits** (`tipo(escopo): descrição`), em pt-BR, um assunto por commit.
- Antes de empurrar, `git log <base>..HEAD --format=%B | grep -iE 'co-authored|generated with|claude|copilot|codex|gemini'`
  não deve retornar nada.
