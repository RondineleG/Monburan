# DESIGN — identidade e tokens atuais (referência factual)

Documenta o que já existe em `src/Monburan.Web/wwwroot/ui.css` (e `app.css` para utilitários).
Não é um novo sistema — é o mapa do que usar antes de inventar classe.

## Tokens (CSS custom properties em `:root`)

- **Cor**: `--bg` (fundo da página), `--bg-elev` (superfície elevada), `--bg-card` (cartão),
  `--tx` (texto), `--tx-2`/`--tx-muted` (texto secundário), `--border`,`--border-2`,
  `--gold`/`--gold-d`/`--gold-soft`/`--gold-border` (acento dourado da marca — usado só para
  destaques de valor, como o painel de IA).
- **Tipografia**: `--f-display` (títulos/marca), `--f-ui` (interface), `--f-mono` (rótulos,
  contagens, dados técnicos); escala `--fs-xs` → `--fs-lg`.
- **Forma**: `--r-sm`, `--r-md`, `--r-lg`, `--r-pill`; sombras `--shadow-sm`+;
  `--dur-sm`/`--ease-out` para microtransições discretas.
- **Tema**: `html[data-theme="dark"]` redefine os mesmos tokens — componentes novos devem
  usar só variáveis, nunca cor fixa (exceção: a folha de CV `.cv-sheet`, papel neutro impresso).

## Padrões visuais vigentes

- Utilitários próprios compatíveis com nomes Bootstrap (`container`, `row`, `col-*`,
  `card`, `btn`, `badge`, `form-control`, `d-flex`, `gap-*`, `mt-*`, `table`) definidos
  em `app.css`/`ui.css` — o projeto não carrega o CSS do Bootstrap; classes BEM próprias:
  `journey-tabs`, `chip-clear`, `app-shell`/`app-stage`/`app-main`, `table-cards`,
  `job-actions`, `empty-state`, `ai-panel`, `ai-workspace`, `cv-sheet`.
- Tom confiante e discreto: hierarquia por tipografia e espaçamento; ações primárias
  óbvias, destrutivas secundárias (`btn-outline-danger btn-sm`); contagens em pills.
- Conteúdo denso por divulgação progressiva (`<details>`, gaveta, abas) em vez de cartões aninhados.
- Impressão só para o documento de CV: `@page A4 margin:0`, isolamento por
  `body:has(.cv-print-area)`, sem faixas do shell.
- Acessibilidade: rótulos visíveis nos filtros, `aria-current` em navegação ativa,
  alvos de toque ≥44px, foco visível.
