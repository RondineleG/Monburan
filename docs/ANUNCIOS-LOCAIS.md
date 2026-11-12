# Campanhas de publicidade local

Campanhas aparecem como um único cartão discreto no detalhe de vagas ativas. O cartão fica abaixo do conteúdo da vaga no celular e na coluna lateral do desktop; não sobrepõe o botão de candidatura nem permanece fixo durante a rolagem. Sem uma campanha ativa que corresponda à cidade e UF da vaga, nenhum espaço vazio é exibido.

## Liberação

1. Em `/admin/funcionalidades`, crie `publicidade-local` com descrição e sem liberação para todos.
2. Libere a flag ao grupo `admin` para revisar o formulário e cadastrar campanhas.
3. Quando estiver pronto para o público, habilite **Todos**. A exibição pública exige essa liberação; campanhas não são mostradas apenas porque a pessoa está autenticada.
4. Pause a flag ou a campanha para interromper a exibição.

O painel fica em `/admin/publicidade-local` e exige a política de administração (incluindo 2FA quando exigido). Criar, editar e remover campanhas passa pela verificação da flag no servidor e fica na trilha de auditoria.

## Cadastro de campanha

- Nome do anunciante, título e texto curto.
- Imagem opcional enviada ao próprio Monburan (JPEG, PNG, GIF ou WebP, até 5 MB). O conteúdo é validado por assinatura de arquivo.
- Link de destino HTTPS; o clique passa por `/ads/{id}/click`, que contabiliza o acesso, remove o referrer e redireciona ao destino.
- Uma ou mais localidades no formato `Cidade, UF`, uma por linha.
- Datas inicial/final e status ativo/pausado.
- Contagem de impressões após o render interativo e de cliques pelo redirecionamento.

O catálogo permitido acompanha os municípios já usados pelo coletor de vagas: Ponta Grossa, Curitiba, Joinville e Itajaí e cidades próximas listadas em `tools/job-collector/collect.mjs`. Essa cobertura é aproximada e pode ser ajustada conforme a equipe confirmar o raio atendido. A correspondência é por cidade e UF exatas; não há coleta de GPS.

Campanhas são cadastradas e ativadas manualmente. Esta entrega gerencia a exibição e as métricas básicas; pagamento do espaço publicitário é combinado fora da plataforma. Não venda métricas como usuários únicos: as impressões contam renderizações da página e podem incluir visitas repetidas.
