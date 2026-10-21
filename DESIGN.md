# DESIGN — sistema de design da Monburan

Referência normativa da interface do site (`src/Monburan.Web`) e do app (`src/Monburan.Mobile`). Antes de criar classe,
cor, ícone ou componente, procure aqui. As regras marcadas com ✔ são verificadas por teste.

## Princípios

1. **A tarefa primeiro.** Cada tela responde uma pergunta; uma ação primária por área.
2. **Uma fonte de orientação.** A barra superior mostra a trilha ("Área › Página"); o h1 é o único título da tela.
   Sem rótulo em caixa alta acima do título.
3. **Um padrão por problema.** Cabeçalho, estados, filtro, tabela, confirmação e acesso negado têm um componente ou
   contrato único.
4. **Estado sempre explícito.** Carregando, vazio, vazio por filtro, erro, sem permissão, ocupado e sucesso são distintos.
5. **Acessível por construção.** Contraste, alvo, foco e nome de controle verificados por teste e por captura.
6. **Identidade quente e sóbria.** Dourado e marrom; a Cinzel só no logotipo e no título da página inicial.

## Identidade

- **Nome:** Monburan é a leitura japonesa de *Mont Blanc*, a "montanha branca". A inspiração vem da lenda do navegador
  e explorador que encontrou uma cidade de ouro, foi chamado de mentiroso e teve a verdade provada séculos depois por um
  sino dourado. A marca fala de **jornada** (a montanha), **chamado** (o sino que anuncia que algo valioso foi
  encontrado) e **palavra cumprida** (o ouro que provou a verdade; "palavra dada."). A identidade é original: não usa personagens,
  nomes nem elementos visuais de nenhuma obra.
- **Marca — maleta, sino e pico:** a **maleta** (o trabalho) com um **sino** pequeno no lugar da alça (o chamado da
  oportunidade) e, recortado no corpo, o **Pico**: o M de montanha que também é uma pessoa de braços erguidos (a vaga
  conquistada). Desenho chapado, em uma cor, legível em 16px.
  - Fonte única: `wwwroot/brand/monburan-mark.svg`; nas telas, o componente `BrandMark` (web e app). Os recortes são
    máscara (mostram o fundo de onde a marca estiver); a cor é o token `--brand-metal`: ouro #E0B252 no tema escuro,
    bronze #8A5500 no claro.
  - Fora de uso: perfis de personagem ou qualquer figura reconhecível de obra existente.
- **Assinatura:** marca + "MONBURAN" em Cinzel 600 caixa-alta espaçada + "palavra dada." em serifa itálica (`Brand`).
- **Ícones do produto:** `favicon.svg` e PNGs (16, 32, 180, 192, 512 e *maskable*) com a marca sobre a tinta
  #141008; imagem de compartilhamento `brand/og-image.png` (1200×630); no app,
  `Resources/AppIcon` (fundo #141008) e `Resources/Splash` (fundo #0A0806).
- **Carregamento:** só o sino balança; a maleta fica parada (`LoadingState`, telas de abertura e de
  reconexão). Com movimento reduzido fica parado. Não confundir com o ícone de notificações (sino de contorno).

## Arquivos e camadas (web)

| Arquivo | Camada | Conteúdo |
|---|---|---|
| `wwwroot/tokens.css` | `tokens` | declara a ordem das camadas; **única** fonte de cor, tipo, espaço, forma, movimento, alturas, contêineres e z-index; `@font-face` da Cinzel |
| `wwwroot/base.css` | `base` | reset, elementos nativos, tipografia, foco, campos, grade de 12 colunas, movimento reduzido |
| `wwwroot/components.css` | `components` | componentes do sistema (botão, campo, cartão, selo, aviso, tabela, paginação, chip, abas, cabeçalho, menu, etapas, estados, toast, diálogo, gaveta) |
| `wwwroot/shell.css` | `components` | marca, menu lateral, barra superior com trilha, cabeçalho público, rodapé, telas de entrada e da conta |
| `wwwroot/pages.css` | `components` | padrões de página (vagas, painéis, currículos, IA, administração, planos, CV) |
| `wwwroot/utilities.css` | `utilities` | utilitários com nome do Bootstrap (o Bootstrap **não** é carregado), sem `!important` |
| `Components/**/*.razor.css` | sem camada | estilo isolado, só com tokens |

Ordem: `tokens, base, components, utilities` — a camada posterior vence. ✔ Não existe camada `legacy`.
✔ `!important` só dentro de `prefers-reduced-motion`.

## Tokens

- **Cor** (definida em `:root`/`html[data-theme]`, mesmos nomes nos dois temas ✔):
  fundos `--bg`, `--bg-2`, `--bg-3`, `--bg-card`, `--bg-elev`, `--bg-input`; texto `--tx`, `--tx-2`, `--tx-muted`;
  bordas `--border` (decorativa, nunca o único limite de um controle), `--border-2`, `--border-input` (≥ 3:1 ✔);
  marca `--gold`, `--gold-d` (links e destaques), `--gold-l`, `--gold-soft`, `--gold-border`;
  ação primária `--action-bg`/`--action-fg`; foco `--focus-ring`.
- **Tons** (selo, aviso, barra): `--ok`/`--ok-bg`, `--er`/`--er-bg`/`--er-ink`, `--warn`/`--warn-bg`,
  `--info`/`--info-bg`/`--info-tx`, `--neutral-tx`/`--neutral-bg`, `--brand-tx`/`--brand-bg`. ✔ Texto ≥ 4,5:1 sobre o
  fundo do próprio tom, nos dois temas.
- **Tipo**: `--f-ui` (interface, fonte do sistema), `--f-display` (Cinzel 600, só identidade).
  Escala `--fs-2xs` 12px (só metadado) · `--fs-sm` 14 · `--fs-md` 16 (leitura e campos) · `--fs-lg` 18 · `--fs-xl` 22 ·
  `--fs-2xl` 28 (h1 do app) · `--fs-3xl` 36. Altura de linha `--lh-tight|snug|body`; peso `--fw-regular|semibold|bold`.
- **Espaço e forma**: `--s-1`…`--s-16` (base 4px); raio `--r-xs|sm|md|lg|pill`; sombra `--shadow-sm|md|lg`
  (nível 0 = borda, para cartões).
- **Controles**: `--control-h` 44px (alvo de toque), `--control-h-sm` 40px (44px com ponteiro grosso), `--control-h-lg` 48px.
- **Estrutura**: `--container` 1240px (listas), `--container-md` 1100px (formulário em duas colunas), `--container-narrow`
  760px (leitura e formulário simples), `--gutter` 16px → 24px a partir de 768px, `--topbar-h`, `--sidebar-w`, `--rail-w`.
- **Camadas**: `--z-raised` < `--z-dropdown` < `--z-sticky` < `--z-scrim` < `--z-sidebar` < `--z-sticky-cta` <
  `--z-drawer` < `--z-dialog` < `--z-toast` < `--z-skip-link`.
- **Breakpoints** ✔: 576, 768, 992, 1200px (`max-width` usa o valor − 0,02px). O menu lateral fixo vale a partir de 992px.
- **Tema**: claro e escuro; sem escolha salva, vale o tema do sistema (web). O app mantém a escolha do usuário.

✔ Toda `var(--x)` existe. ✔ Cor fixa só em `tokens.css` (exceção: a folha de CV `.cv-*`, papel neutro impresso).

## Componentes (Components/Shared)

| Componente | Contrato |
|---|---|
| `PageHeader` | Um por tela: h1 (recebe o foco ao navegar), descrição, `BackHref` (um nível acima), `Actions` com no máximo uma `btn-primary` e duas secundárias |
| `StatusBadge` | `Kind` + `Value` (candidatura, vaga, assinatura) ou `Text` + `Tone`. Texto sempre junto da cor |
| `EmptyState` | Vazio ou não encontrado; `Icon` (nome do Icon), título, mensagem e o próximo passo; `HeadingLevel="1"` quando é a página |
| `ErrorState` | Erro de carga (nunca disfarçado de vazio); "Tentar novamente" por `OnRetry` ou `RetryHref` (SSR) |
| `LoadingState`, `SkeletonBlock`, `SkeletonCard` | Primeira carga, com `role="status"` |
| `AccessRestricted` | Conta sem o perfil exigido; `Reason` diz para quem é a área; saídas "Voltar ao início" e "Entrar com outra conta" |
| `Pager` | Até 5 números + Anterior/Próxima; por callback ou por link (`HrefFor`); "Mostrando X–Y de N" com `Total`/`PageSize` |
| `ConfirmDialog` | Só para ação irreversível: `<dialog>` modal, título nomeia o item, foco abre em "Cancelar", Esc e clique fora cancelam, foco volta |
| `ToastHost` / `IToastService` | Sucesso some em 6s (pausa com ponteiro/foco); erro fica até ser fechado; anúncio único por região viva |
| `UnsavedChangesGuard` | Formulário longo: pergunta antes de sair com alterações |
| `Icon` | SVG em linha; decorativo por padrão, `Label` para nome acessível. Ícone sozinho só em botão com `aria-label` |
| `Brand`, `ThemeToggle` | Marca e alternância de tema do layout |

## Padrões de página e comportamento

- **Layout**: barra superior com trilha (`NavigationTrail`) e ações globais por papel (empresa: "Publicar vaga");
  menu lateral por papel com a identidade da conta (iniciais reais) no rodapé; abaixo de 992px o menu vira gaveta
  (foco preso, Esc fecha, foco volta ao botão).
- **Painel (início)**: próxima ação → números (cada um é link para a lista filtrada) → conteúdo recente com "Ver todas".
- **Listas e filtros**: estado na URL (voltar, compartilhar e recarregar reproduzem a lista). Selects e chips aplicam ao
  mudar com mouse/toque; pelo teclado, setas só escolhem e Enter aplica (WCAG 3.2.2). Busca por texto com Enter ou
  "Buscar". Sem botão "Filtrar". "Limpar filtros" só com filtro ativo. Ao refiltrar, a lista atual fica visível com
  `aria-busy`; a contagem nova é anunciada por região viva.
- **Tabelas**: `caption` (pode ser `visually-hidden`), `th scope="col"`, `.table-cards` com `data-label` (cartões abaixo de
  768px; a primeira célula é o título do cartão; ações em `data-label="Ações"`); botão de linha com nome contextual.
- **Formulários**: rótulo acima, dica abaixo do rótulo, erro no campo (`aria-invalid` + mensagem); "Campos com * são
  obrigatórios" quando houver `*`; foco no primeiro campo inválido; ocupado com `aria-busy` e "Salvando…".
  Assistentes validam **por etapa** antes de avançar.
- **Remoção**: reversível → "Desfazer"; irreversível → `ConfirmDialog` nomeando o item.
- **Responsivo**: ação principal da vaga fixa no rodapé abaixo de 992px; avisos acima dela.

## Botões

`btn-primary` (uma por área) · `btn-outline-secondary` (secundária) · `btn-ghost` (terciária) · `btn-outline-danger`
(destrutiva, sempre secundária) · `btn-link`. Tamanhos `btn-sm`, `btn-lg`; `btn-cta` = primária grande do funil de vagas.
✔ Sem `btn-success` (verde é estado, não ação). ✔ Sem glifo de texto como ícone.

## Textos (glossário)

Candidato → "Minhas candidaturas" (Candidaturas, Vagas salvas, Alertas); empresa → "Minhas vagas" e "Candidaturas
recebidas"; criar vaga = "Publicar vaga"; editar = "Salvar alterações"; excluir irreversível = "Excluir" + objeto;
remover vínculo = "Remover"; candidatura Rejected = "Não selecionada"; vaga reprovada = "Reprovada"; plano "Free" =
"Gratuito" (`UiLabels.PlanName`); "verificação em duas etapas" (nunca "2FA"); "E-mail". Títulos de aba:
"Página - Monburan", caixa de frase. Nada em caixa alta no conteúdo.

## Contribuição

1. Use um componente existente; se não houver, proponha no `DESIGN.md` antes de criar.
2. Estilo novo vai na camada `components` (`components.css`, `shell.css` ou `pages.css`), só com tokens.
3. Rode `dotnet test tests/Monburan.UnitTests` (guardas e contraste) e a captura
   (`tests/ux-smoke`: `dotnet run -- --capture all`) antes de abrir o PR; o resumo não pode ter overflow, contraste abaixo
   de AA, texto < 12px, campo sem rótulo ou controle sem nome.

## App (MAUI)

`src/Monburan.Mobile/wwwroot/app.css` replica os tokens da web (✔ mesmos valores, `MobileTokenParityTests`) e os
componentes equivalentes; ícones pelo `Components/Icon.razor` (mesmo conjunto da web). A pré-visualização no navegador
(`tests/ux-mobile-preview`) roda os mesmos componentes para revisão e captura; barras do sistema, WKWebView e gestos
exigem teste no aparelho.
