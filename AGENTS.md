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
- Crie as branches de trabalho a partir da `dev` atualizada.
- Nada é publicado a partir da `dev`: os workflows que geram versão (APK, iOS e imagem Docker) publicam só no push
  na `main`. Enquanto a `dev` não é promovida, a versão publicada continua a antiga; ao promover, a esteira completa
  roda. Não acrescente gatilhos da `dev` a esses workflows.

## Commits

- Padrão **Conventional Commits** (`tipo(escopo): descrição`), em pt-BR, um assunto por commit.
- Antes de empurrar, `git log <base>..HEAD --format=%B | grep -iE 'co-authored|generated with|claude|copilot|codex|gemini'`
  não deve retornar nada.
