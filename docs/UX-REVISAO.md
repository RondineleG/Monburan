# Revisão de UX/UI e roteiro

Este documento registra a análise da interface do Monburan frente aos principais portais de vagas, as decisões
tomadas e o roteiro de melhorias. A web vem primeiro; o app (MAUI Blazor Hybrid) reproduz a mesma interface depois.

## O que foi analisado

Portais observados em outubro de 2026: Indeed, Catho, InfoJobs, Gupy, Vagas.com e LinkedIn Vagas. O que foi **visto
em tela** (capturas): página inicial do Indeed, resultados e página da vaga de Catho, InfoJobs, Gupy e Vagas.com, além
do cartão de criação de alerta do LinkedIn. A página de resultados do Indeed bloqueia acesso automatizado (HTTP 403);
sobre ela e sobre "Minhas vagas" do Indeed valem a página inicial, a central de ajuda e conhecimento geral, sem
captura.

Também foi auditada a interface atual da web (visitante, candidato e empresa), em desktop e celular, com dados
realistas e nos dois temas.

## O que os portais fazem e vale copiar

- **Busca primeiro.** A tela inicial é uma barra com dois campos ("cargo, palavra-chave" e "cidade, estado ou
  remoto") e um botão forte. Filtros aparecem depois, como atalhos.
- **Resultados que se explicam.** Contagem ("142 vagas"), ordenação (relevância ou mais recentes), filtros em
  "chips" (modalidade, salário, data, contrato, senioridade, PCD) e, em alguns, contadores por filtro.
- **Cartão rico e escaneável.** Logotipo, cargo em destaque, empresa, local, modalidade, salário, "publicada há X
  dias" e um trecho da descrição. O cartão inteiro é clicável.
- **Página da vaga pública.** Qualquer pessoa lê a vaga; o login só aparece ao se candidatar. Botão de candidatura
  sempre à vista (fixo no rodapé do celular), compartilhar por WhatsApp e link, prazo de inscrição, vagas
  semelhantes.
- **Lista e detalhe lado a lado** no desktop (Catho e InfoJobs, vistos em tela), página cheia no celular.
- **Acompanhamento.** "Minhas vagas" com abas (salvas, candidaturas, entrevistas, arquivadas) e alertas de vaga.

### O que evitar

Pop-ups de venda sobre a lista, anúncios no meio dos resultados, textos misturando idiomas e avisos de cookies que
cobrem a tela. O Monburan já não tem nenhum deles; a regra é não introduzir.

## Diagnóstico da interface antes da Fase 1

1. A vaga só abria em um **modal** dentro da lista e **exigia login para ler**; a página própria da vaga existia, mas
   nada apontava para ela e ela também redirecionava visitantes ao login. Sem URL compartilhável e sem indexação.
2. A busca tinha **seis campos** (salário mínimo e máximo, UF e cidade separados, tipo) em um painel grande: muito
   atrito para uma busca que deveria ser uma linha.
3. Os cartões eram altos e repetiam informação, com um botão "Ver detalhes" em cada um; ícones feitos de caracteres
   Unicode e rótulos em fonte mono minúscula (0,58 rem), difíceis de ler.
4. Sem contagem clara, sem ordenação, sem "limpar filtros", paginação sem URL (Voltar não restaurava a busca).
5. No celular, o painel de busca empurrava as vagas para fora da primeira tela.
6. Tabelas da área da empresa estouram no celular; o aviso "Para empresas" ocupava uma faixa inteira.
7. Navegação lateral recolhida mostra só ícones, sem texto.
8. Contraste baixo em textos secundários (`--tx-3`) e botões em caixa-alta mono em todo o produto.

## Princípios adotados

- **Estrutura e legibilidade antes de estética.** A identidade (escuro e dourado, títulos serifados) fica; o texto de
  leitura passa a usar a fonte de interface, e as fontes da marca ficam em títulos de página e rótulos curtos.
- **A URL é o estado.** Busca, filtros, ordenação e página vivem no endereço: Voltar, Avançar, recarregar e
  compartilhar reproduzem a mesma tela.
- **Páginas públicas renderizadas no servidor.** Mais rápido para o primeiro conteúdo, indexável por buscadores e sem
  depender do circuito interativo para ler uma vaga.
- **Controles nativos quando bastam.** Botões de opção, `select` e formulário GET dão teclado, leitor de tela e
  celular sem JavaScript pesado.
- **Um componente por ideia.** Cartão de vaga, ícones e texto formatado são componentes reaproveitáveis (o app os
  replica na fase 5).

## Roteiro

### Fase 1: funil de vagas na web (entregue neste PR)

- Busca em uma linha (cargo e local), chips de modalidade, piso salarial, ordenação e contagem; filtros e página na
  URL; "limpar filtros"; estado vazio útil.
- Cartão novo: logotipo (ou inicial), cargo, empresa, local, modalidade, salário, trecho da descrição, categoria e
  etiquetas, "publicada há X" e aviso de encerramento próximo. O cartão inteiro é o link para a vaga.
- **Página pública da vaga** (`/jobs/{id}`): lida sem login; candidatura leva ao login e volta ao formulário;
  compartilhar por WhatsApp e copiar link; barra de candidatura fixa no celular; vagas semelhantes; "Voltar para as
  vagas" preserva a busca; dados estruturados `JobPosting` e metadados sociais; 404 de verdade para vaga inexistente;
  vagas pausadas, ocultas e reprovadas só aparecem para a empresa dona.
- Ordenar por salário deixa as vagas sem faixa salarial por último (no PostgreSQL o `NULL` vem primeiro em ordem
  decrescente).
- Links antigos (`/jobs?jobId=`) redirecionam para a página da vaga.

### Fase 2: candidato

Painel com próximos passos, completude do perfil, candidatura com resumo e confirmação, acompanhamento por etapas.

### Fase 3: empresa

Minhas vagas com abas por situação e menu de ações, candidaturas com filtros e gaveta do candidato, formulário de
vaga em etapas com pré-visualização e categoria estruturada, tabelas que viram cartões no celular.

### Fase 4: base visual

Rótulos na navegação lateral, tokens de tipografia e contraste (substituir `--tx-3` em texto), esqueletos de
carregamento, revisão de acessibilidade (foco, rótulos, movimento reduzido).

### Fase 5: biblioteca compartilhada e app

Extrair os componentes e os tokens para uma biblioteca de componentes (RCL) usada por web e app, e reproduzir no app
MAUI a mesma interface: busca, cartão, página da vaga com barra fixa, filtros.

### Trilha de back-end (sem dependência de interface)

Vagas salvas, alertas de vaga, recomendações, histórico de etapas da candidatura, perfil de carreira, filtro por
"publicada em", período do salário e tipo de contratação (hoje o cadastro não guarda, e por isso não entram nos
dados estruturados), contagem real de visualizações (o contador existe, mas nada o incrementa), `robots.txt` e
`sitemap.xml` com as vagas ativas.

Pendência encontrada na Fase 1, anterior a ela: rotas que não existem (ex.: `/nao-existe`) respondem 404 com **corpo
vazio**, e o navegador mostra uma página em branco, porque o re-execute do `StatusCodePages` para `/404` não chega a
renderizar a página. A página da vaga já trata o próprio 404 (status 404 com a tela "Vaga não encontrada"); o
restante do site precisa de uma correção à parte.

## Decisões técnicas da Fase 1

- `Jobs.razor` e `JobDetails.razor` são páginas de renderização no servidor (sem modo interativo próprio). O
  formulário de busca é `GET` com `data-enhance`: o Blazor o trata como navegação melhorada, atualiza a URL e troca só
  o conteúdo. Campos vazios ou padrão saem do endereço (`monburan-ui.js`).
- Filtros enviam o formulário ao mudar (`data-autosubmit`); o `select` de ordenação fica fora do `<form>` e se liga a
  ele pelo atributo `form`.
- Ícones são SVG inline (`Icon.razor`), sem fonte de ícones nem pedido de rede.
- URLs absolutas (canônica, compartilhamento, dados estruturados) usam `App:PublicBaseUrl`, como os links de e-mail,
  porque o proxy TLS não repassa o esquema.
- Dados estruturados descrevem só o que a vaga informa: sem salário (falta o período) nem tipo de contratação.

## Como validar

```bash
ASPNETCORE_ENVIRONMENT=Development APPLY_MIGRATIONS=true dotnet run --project src/Monburan.Web --urls http://localhost:5195
E2E_BASE_URL=http://localhost:5195 dotnet test tests/Monburan.E2ETests
dotnet test tests/Monburan.UnitTests
```

Conferir à mão em 1440 px e 390 px, nos temas claro e escuro: `/jobs`, `/jobs?modo=Remote&ordem=salario`,
`/jobs/{id}` como visitante, candidato e empresa dona, e a barra de candidatura fixa no celular.

---

## Auditoria de UX — etapa de correções (esta revisão)

Escopo **implementado** nesta rodada (não é auditoria completa do site nem do navegador):

- **Candidatura sem adivinhação**: `/jobs/{id}` e `/apply/{id}` consultam a candidatura
  antes de oferecer ação. Já candidatado vê situação + "Acompanhar candidatura" (desktop e
  CTA fixo); falha de consulta vira "Verificar candidatura"/erro de tentar de novo — nunca
  formulário. Vagas salvas mostram "Acompanhar" quando já aplicadas e "Ver oportunidade" nos demais.
- **Currículo com permissão única**: `ResumeViewerService` autoriza dono ou a empresa que
  recebeu aquele currículo numa candidatura (consulta exata, sem teto de 200). Detalhes, CV e
  gaveta do candidato usam o mesmo caminho; sem permissão é sempre "não encontrado".
- **Sugestões de IA com rascunho no navegador**: resultado vira rascunho criptografado por
  candidato+currículo; revisão em área de largura total com seleção explícita, edição, prévia
  do documento e bloqueio de lacunas [X%]; aplicação só grava o selecionado, rechecando dono e
  conflito de snapshot. Consentimento por nova geração; revisar/aplicar rascunho não regenera.
- **CV compartilhado**: `ResumeDocument` serve a tela de CV e a prévia; impressão em folha A4
  isolada, sem elementos do app; removidos nome/link do PDF de origem da interface.
- **Listas e perfil**: "Minhas vagas" com contagens, filtros sempre visíveis, próximo passo por
  candidatura e link do currículo enviado; "Minhas vagas" da empresa com busca em memória,
  título/link e ações diretas; "Candidaturas recebidas" com rótulos de filtro visíveis, contagem
  e nome como botão para a gaveta; perfil do candidato agrupa contato/currículo/conta e
  preserva o `AddressId` (regressão corrigida).

Não inspecionado nesta rodada: área administrativa, app móvel, cadastro/login, pagamento e
páginas públicas fora do fluxo vaga↔candidatura↔currículo.


## Evidência de validação (esta rodada)

- Build: `dotnet build src/Monburan.Web/Monburan.Web.csproj --no-restore` → 0 erros/0 avisos
  (`/tmp/monburan-final-build.log`).
- Unitários focados (52): `dotnet test tests/Monburan.UnitTests --filter "ResumeImprovement|
  ApplicationLookup|ResumeViewer|UpdateResumeOwnership|PlaceholderTheory|LookupViewerExceptionSafety"`
  → 52/52 (`/tmp/monburan-final-unit.log`). Cobre rascunho criptografado real (DataProtection),
  corrida save/delete serializada, falhas/cancelamento dos serviços novos, placeholders.
- API (InMemory + repositório real, 4): `dotnet test tests/Monburan.ApiTests --filter
  "ResumeUxRepositoryTests"` → 4/4 (`/tmp/monburan-final-api.log`). Lookup exato, soft-delete,
  permissão por anexo exato e regressão acima de 200 candidaturas.
- Navegador real (Chromium headless, só loopback) via harness `tests/ux-smoke` contra host
  isolado `MonburanApiFactory` + `UseKestrel`: geração única (`fakeAi.Requests==1`), rascunho
  restaurado sem nova chamada, revisável com IA desligada, edição salva ao digitar, prévia com
  seleções, aplicação persiste só o marcado, rascunho removido; recrutador lê só o currículo
  anexado (leitura/impressão, sem editar/excluir/IDOR); perfil preserva `AddressId` em
  desfazer e salvar; vaga já candidatada mostra "Acompanhar" (desktop e 390px) e `/apply`
  bloqueia de cara; CV longo imprime 7 páginas A4 sem nav/chrome nem corte (linhas 001 e 120
  presentes). Screenshots e `cv.pdf`/`cv-long.pdf` em `/tmp/monburan-ux-host/`.

Escopo não coberto por esta evidência: área administrativa, app móvel, cadastro/login,
pagamento e páginas públicas fora do fluxo vaga↔candidatura↔currículo; comparação visual
pixel-a-pixel claro/escuro (capturas existem, sem baseline de referência).


## Evidência final (gate de aceite)

- Build: `dotnet build src/Monburan.Web --no-restore` → 0 erros/0 avisos (`/tmp/monburan-phase4-build.log`).
- Unit: `dotnet test tests/Monburan.UnitTests --no-restore` → **937/937** (`/tmp/monburan-final-unit-all.log`).
- API: `dotnet test tests/Monburan.ApiTests --filter "FullyQualifiedName~ResumeUxRepositoryTests"` → **4/4** (`/tmp/monburan-final-api2.log`).
- Browser real (repo `tests/ux-smoke`, asserts lançam exceção): todas as verificações passaram —
  geração única de IA, restauração de rascunho, revisão com IA desligada, persistência ao digitar
  (sem blur), edição restaurada, prévia com texto editado, aplicação preservando título, rascunho
  removido, overflow 390 ok, recrutador lê só o CV anexado (sem editar/excluir/IDOR), `AddressId`
  preservado no perfil, vaga já candidatada mostra "Acompanhar" (desktop+mobile) e `/apply` bloqueia,
  CV de 7 páginas sem chrome (`/tmp/ux-smoke-final.log`, host `http://127.0.0.1:36215`, artefatos
  `/tmp/monburan-ux-smoke-be2533be95dc418084e5d170becfee4e`).
- Capturas pós-redesign: `/tmp/monburan-visual-after/` (43 entradas no manifest — primeiro paint
  frio = claro+sidebar expandida, drawer mobile abre/foca/ESC-fecha/restaura-foco/inert,
  trilho recolhido NÃO expande com hover, `th` visível em desktop, scrollWidth ≤ viewport em
  320/768/390, 0 erros de JS, h1 real em todas as rotas). Comparações lado a lado (antes era dark
  apesar do rótulo 'light'): `/tmp/monburan-comparisons/cmp-{dashboard,profile,myjobs,received}.png`.
- Gate final: host `http://127.0.0.1:41133` (ux-smoke --serve) — todos os 33 checks `True`
  (`/tmp/ux-smoke-final2.log`); capturas pós-fix `/tmp/monburan-visual-after/` incluem dark real
  (4 shots, `actualTheme=dark` medido), mobile 390 sem overflow de tabela aninhada
  (`tableFitsViewport=True`), drawer abre/foca/ESC/inert/scrim verificados, rail sem overlay de
  hover, `th` visível em desktop, 0 erros JS. Comparações dark-vs-dark em `/tmp/monburan-comparisons/`.

## Rodada 6 — geradores sem consentimento cruzado + sidebar pública (ux-v7)

- **Sidebar fora do anônimo:** `MainLayout.razor` agora ramifica por `isAuthenticated`; páginas públicas
  renderizam `.public-header` (marca + Vagas/Entrar/Criar conta + tema, 44px, duas linhas ≤680px) e
  nenhuma `.app-sidebar`/scrim/hambúrguer — nem quando a preferência `rail` está persistida
  (`.app-shell--public .app-stage { margin-left: 0 !important }`).
- **Gerador de melhorias sem checkbox:** `ResumeAiPanel` trocou a caixa "Autorizo o processamento
  externo" por um aviso curto junto ao botão **"Gerar sugestões com IA"** (`aria-describedby`);
  o clique explícito É a autorização (`AcceptsExternalProcessing = true`, guarda do backend intacta).
  Regenerar confirma substituição do rascunho e já dispara a geração. "Revisar rascunho" →
  **"Revisar e aplicar"**.
- **Importação de PDF idem:** `UploadResume` removeu o checkbox marcado por padrão e o nome do
  provedor (`@AiTextClient.ProviderName`) da tela; agora são duas ações explícitas —
  **"Importar com IA"** / **"Importar sem IA"** (`UploadResumeAsync(bool useAi)`). IA indisponível →
  botão único "Importar currículo" (sempre leitura local).
- **Prompts menos prolixos:** `ResumeAiPrompt` proíbe marcadores de preenchimento (`[X%]`), habilidades
  deduzidas (só as citadas nas descrições) e reescritas sem evidência; `ResumeImportPrompt` devolve
  `summary` vazio em vez de inventar um e proíbe misturar atividades entre empresas.
- **Bug real corrigido:** `RegexMatchTimeoutException` no regex de URL (`CandidateValidator`/
  `AdvertiserValidator`) estourava os 250ms e abortava o salvar do perfil — substituído por
  `Uri.TryCreate` + esquema http/https + host com ponto (sem risco de ReDoS).
- **Política de privacidade** atualizada: autorização = clique explícito nos botões, por geração.
- Assets `ux-v7`, service worker `v11-workspace`.

**Verificação (ux-v7):** build 0/0; unit focado 41/41 (`ResumeAiPromptTests`, `ResumeImportTests`,
`ImproveResumeUseCaseTests`); ux-smoke **36/36** (`/tmp/ux-smoke-anon2.log`) — incl. `ANON_NO_SIDEBAR`,
`ANON_NO_SCRIM`, `ANON_NO_MENU_BTN`, `ANON_PUBLIC_HEADER`, `ANON_NO_GUTTER_1440`, `ANON_FITS_390`,
`NO_CONSENT_CHECKBOX`, `GENERATION_READY`, `NO_AUTOMATIC_AI`, `NAME_SAVED` (falhava pelo timeout do
regex, agora passa). Real `http://localhost:5095` capturado (`/tmp/monburan-anon-5095/`): anônimo
1440/390/320 e com preferência `rail` → `sidebar=0`, `publicHeader=1`, `scrollWidth=viewport`;
autenticado mantém `sidebar=1` + menu mobile; `/resumes/upload` com 2 botões e sem nome de provedor.
