# DESIGN — identidade, tokens e regras de uso

Mapa do sistema visual do site (`src/Monburan.Web`). Antes de criar classe ou cor nova, procure aqui.
O app móvel (`src/Monburan.Mobile`) ainda tem cópia própria dos tokens; ver "Pendências".

## Arquivos e camadas

| Arquivo | Camada | Conteúdo |
|---|---|---|
| `wwwroot/tokens.css` | `tokens` | **única** fonte de cor, tipo, espaço, forma, movimento, alturas de controle e z-index; declara a ordem das camadas |
| `wwwroot/app.css` | `base` | reset, grade de 12 colunas e utilitários com nomes do Bootstrap (o CSS do Bootstrap **não** é carregado) |
| `wwwroot/ui.css`, `wwwroot/jobs.css`, fim do `app.css` | `legacy` | componentes atuais, migrados aos poucos para `components` |
| `wwwroot/components.css` | `components` e `utilities` | componentes novos (só tokens, sem `!important`), ajustes de leitura e alto contraste; utilitários `.h3`–`.h6` |
| `Components/**/*.razor.css` | sem camada | estilo isolado de componente, sempre com tokens |

Ordem: `tokens, base, legacy, components, utilities` — a camada posterior vence. Regra nova de componente vai
em `components`; não use `!important` fora de utilitário.

## Tokens

- **Cor** (temas em `:root`/`html[data-theme]`): fundos `--bg`, `--bg-2`, `--bg-3`, `--bg-card`, `--bg-elev`,
  `--bg-input`; texto `--tx`, `--tx-2`, `--tx-muted`; bordas `--border` (decorativa), `--border-2`,
  `--border-input` (campos, ≥ 3:1); marca `--gold`, `--gold-d` (links e destaques), `--gold-l`, `--gold-soft`,
  `--gold-border`; ação primária `--action-bg`/`--action-fg`; estados `--ok`/`--ok-bg`, `--er`/`--er-bg`/`--er-ink`,
  `--warn`, `--info`/`--info-bg`/`--info-tx`; foco `--focus-ring`.
- **Tipo**: `--f-ui` (interface), `--f-display` (marca e títulos de destaque), `--f-serif` (texto de apoio do hero),
  `--f-mono` (rótulos curtos); escala `--fs-2xs` (12px, só metadado) → `--fs-3xl`.
- **Espaço e forma**: `--s-1`…`--s-16` (base 4px), `--r-xs`…`--r-pill`, sombras `--shadow-sm|md|lg`.
- **Controles**: `--control-h` (44px, alvo de toque), `--control-h-sm` (40px só com ponteiro fino), `--control-h-lg`.
- **Camadas**: `--z-dropdown` < `--z-sticky` (topbar) < `--z-scrim` < `--z-sidebar` < `--z-sticky-cta` <
  `--z-drawer` < `--z-dialog` < `--z-toast` < `--z-skip-link`.
- **Tema**: claro e escuro redefinem os mesmos tokens; sem escolha salva, vale o tema do sistema operacional.

## Regras (verificadas por teste)

- `DesignTokenContrastTests`: texto ≥ 4,5:1 e borda de campo/foco ≥ 3:1 nos dois temas, para cada par usado.
- `StylesheetGuardTests`: toda `var(--x)` existe; cores de tema só em `tokens.css`; `components.css` sem cor fixa e sem
  `!important`; nenhuma marcação depende do Bootstrap (`bi-*`, `glyphicon`, `data-bs-*`).
- `MobileTokenParityTests`: as cores do app (`Mobile/wwwroot/app.css`) têm os mesmos valores da web.
- Nunca cor fixa em componente (exceção: a folha de CV `.cv-sheet`, papel neutro impresso).

## Padrões de uso

- **Botões**: `btn-primary`/`btn-cta` (uma ação primária por área), `btn-outline-secondary` (secundária),
  `btn-outline-danger` (destrutiva, sempre secundária), `btn-link` (ação com aparência de link);
  tamanhos `btn-sm`/`btn-lg`. Botão só com ícone exige `aria-label`.
- **Foco**: anel de 2px em `--focus-ring`, nunca removido sem substituto equivalente (o cartão de vaga recebe o anel
  no cartão inteiro).
- **Estados**: `LoadingState`/`Skeleton*`, `EmptyState`, `ErrorState` (`HeadingLevel="1"` quando o estado é a página
  inteira); avisos pelo `ToastService` (erro fica até ser fechado; pausa com ponteiro/foco); confirmação destrutiva pelo
  `ConfirmDialog` (`<dialog>` modal; foco volta para quem abriu).
- **Formulários longos**: `UnsavedChangesGuard` pergunta antes de sair com alterações; erro de envio leva o foco ao
  primeiro campo inválido (`monburan-ui.js`); erros de etapa entram no `EditContext` (aria-invalid).
- **Remoção reversível** (ex.: vaga salva): "Desfazer" em vez de confirmação. **Irreversível**: confirmação nomeando o item.
- **Tabelas**: `caption` (pode ser `visually-hidden`), `th scope="col"`, `.table-cards` com `data-label` em cada célula
  (cartões no celular), botões de linha com nome contextual ("Excluir a empresa X").
- **Filtros**: mouse/toque aplicam ao mudar; pelo teclado, setas só escolhem e Enter aplica (WCAG 3.2.2).
- **Glossário**: candidato → "Minhas candidaturas" (Candidaturas, Vagas salvas, Alertas); empresa → "Minhas vagas" e
  "Candidaturas recebidas"; situação Rejected de candidatura → "Não selecionada"; vaga reprovada na moderação →
  "Reprovada"; plano "Free" → "Gratuito" (`UiLabels.PlanName`). Títulos de aba: "Página - Monburan", caixa de frase.
- **Conteúdo denso** por divulgação progressiva (`<details>`, gaveta, abas).
- **Impressão** só para o CV: `@page A4`, isolamento por `body:has(.cv-print-area)`.

## Pendências

- App móvel: os valores de cor já são os da web (garantido por teste), mas o arquivo ainda é uma cópia; gerar a partir
  de uma fonte única exigiria mudança no build do MAUI.
- Fontes ainda vêm do Google Fonts; hospedar localmente exige decisão (privacidade × manutenção).
- Breakpoints: ainda há valores próximos misturados (767/767.98, 980/991.98) nas folhas `legacy`.
