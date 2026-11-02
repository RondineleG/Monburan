# Relatório da auditoria de produção — Monburan

> Auditoria completa de web (Blazor Server + API REST) e mobile (MAUI Blazor Hybrid, escopo candidato),
> conduzida a partir de `PROMPT-AUDITORIA-PRODUCAO.md`. Base: `fa62646`. Data: 2026-10-17.
> Inventário de partida: [`AUDIT-MATRIX.md`](AUDIT-MATRIX.md).

| Fase | Commit | Conteúdo |
|---|---|---|
| 0 | `f1ce1d9` | Matriz UI ↔ API |
| 1 | `d9eceba` | Correções funcionais, segurança, autorização, pt-BR, dados |
| 2 | `395796a` | Camada de UX compartilhada na web |
| 3 | `5d78d10` | Mobile: feedback padronizado e contratos protegidos |
| 4 | `c39c67e` | Backend: ProblemDetails, upload, notificações, desempenho |
| 5 | `b4fd64a` | Correções achadas na verificação (currículo com seções, habilidades, busca de vagas, títulos) |
| 5 | (este commit) | Testes novos + este relatório |

---

## 1. Resumo executivo

O sistema tinha uma base sólida (API com ProblemDetails no `ResultMapper`, JWT com `token_use`, cliente mobile com
refresh), mas **não estava pronto para produção**. Os problemas mais graves:

- **Segurança:** qualquer usuário logado lia e **excluía currículos de terceiros** pelo id (IDOR em `/resumes/{id}`);
  `/my-jobs` mostrava as vagas de todos os anunciantes a quem não era anunciante; não havia autorização por papel.
- **Dados que somem:** sete listagens (candidaturas, currículos, notificações, assinatura ativa) liam as primeiras
  100/1000 linhas **da tabela inteira** e filtravam em memória; o admin filtrava depois de paginar.
- **Telas que quebram:** datas vindas de formulário derrubavam o save em colunas `timestamptz` (currículo com
  experiências); o país "Brasil" numa coluna `char(5)` derrubava o cadastro de endereço.
- **Produção incompleta:** os planos de assinatura só existiam no seed de Development — em produção não havia planos.
  Notificações in-app nunca eram criadas para candidatos. "Excluir currículo" não excluía. Trocar o e-mail da conta
  desligava o perfil.
- **UX:** nenhuma camada de toast/confirmação; validações de domínio voltavam com mensagem vazia (o usuário clicava e
  nada acontecia); exceções cruas na tela; textos em inglês (Identity, Manage, validações, títulos da API, e-mails).
- **Promessas sem backend:** boost, destaque e analytics por plano, "Currículo Pro", "Ver perfil da empresa".

### Achados por severidade

Linhas da matriz (Fase 0) + achados descobertos durante a implementação e a verificação (§ 3.9).

| Severidade | Matriz | Adicionais | Total | Corrigidos/implementados | Escondidos | Documentados como lacuna |
|---|---|---|---|---|---|---|
| ❌ P0 | 48 | 10 | 58 | 58 | — | — |
| 🙈 Sem backend | 4 | — | 4 | — | 4 | 4 (decisão de produto) |
| ⚠️ P1 | 40 | 7 | 47 | 47 | — | — |
| ⚠️ P2 / lacuna | 6 | 2 | 8 | 5 | — | 3 (cobrança dos planos, currículo-base automático, render interativo das páginas legais) |
| ✅ ok | 48 | — | 48 | — | — | — |

**Critério de pronto:** build verde ✅ · testes verdes ✅ · zero elemento de UI sem backend ✅ · feedback por
toast/modal nas ações ✅ · fluxos críticos validados rodando ✅ (detalhes na § 6).

---

## 2. Matriz de auditoria

A matriz completa, com cada ação de UI → UseCase/endpoint → status, está em [`AUDIT-MATRIX.md`](AUDIT-MATRIX.md)
(inventário **antes** das correções). O destino de cada linha está na § 3.

---

## 3. Correções — arquivo → problema → correção → status

### 3.1 Segurança e autorização

| Arquivo | Problema | Correção | Status |
|---|---|---|---|
| `Pages/ResumeDetails.razor` | Qualquer usuário logado lia e excluía qualquer currículo pelo id (IDOR) | Dono gerencia; anunciante só lê currículo anexado a candidatura de vaga dele; demais veem "não encontrado" | ✅ |
| `Program.cs` + 14 páginas | Só `[Authorize]`; candidato abria telas de empresa e vice-versa | Políticas `CandidateOnly`/`AdvertiserOnly` (claim `user_type`) aplicadas às páginas por papel | ✅ |
| `Pages/MyJobs.razor` | Sem perfil de anunciante, o filtro ficava nulo e listava as vagas de todos | Política + guarda explícita | ✅ |
| `Routes.razor`, `Account/Pages/AccessDenied.razor` | Usuário logado sem permissão caía em ciclo de login | Mensagem "Esta área é restrita" com saída para o início | ✅ |
| `Layout/MainLayout.razor` | Páginas `/admin/*` sem entrada de menu | Seção "Administração" para o papel Admin | ✅ |
| `Services/CurrentUserHelper.cs`, `CircuitUserAccessor.cs` | Usuário lido do `HttpContext` dentro de componentes interativos (desaconselhado, frágil fora de WebSockets) | Usuário do circuito via `CircuitHandler` + `AuthenticationStateProvider`; HTTP continua usando o `HttpContext` | ✅ |
| `Core/UseCases/Storage/UploadFileUseCase.cs` | Tipo do arquivo vinha só do content-type do cliente; páginas gravavam direto no storage | Assinatura (magic bytes) validada; tipo detectado prevalece; páginas passam pelo UseCase | ✅ |
| `Endpoints/JobEndpoints.cs` | API pública expunha o salário mesmo com `SalaryDisplay = false` (o app exibia) | Salário oculto mascarado nos endpoints públicos; dono continua vendo em `/jobs/mine` | ✅ |

### 3.2 Dados e consistência do backend

| Arquivo | Problema | Correção | Status |
|---|---|---|---|
| `List{ApplicationsByCandidate,ApplicationsByJob,ResumesByCandidate,NotificationsByUser}UseCase`, `GetAvailablePlans`, `UpgradePlan`, `CheckJobLimit` | Liam 100/1000 linhas da tabela inteira e filtravam em memória — dados sumiam com o crescimento | Repositórios específicos (`IApplicationRepository`, `IResumeRepository`, `INotificationRepository`, `ISubscriptionRepository`) com consulta filtrada no banco e `AsNoTracking` | ✅ |
| `List{Candidates,Advertisers}UseCase` | Admin filtrava depois de paginar; `TotalCount` = tamanho da página (paginação nunca passava da 1ª) | `SearchAsync` com filtro no banco e total real (`ICandidateRepository`, `IAdvertiserRepository`) | ✅ |
| `Repositories/JobRepository.cs` | "Minhas vagas" só listava vagas ativas — pausadas/expiradas sumiam da gestão | Visão do dono traz todas as situações; pública continua só ativas | ✅ |
| `Data/ApplicationDbContext.cs`, `Core/Converters/UtcDateTimeConverter.cs` | `DateTime` com `Kind=Unspecified` (formulários) derrubava o save em `timestamptz` | Conversor global UTC (sem mudança de esquema) | ✅ |
| `Migrations/…SeedPlansAndSyncEmailVerified.cs`, `Data/SeedData.cs` | Planos só existiam no seed de Development | Migration idempotente cria Free/Premium/Enterprise; seed reaproveita por nome | ✅ |
| idem + `Account/Pages/ConfirmEmail.razor` | `IsEmailVerified` do perfil nunca era atualizado (site e app sempre "não verificado") | Sincronizado na confirmação + backfill na migration | ✅ |
| `Account/Pages/ConfirmEmailChange.razor`, `Services/AccountProfileService.cs` | Trocar o e-mail da conta desligava o perfil (vínculo é o e-mail) | Perfil acompanha a troca; bloqueia e-mail já usado por outro perfil | ✅ |
| `Account/Pages/Manage/DeletePersonalData.razor` | Excluir conta mantinha nome/telefone/currículos no perfil (LGPD) e bloqueava o e-mail | Perfil anonimizado e desativado; currículos removidos; vagas da empresa removidas | ✅ |
| `Core/UseCases/Jobs/CreateJobUseCase.cs`, `CheckJobLimitUseCase.cs` | Limite de vagas do plano nunca era aplicado | Aplicado ao publicar (e ao reativar); catálogo ausente não bloqueia | ✅ |
| `Core/UseCases/Subscriptions/UpgradePlanUseCase.cs` | Podia "mudar" para o plano atual; deixava duas assinaturas ativas acima de 100 registros | Consulta direta da ativa; mesmo plano vira erro de domínio | ✅ |
| `Core/UseCases/Resumes/DeleteResumeUseCase.cs` | "Excluir" só marcava `IsActive = false`: o currículo continuava na lista | Exclusão lógica real (`IsDeleted`) — achado pelos testes de contrato | ✅ |
| `Repositories/ResumeRepository.cs` (`UpdateAsync`) | Editar currículo com formação, experiência ou idiomas falhava: `Update()` de grafo desanexado com coleções owned lançava "shadow key property 'Education.Id' is unknown" | Mudanças aplicadas na versão rastreada; habilidades mescladas sem colidir a chave `(ResumeId, Skill)` | ✅ |
| `Repositories/ResumeRepository.cs` (`GetByIdAsync`) | Habilidades nunca apareciam no detalhe nem na edição (faltava `Include`) | `Include` das habilidades; leitura com `AsNoTracking` | ✅ |
| `Core/UseCases/Notifications/NotifyOn*` | Nenhuma notificação in-app para candidatos nem de nova candidatura; status no e-mail em inglês | Notificação in-app para empresa (nova candidatura) e candidato (mudança de status), independente do e-mail; status traduzido | ✅ |
| `CreateApplicationUseCase`, `UpdateApplicationStatusUseCase` | Falha de notificação engolida (`catch (Exception) { }`) | Registrada em log | ✅ |
| `Repositories/AdvertiserRepository.cs` | CPF/CNPJ duplicado = exceção crua | Erro de campo "Este CPF/CNPJ já está cadastrado" | ✅ |
| `UseCases/Analytics/GetDashboardAnalyticsUseCase.cs` | Carregava todas as candidaturas com `Include` para contar | Agregações no banco; só as 6 recentes são materializadas | ✅ |
| `Endpoints/ApiProblemDetails.cs`, `Program.cs` | Exceção em `/api` devolvia a página HTML de erro; 401 era `{ "error": "Unauthorized" }`; títulos do framework em inglês | `AddProblemDetails` + exception handler e status pages só na API; 401/403 em ProblemDetails; títulos pt-BR; 5xx sem detalhe técnico | ✅ |
| `Endpoints/{Auth,Candidate,File}Endpoints.cs` | `ValidationProblem` com título "One or more validation errors occurred." (exibido pelo app) | Título "Erro de validação" | ✅ |
| `Endpoints/CandidateEndpoints.cs`, `UpdateAdvertiserLogoUseCase.cs` | Upload de logo dependia de o restante do perfil ser válido | UseCase dedicado só para a logo | ✅ |

### 3.3 Fluxos core implementados

| Fluxo | Antes | Agora |
|---|---|---|
| Upload de logo (web) | Arquivo salvo, mas `LogoUrl` nunca atualizada; "Voltar" ia para 404 | Persiste no perfil; página ligada ao perfil da empresa |
| Candidatura (web) | Texto prometia "currículo ativo enviado automaticamente" — nada era anexado | Escolha do currículo (ativo pré-selecionado), igual ao app |
| Candidaturas recebidas | Recrutador não via a mensagem nem o currículo do candidato | Mensagem expansível + link "Ver currículo enviado" (leitura autorizada) |
| Gestão de vagas | Sem excluir na web; vaga pausada/expirada sem volta | Excluir (com confirmação), pausar e reativar (`ChangeJobStatusUseCase`, respeita validade e limite) |
| Notificações | Telas sempre vazias para candidatos | Avisos de nova candidatura e de mudança de status; "marcar todas" em uma operação (web e app, `PUT /api/v1/notifications/mine/read-all`) |

### 3.4 Camada de UX compartilhada (web)

| Componente/serviço | Uso |
|---|---|
| `IToastService` + `ToastHost` (ilha interativa no layout) | Sucesso/erro/aviso/info, auto-dismiss (5 s / 8 s erro), empilha até 4, `aria-live`; **sobrevive à navegação** (validado: "Vaga publicada" aparece na página seguinte) |
| `ConfirmDialog` + `ConfirmOptions` | Toda ação destrutiva: excluir vaga/currículo/endereço/candidato/empresa, rejeitar candidatura, mudar plano, cancelar assinatura (com campo de motivo). Foco na opção segura; Esc fecha |
| `LoadingState`, `EmptyState`, `ErrorState` | Todas as páginas que carregam dados; erro sempre com "Tentar novamente" |
| `ServerValidationErrors` + `ResultMessages` | Erros de validação dos UseCases aparecem no campo; regra de negócio/conflito com mensagem de domínio; erro interno com texto genérico (detalhe só no log) |
| `UiLabels` | Status em pt-BR (vaga, candidatura, assinatura, conta), moeda da vaga e datas no horário de Brasília |
| Formulários | Botões desabilitados + guarda contra duplo envio; `InputNumber`/`InputDate` com mensagens pt-BR; endereço com regras espelhando as colunas |

Páginas revisadas: Dashboard, Jobs (paginação com janela, busca pelo Enter, erro ≠ "nenhuma vaga", resposta atrasada
não sobrescreve a busca mais nova), JobDetails (ações do
dono, vaga encerrada), ApplyJob, MyApplications, MyJobs, ReceivedApplications, JobCreate (aviso antecipado de limite),
JobEdit, Plans, MySubscription, Candidate/AdvertiserProfile, UploadLogo, UploadResume, ResumeList/Create/Edit/Details,
Notifications, admin (Candidatos/Empresas), endereço (List/Create/Edit/Delete), conta (Manage/*).

### 3.5 Mobile (Fase 3)

| Arquivo | Problema | Correção | Status |
|---|---|---|---|
| `Mobile.Shared/Ui/ToastService.cs`, `Components/ToastHost.razor`, `ConfirmDialog.razor` | Sem toast/confirmação | Toasts e confirmação em bottom sheet | ✅ |
| `Pages/Resumes.razor` | Excluía sem confirmação | Confirmação + toast | ✅ |
| `Layout/MainLayout.razor` | Refresh rejeitado deixava a tela em "Sessão expirada" + retry em loop | `SessionExpired` → aviso + login | ✅ |
| `Mobile.Shared/Api/MonburanApiClient.cs` | Resposta não-JSON (HTML de proxy) virava `JsonException` fora dos `catch (ApiException)` → crash | Vira `ApiException` tratável; validação vira a mensagem principal; `FieldError(campo)` | ✅ |
| `Pages/Apply.razor` | Falha ao carregar → tela em branco | Erro com retry; vaga encerrada; currículo ativo pré-selecionado; erros por campo | ✅ |
| `Pages/Jobs.razor`, `JobDetails.razor` | Salário oculto exibido; moeda fixa "R$"; erro no "carregar mais" apagava a lista | `Formatters.Salary` respeita `SalaryDisplay` e moeda; toast no erro; detalhe mostra "já candidatado"/encerrada | ✅ |
| `Pages/Jobs.razor` | Buscar ou filtrar com a carga anterior em andamento somava as duas respostas (cards duplicados e fora do filtro) — provável em rede móvel lenta | Só a busca mais recente preenche a lista; "carregar mais" é descartado se a busca mudou | ✅ |
| `Pages/Login/Register/ForgotPassword/Profile.razor` | Sem envio pelo teclado, labels sem `for`, erros sem campo | `<form>` + submit, labels associadas, erros por campo, regra de senha explícita | ✅ |
| `Pages/Notifications.razor` | Falha ao marcar como lido silenciosa; horário UTC | Toast de erro; "marcar todos"; fuso do aparelho | ✅ |
| `Services/SecureStorageTokenStore.cs` | `catch (Exception)` sem log | Log de aviso mantendo o fallback | ✅ |
| `Monburan.Mobile.csproj` | — | `dotnet build -f net10.0-android`: **0 warnings, 0 erros** | ✅ |

### 3.6 pt-BR

| Arquivo | Problema | Correção |
|---|---|---|
| `Services/PortugueseIdentityErrorDescriber.cs` | Erros do Identity em inglês no cadastro (site e app) | Descritor pt-BR registrado |
| `Account/Pages/**` (14 arquivos) | "Disable 2FA", "Reset recovery codes", "Delete", validações "The X field is required." | Traduzidos; DataAnnotations com `ErrorMessage` |
| `Account/Pages/Manage/EnableAuthenticator.razor` | Instrução de desenvolvimento ("enable QR code generation") e emissor "Microsoft.AspNetCore.Identity.UI" | Removida; link `otpauth://` para abrir no celular; emissor "Monburan" |
| `NotifyOnApplicationStatusChangedUseCase.cs` | E-mail "Novo Status: Selected" | Status traduzido |
| Páginas e app | Status crus (`Active`, `Archived`, `Cancelled`…) | `UiLabels` / `Formatters` |

### 3.7 Navegação

| Item | Correção |
|---|---|
| `JobDetails` "Ver perfil da empresa" (botão sem ação) | Removido |
| Admin "Ver detalhes" → `/admin/candidates/{id}` (404) | Removido |
| `UploadLogo` "Voltar" → `/advertiser/profile` (404) | `/profile/advertiser` |
| Páginas órfãs: endereço, segurança da conta, upload de logo, admin | Ligadas aos perfis e ao menu |
| Título da barra superior em `/jobs/{id}/applications` e edição | Corrigido |

### 3.8 Removido do código

| Item | Motivo |
|---|---|
| `Components/Address/Address.razor` | Placeholder sem uso ("&lt;h3&gt;Address&lt;/h3&gt;") que ainda colidia com o nome da entidade |
| Modais ad-hoc em ResumeDetails, MySubscription, CandidatesList, AdvertisersList | Substituídos pelo `ConfirmDialog` |

### 3.9 Achados adicionais (fora da matriz inicial)

| # | Achado | Severidade | Como foi encontrado |
|---|---|---|---|
| A1 | `DateTime` `Unspecified` derrubava saves em `timestamptz` | P0 | Leitura do mapeamento ao revisar datas |
| A2 | Planos inexistentes em produção | P0 | Revisão do enforcement de limite |
| A3 | Troca de e-mail desligava conta ↔ perfil | P0 | Revisão de `ConfirmEmailChange` |
| A4 | Notificações in-app nunca criadas para candidatos | P0 | Ao escrever o teste de contrato de avisos |
| A5 | "Excluir currículo" não excluía | P0 | Teste de contrato `Resumes_CreateUploadAndDelete` falhou |
| A6 | País "Brasil" em `char(5)` derrubava o endereço | P0 | Revisão do formulário |
| A7 | `ValidationProblem` da API com título em inglês | P0 | Revisão das mensagens do app |
| A8 | `InputNumber`/`InputDate` com mensagem padrão em inglês | P0 | Revisão dos formulários |
| A9 | Status em inglês no e-mail ao candidato | P0 | Revisão dos notificadores |
| A10 | Filtros do admin com situações inexistentes ("Inativo", "Suspenso") | P1 | Reescrita do admin |
| A11 | Instrução de dev na tela de 2FA | P1 | Tradução do Manage |
| A12 | Placeholder `Address.razor` | P2 | Conflito de nome ao compilar |
| A13 | Vaga expirada/pausada sem como reativar | P1 | Revisão de MyJobs |
| A14 | Título errado na barra superior | P2 | Prints da verificação |
| A15 | Excluir conta lançava exceção em falha (página de erro) | P1 | Revisão do Manage |
| A16 | Gerar códigos de recuperação sem 2FA lançava exceção | P1 | Revisão do Manage |
| A17 | Editar currículo com formação/experiência/idiomas falhava (chave sombra das coleções owned) | P0 | Verificação contra PostgreSQL real (salvar currículo com seções) |
| A18 | Habilidades do currículo nunca apareciam no detalhe/edição | P1 | Mesma verificação (resposta sem `skills`) |
| A19 | Busca/filtro de vagas durante o carregamento: app somava respostas, web podia exibir resultado antigo | P1 | Investigação de um passo instável no fluxo do app no emulador |

---

## 4. Escondido ou removido da UI (e por quê)

| Elemento | Onde | Por quê |
|---|---|---|
| "Boost de vagas", "Destaque", "Analytics" por plano | Planos, Minha assinatura | Flags existem no catálogo mas **nenhuma regra as usa** (todo anunciante já tem o painel). Os planos agora anunciam só o que é aplicado: limite de vagas ativas, candidaturas ilimitadas, painel |
| "N currículos" por plano | Planos, Minha assinatura | `MaxResumes` não é usado (e anunciante não cria currículo) |
| Card "Currículo Pro" (R$ 19,90/ano, assistente, importação, exportação) | Planos | Produto inexistente; "Ativar" só levava a `/resumes` |
| Faixa "Recrute com destaque / boost / analytics" | Dashboard | Promessa sem backend e exibida a candidatos; agora só para anunciantes, falando de vagas ativas |
| "Destaque vagas para milhares…" | Vagas | Idem; reescrita sem prometer destaque e oculta para candidatos |
| "Ver perfil da empresa" | Detalhe da vaga | Não existe página pública de empresa |
| "Ver detalhes" do candidato | Admin | Rota inexistente |
| Instrução "enable QR code generation" | 2FA | Texto de template de desenvolvimento |

---

## 5. Lacunas remanescentes — decisões de produto (não técnicas)

1. **Cobrança dos planos pagos.** Não há gateway: "Fazer upgrade" ativa Premium/Enterprise na hora, sem pagamento
   (fora de escopo pelo prompt). Decidir: integrar gateway, ativação manual pelo admin ou ocultar planos pagos.
2. **Boost/destaque/analytics por plano.** Ou implementar (ordenação/selos/painel restrito) ou remover do catálogo.
3. **Limite de vagas passa a valer.** Quem já tem mais vagas ativas que o limite mantém as atuais, mas não publica
   novas nem reativa até ficar abaixo — vale comunicar aos anunciantes.
4. **Página pública de empresa** e **detalhe de candidato no admin**: hoje não existem; decidir se entram no roadmap.
5. **PDF importado não é um campo do currículo.** A URL fica no resumo (comportamento herdado); decidir modelar
   `FileUrl` para exibir/baixar o PDF ao recrutador.
6. **Retenção na exclusão de conta (LGPD).** Perfil anonimizado, currículos e vagas removidos; candidaturas ficam
   anônimas no histórico da empresa — validar com o jurídico.
7. **Texto da notificação de rejeição** ("não seguiu no processo") — validar tom com o produto.
8. Fora de escopo (já aceitos): SignalR/tempo real, push, cache offline, iOS, publicação nas lojas.

---

## 6. Verificação

### 6.1 Build

| Comando | Resultado |
|---|---|
| `dotnet build Monburan.sln` | ✅ 0 erros, 0 warnings (inclui Android) |
| `dotnet build src/Monburan.Mobile -f net10.0-android` | ✅ 0 erros, 0 warnings |

### 6.2 Testes

| Projeto | Antes | Depois | Novos |
|---|---|---|---|
| Monburan.UnitTests | 79 ✅ | **119 ✅** | Toast (web/app), ResultMessages, Formatters, UploadFile (assinaturas), CheckJobLimit, ChangeJobStatus, limite no CreateJob |
| Monburan.IntegrationTests | 7 ✅ | **11 ✅** | `AccountProfileService`: e-mail verificado sem diferenciar maiúsculas, troca de e-mail mantém o vínculo conta ↔ perfil, conflito de e-mail com outro perfil, anonimização na exclusão de conta |
| Monburan.ApiTests | 38 ✅ | **58 ✅** | 9 de contrato do `Mobile.Shared` contra a API real (login, vagas, candidatura + 409, currículos + PDF, perfil, avisos, refresh transparente, refresh rejeitado, senha fraca pt-BR, resposta não-JSON) e 11 de regressão (ProblemDetails pt-BR em rota inexistente/401/validação, salário oculto, limite do plano, vagas pausadas do dono, upload forjado, logo grava no perfil, currículo com seções editado duas vezes, escopo por candidato, marcar todas) |
| Monburan.E2ETests (Playwright, Postgres real) | não executada na base | **55 ✅** | — |

> E2E: em 6 execuções completas (as duas últimas depois das correções da verificação) houve **1 falha intermitente**
> em `FullSweepTests.Sweep_Advertiser_AllRoutes` (passou isolado e nas outras 5; a mensagem não foi capturada).
> Registrado para acompanhamento.
>
> `ResumeUpdate_WithSections_CanBeSavedAndEditedAgain` é regressão de verdade para A17: rodado contra o
> `ResumeRepository` anterior, falha com `500` e "The value of shadow key property 'Education.Id' is unknown". A
> correção também foi validada contra PostgreSQL: criar → salvar com formação, experiência, idiomas e habilidades →
> salvar de novo alterando tudo → `GET` com as seções e habilidades esperadas (C# atualizado, SQL removido, Azure
> incluído).

### 6.3 Fluxos críticos (rodando, não só lendo código)

Ambiente: app em Development contra PostgreSQL 16 real (container local), navegador Chromium headless via Playwright;
app Android no emulador (Android 16) conectado à mesma API.

**Candidato (web)** — `register → login → listar vagas → detalhe → candidatar → Minhas candidaturas → upload de currículo → perfil → notificações`

| Passo | Resultado | Evidência |
|---|---|---|
| Cadastro + login | ✅ | menu de candidato visível após o cadastro |
| Listar vagas (busca pelo Enter) | ✅ | vaga criada pelo anunciante encontrada |
| Detalhe da vaga | ✅ | título confere em `/jobs/{id}` |
| Candidatar | ✅ | "Candidatura realizada com sucesso!" + toast "Candidatura enviada" |
| Minhas candidaturas | ✅ | linha "Analista de Qualidade … · Enviada · 17/10/2026 02:14" (horário de Brasília) |
| Upload de currículo (PDF) | ✅ | toast "Currículo enviado e adicionado à sua lista!" |
| Upload forjado (HTML como PDF) | ✅ recusado | toast "O arquivo não é um PDF válido." |
| Perfil | ✅ | toast "Perfil salvo" |
| Notificações | ✅ | "Você foi selecionado(a)!" após a empresa selecionar; "marcar todas" com toast |
| Acesso à área de empresa | ✅ bloqueado | "Esta área é restrita" |

**Anunciante (web)** — `login → criar vaga → Minhas vagas → candidaturas recebidas → mudar status → planos/assinatura`

| Passo | Resultado | Evidência |
|---|---|---|
| Login | ✅ | — |
| Criar vaga | ✅ | abre a vaga publicada + toast "Vaga publicada" (visível após a navegação) |
| Minhas vagas | ✅ | linha da vaga; pausar/reativar com toast; excluir abre confirmação e "Cancelar" não exclui |
| Validação de domínio | ✅ | "Data de expiração deve ser futura." no campo + toast |
| Candidaturas recebidas | ✅ | candidato com "Mensagem do candidato" |
| Mudar status | ✅ | "Selecionada" + toast; candidato notificado |
| Planos → mudança | ✅ | confirmação → `/my-subscription` + toast "Plano atualizado"; sem textos em inglês |

> Re-executados no build final: roteiro web **23/23 ✅** com **0 erros de console**; checagens de UX (pausar/reativar
> com toast, confirmação de exclusão e "Cancelar", erro de domínio no campo, toast após navegar) **9/9 ✅**.

**Mobile (Android 16, emulador)** — `cadastro → logout → login → vagas → detalhe → candidatar → minhas candidaturas → perfil → currículos → avisos → logout`

App instalado do build atual, sessão limpa (`pm clear`) e conduzido pelo WebView via Playwright (CDP). A vaga foi
publicada pela API como empresa, com salário oculto; a seleção do candidato também foi feita pela API como empresa.
Resultado: **19/19 ✅**.

| Passo | Resultado | Evidência |
|---|---|---|
| Cadastro com senha fraca | ✅ | "A senha deve ter pelo menos um número (0-9). A senha deve ter pelo menos uma letra maiúscula (A-Z)." |
| Cadastro + login automático | ✅ | navegação inferior visível |
| Logout → login | ✅ | "Sair da conta" volta ao login; entra com a conta criada |
| Buscar vagas | ✅ | 1 resultado para o título da vaga publicada |
| Salário oculto pela empresa | ✅ | card sem salário |
| Detalhe da vaga | ✅ | título confere |
| Candidatar | ✅ | tela "Candidatura enviada" + toast |
| Minhas candidaturas | ✅ | "Vaga App … · Enviada · Enviada em 17/10/2026" |
| Detalhe depois de candidatar | ✅ | aviso de que já se candidatou |
| Perfil | ✅ | toast "Perfil salvo" |
| Currículos | ✅ | criar com toast; excluir pede confirmação (bottom sheet), toast, e o item não volta ao recarregar |
| Avisos | ✅ | "Você foi selecionado(a)!" depois da seleção; "Marcar todos como lidos" com toast |
| Logout | ✅ | volta ao login |

**Falha de rede (app)** — Fase 3: erro com retry, nunca exceção crua.

| Cenário | Resultado | Evidência |
|---|---|---|
| Login sem rota até a API | ✅ | "Sem conexão com o servidor. Verifique sua internet e tente novamente." no formulário |
| Servidor fora do ar com a lista aberta (troca de filtro) | ✅ | tela "Sem conexão" + "Tentar novamente"; nenhum erro não tratado (`#blazor-error-ui` oculto) |
| Servidor de volta → "Tentar novamente" | ✅ | lista recarregada com o filtro escolhido |
| Token expirado / refresh rejeitado | ✅ (teste de contrato) | `ExpiredAccessToken_IsRenewedWithRefreshToken_AndRequestRetried` e `RejectedRefreshToken_ClearsSession_AndRaisesSessionExpired`, com o cliente real do app contra a API; o layout do app trata `SessionExpired` com aviso + login (não exercitado no emulador) |

### 6.4 Observações do ambiente de verificação

- Os checks do GitHub Actions não rodam: a conta está bloqueada por cobrança ("The job was not started because your
  account is locked due to a billing issue"), então **toda a verificação desta auditoria foi local** — nenhum workflow
  validou estes commits. Enquanto isso durar, a imagem Docker é construída na VPS (`infra/vps/build-push.sh`).
  **Atualização 2026-10-29:** o bloqueio acabou. Os workflows voltaram a rodar (CodeQL, `Build & Push Docker (GHCR)` e
  rebuild semanal concluídos com sucesso em 26/10, conferido com `gh run list`).
- A porta 5095 do host estava ocupada por outro processo local; a instância auditada rodou em 5195 e o app Android foi
  apontado para ela com `adb reverse tcp:5095 tcp:5195`.
- O Chromium do Playwright 1.50 (`chromium-1155`), exigido pelo projeto E2E, foi instalado em `~/.cache/ms-playwright`.
- O build Debug do app usa Fast Deployment: os assemblies ficam fora do APK, `pm clear` os apaga e o app fecha ao
  abrir ("No assemblies found … Assuming this is part of Fast Deployment"). Para testar com sessão limpa, o APK foi
  instalado com `-p:EmbedAssembliesIntoApk=true`. Não afeta builds Release.
- Remover só o `adb reverse` não simula queda: a conexão HTTP já aberta continua sendo reutilizada. O cenário
  "servidor fora do ar" foi feito parando a instância e religando-a.
- Sob carga, o emulador exibiu diálogos de sistema "não está respondendo" (Digital Wellbeing, Pixel Launcher), que são apps do
  Android e não do Monburan; os prints dos cenários de rede foram capturados direto do WebView.
- Os prints da verificação ficaram fora do repositório (diretório temporário da sessão).
