# Matriz de auditoria UI ↔ API — Monburan

> Fase 0 da auditoria de produção (inventário antes de qualquer correção).
> Base: commit `fa62646` (branch `main`). Data: 2026-10-17.
> Status de correção de cada item: ver [`AUDIT-REPORT.md`](AUDIT-REPORT.md).

## Como ler

- **Web** é Blazor Server: as páginas chamam *UseCases* em processo (não há HTTP entre a página e o backend). A coluna "Endpoint/UseCase" mostra o UseCase/serviço → repositório. O **mobile** chama a API REST (`/api/auth/*`, `/api/v1/*`) via `Monburan.Mobile.Shared`.
- **Existe?** — o recurso de backend chamado existe e está registrado.
- **Retorna erro?** — "sim" quando o fluxo feliz quebra, retorna dado errado ou falha silenciosamente.
- **Feedback** — o usuário recebe confirmação/erro visível (toast, modal, alerta).
- **Status** — ❌ P0 (quebrado, inseguro, dado incorreto, texto em inglês, UI sem backend) · ⚠️ P1 (sem feedback/loading/erro, desempenho, consistência) · 🙈 esconder (sem backend, fora de escopo) · ✅ ok.

Fatos que valem para várias linhas (citados como **[G1]**…):

- **[G1]** Não existe serviço de toast nem modal de confirmação compartilhados — cada página improvisa `alert` inline ou não dá feedback.
- **[G2]** Falhas de validação de domínio voltam como `Status = NoContent/HasValidation` com `Message` vazio; as páginas exibem `result.Message` → **falha silenciosa** (nada aparece). Erros internos (`HasError`) carregam `exception.Message` cru, que as páginas exibem.
- **[G3]** Autorização por papel inexistente: páginas de anunciante e de candidato usam só `[Authorize]`; a checagem de perfil é feita (ou não) dentro de cada página.
- **[G4]** Use cases de listagem buscam as primeiras 100/1000 linhas da **tabela inteira** e filtram em memória: além de lento, dados somem quando a base passa do limite.
- **[G5]** Mensagens de erro do ASP.NET Identity em inglês (não há `IdentityErrorDescriber` pt-BR) — chegam ao site e ao app via `/api/auth/register`.
- **[G6]** Datas UTC exibidas como se fossem horário local (diferença de 3 h para o Brasil).

---

## 1. Web — descoberta e páginas públicas

| Tela/Componente | Ação de UI | Endpoint/UseCase chamado | Existe? | Retorna erro? | Feedback (toast/modal)? | Loading? | Trata vazio/erro? | Status |
|---|---|---|---|---|---|---|---|---|
| Dashboard `/` `/dashboard` | Carregar visão geral (anunciante) | `GetDashboardAnalyticsUseCase` → EF | sim | falha silenciosa: métricas vazias sem aviso | não | sim | vazio sim / erro não | ⚠️ P1 |
| Dashboard | Carregar vagas recentes (candidato/anônimo) | `SearchJobsUseCase` → `JobRepository.SearchAsync` | sim | falha silenciosa ("Nenhuma vaga disponível") | não | sim | vazio sim / erro não | ⚠️ P1 |
| Dashboard | Atalhos (Acompanhar vagas, Criar vaga, Ver candidatos, Ver vagas, Acompanhar, Organizar) | navegação | sim | não | n/a | n/a | n/a | ✅ |
| Dashboard | "Ver detalhes" em vaga recente do anunciante | `/jobs?jobId=` (modal só abre p/ vaga `Active`) | sim | vaga encerrada → clique sem efeito | não | n/a | não | ⚠️ P1 |
| Dashboard | Faixa "Publicidade": "boost de vagas, destaque nas buscas e analytics" | `/plans` | promessa sem backend (boost/destaque nunca aplicados) | — | — | — | — | 🙈 P0 (também exibida a candidatos) |
| Dashboard (backend) | Analytics | carrega **todas** as candidaturas com `Include` em memória para contar | sim | — | — | — | — | ⚠️ P1 desempenho |
| Jobs `/jobs` | Buscar vagas (botão) | `SearchJobsUseCase` | sim | erro vira lista vazia | não | sim | vazio sim / erro não | ⚠️ P1 |
| Jobs | Paginação | `SearchJobsUseCase` | sim | só exibe páginas 1–5; sem loading | não | não | erro não | ⚠️ P1 |
| Jobs | Ver detalhes (modal) / `?jobId=` | memória / `GetJobByIdUseCase` | sim | modal do dono mostra status cru (`Active`) | n/a | n/a | n/a | ❌ P0 texto em inglês |
| Jobs | Quero me candidatar / Entrar para se candidatar / Criar conta | rotas `/apply/{id}`, `/Account/*` | sim | não | n/a | n/a | n/a | ✅ |
| Jobs | Ver candidaturas / Editar vaga (dono) | rotas | sim | não | n/a | n/a | n/a | ✅ |
| Jobs | Faixa "Publicidade": "Destaque vagas…" | `/plans` | promessa de destaque sem backend; exibida a candidatos | — | — | — | — | 🙈 P0 |
| JobDetails `/jobs/{id}` | Carregar vaga | `GetJobByIdUseCase` | sim | falha de banco vira "Vaga não encontrada"; `Description.Substring` sem null-check | não | sim | erro sem retry | ⚠️ P1 |
| JobDetails | **"Ver perfil da empresa"** | nenhum (`<button>` sem handler; não existe página pública de empresa) | **não** | botão morto | — | — | — | ❌ P0 esconder |
| JobDetails | Candidatar-se / Entrar / Criar conta / Voltar | rotas | sim | não | n/a | n/a | n/a | ✅ |
| Plans `/plans` | Carregar planos | `GetAvailablePlansUseCase` | sim | assinatura ativa procurada nas 100 primeiras assinaturas **[G4]** | não | sim | erro exibe `Message` (pode vir vazio) | ⚠️ P1 |
| Plans | Itens "Boost de vagas", "Destaque", "Analytics", "N currículos" | flags `HasBoost/HasHighlight/HasAnalytics/MaxResumes` | **não** — nunca lidas por nenhuma regra | — | — | — | — | 🙈 P0 |
| Plans | "N vagas por semana" | `CheckJobLimitUseCase` | existe, mas **nunca é chamado**; texto diz "por semana" e a regra é "vagas ativas" | limite não aplicado | — | — | — | ❌ P0 implementar |
| Plans | Fazer upgrade / Mudar para este plano | `UpgradePlanUseCase` | sim | sem confirmação, sem loading/double-submit, sem toast; erro com `Message` cru | não | não | parcial | ⚠️ P1 |
| Plans | Card "Currículo Pro" (R$ 19,90/ano, assistente, importação, exportação) + "Ativar no meu perfil" | nenhum (link para `/resumes`) | **não** | promessa falsa | — | — | — | 🙈 P0 |
| Plans | Criar conta de empresa / Entre para assinar | rotas | sim | não | n/a | n/a | n/a | ✅ |
| TermsOfUse `/terms`, PrivacyPolicy `/privacy` | Links internos | rotas | sim | não | n/a | n/a | n/a | ✅ (render interativo desnecessário — P2) |
| NotFound `/404` + `Routes.NotFound` | Voltar / Ver vagas | rotas | sim | não | n/a | n/a | n/a | ✅ |
| Error `/Error` | Voltar / Ver oportunidades | rotas | sim | não (sem stack trace) | n/a | n/a | n/a | ✅ |

## 2. Web — área do candidato

| Tela/Componente | Ação de UI | Endpoint/UseCase chamado | Existe? | Retorna erro? | Feedback (toast/modal)? | Loading? | Trata vazio/erro? | Status |
|---|---|---|---|---|---|---|---|---|
| ApplyJob `/apply/{id}` | Carregar vaga | `GetJobByIdUseCase` | sim | erro → "Vaga não encontrada" mas o formulário continua ativo | alerta | não | parcial | ⚠️ P1 |
| ApplyJob | Enviar candidatura | `CreateApplicationUseCase` | sim | "já candidatado" (409) e validação chegam com `Message` vazio → nada aparece **[G2]** | alerta | sim (botão) | não | ❌ P0 |
| ApplyJob | Texto "Seu currículo ativo será enviado automaticamente" | `CreateApplicationUseCase` | o use case **não** anexa currículo sem `ResumeId` | promessa falsa | — | — | — | ❌ P0 (implementar seleção de currículo, paridade com o app) |
| ApplyJob | Acesso por anunciante | `[Authorize]` | — | página abre e mostra aviso **[G3]** | — | — | — | ⚠️ P1 |
| MyApplications `/my-applications` | Listar | `ListApplicationsByCandidateUseCase` → `GetAllAsync(1, 1000)` + filtro em memória **[G4]** | sim | candidaturas somem acima de 1000 na plataforma | não | sim | vazio sim / erro sem retry | ❌ P0 |
| MyApplications | Status `Archived` | rótulo | — | exibe `Archived` | — | — | — | ❌ P0 texto em inglês |
| MyApplications | Data da candidatura | — | — | UTC exibido como local **[G6]** | — | — | — | ⚠️ P1 |
| ResumeList `/resumes` | Listar | `ListResumesByCandidateUseCase` → `GetAllAsync(1, 1000)` **[G4]** | sim | idem | não | sim | erro sem retry | ❌ P0 |
| ResumeList | Gera "currículo base" quando a lista está vazia | `CreateResumeUseCase` | sim | efeito colateral em leitura | alerta | — | — | ⚠️ P2 (mantido; documentado) |
| ResumeList | Novo currículo / Importar PDF / Ver currículo | rotas | sim | não | n/a | n/a | n/a | ✅ |
| ResumeCreate `/resumes/new` | Gerar rascunho | local | sim | não | n/a | sim | n/a | ✅ |
| ResumeCreate | Salvar | `CreateResumeUseCase` | sim | validação de domínio silenciosa **[G2]** | alerta | sim | não | ❌ P0 |
| ResumeDetails `/resumes/{id}` | Carregar | `GetResumeByIdUseCase` | sim | **sem checagem de dono — qualquer usuário logado lê qualquer currículo (IDOR)** | — | sim | erro sem retry | ❌ P0 segurança |
| ResumeDetails | Excluir | `DeleteResumeUseCase` | sim | **sem checagem de dono — exclui currículo de terceiros (IDOR)**; modal ad-hoc; erro com `Message` cru | modal ad-hoc | não | não | ❌ P0 segurança |
| ResumeDetails | Editar / Exportar PDF (`window.print`) / Voltar | rota / JS | sim | não | n/a | n/a | n/a | ✅ |
| ResumeEdit `/resumes/{id}/edit` | Carregar (checa dono) | `GetResumeByIdUseCase` | sim | não | n/a | sim | sim | ✅ |
| ResumeEdit | Salvar | `UpdateResumeUseCase` | sim | validação silenciosa **[G2]** | alerta | sim | não | ❌ P0 |
| ResumeEdit | Adicionar/remover experiência, formação, idioma, habilidade | local | sim | não | n/a | n/a | n/a | ✅ |
| UploadResume `/resumes/upload` | Selecionar PDF | validação local | sim | não | alerta | n/a | sim | ✅ |
| UploadResume | Enviar | `IStorageProvider` direto (ignora `UploadFileUseCase`) + `CreateResumeUseCase` | sim | valida só o content-type informado pelo navegador | alerta | sim | sim | ⚠️ P1 |
| UploadResume | Acesso por anunciante | `[Authorize]` | — | anunciante consegue enviar PDF **[G3]** | — | — | — | ⚠️ P1 |
| CandidateProfile `/profile/candidate` | Carregar | `IBaseRepository<Candidate>.GetByIdAsync` | sim | erro vira "Candidato não encontrado" | — | sim | erro sem retry | ⚠️ P1 |
| CandidateProfile | Salvar | `UpdateCandidateUseCase` | sim | exibe `Erro ao salvar: {ex.Message}` (**exceção crua**); validação silenciosa | alerta | sim | não | ❌ P0 |
| CandidateProfile | Status da conta (`Active`, `Incomplete`) | — | — | texto em inglês | — | — | — | ❌ P0 |
| CandidateProfile | "E-mail verificado" | `Candidate.IsEmailVerified` | campo existe, mas **nunca é atualizado** na confirmação do e-mail | sempre "Não" | — | — | — | ❌ P0 dado incorreto |
| CandidateProfile | Endereço / Segurança da conta | `/address/*`, `/Account/Manage` | sim | páginas sem nenhum link (órfãs) | — | — | — | ⚠️ P1 |
| Notifications `/notifications` | Listar | `ListNotificationsByUserUseCase` → `GetAllAsync(1, 1000)` **[G4]** | sim | idem; usuário sem perfil consulta destinatário `"default"` | não | sim | vazio sim / erro não | ❌ P0 |
| Notifications | Marcar como lida | `MarkNotificationAsReadUseCase` | sim | falha silenciosa | não | não | não | ⚠️ P1 |
| Notifications | Marcar todas como lidas | N chamadas sequenciais | sim | falha silenciosa | não | não | não | ⚠️ P1 |
| Notifications | Atualizar | `ListNotificationsByUserUseCase` | sim | — | não | sim | — | ✅ |

## 3. Web — área do anunciante

| Tela/Componente | Ação de UI | Endpoint/UseCase chamado | Existe? | Retorna erro? | Feedback (toast/modal)? | Loading? | Trata vazio/erro? | Status |
|---|---|---|---|---|---|---|---|---|
| JobCreate `/jobs/create` | Publicar vaga | `CreateJobUseCase` | sim | validação de domínio silenciosa **[G2]**; limite do plano não verificado | alerta | sim | não | ❌ P0 |
| JobCreate | Sucesso | navega para `/jobs?jobId=` | sim | sem confirmação | não | — | — | ⚠️ P1 |
| JobCreate | Acesso por candidato | `[Authorize]` + checagem interna | — | aviso dentro da página **[G3]** | — | — | — | ⚠️ P1 |
| JobEdit `/jobs/edit/{id}` | Salvar | `UpdateJobUseCase` | sim | validação silenciosa **[G2]** | alerta | sim | não | ❌ P0 |
| JobEdit | Reativar/encerrar vaga | — | **não** (DTO sem status) | — | — | — | — | lacuna de produto |
| MyJobs `/my-jobs` | Listar | `SearchJobsUseCase` com `AdvertiserId` | sim | **usuário sem perfil de anunciante (AdvertiserId nulo) vê todas as vagas da plataforma como "suas"**; vagas não ativas (expiradas/pausadas) nunca aparecem | não | sim | erro silencioso | ❌ P0 |
| MyJobs | Excluir vaga | `DeleteJobUseCase` (existe; `DELETE /api/v1/jobs/{id}` existe) | backend sim / **UI não** | — | — | — | — | ❌ P0 (fluxo core — implementar com confirmação) |
| MyJobs | Ver / Editar / Ver candidaturas | rotas | sim | não | n/a | n/a | n/a | ✅ |
| MyJobs | Nova vaga | `NavigateTo(..., forceLoad: true)` | sim | recarrega o app inteiro | — | — | — | ⚠️ P2 |
| ReceivedApplications `/received-applications`, `/jobs/{id}/applications` | Listar | `ListApplicationsByJobUseCase` → `GetAllAsync(1, 1000)` da **plataforma inteira** + filtro em memória **[G4]** | sim | candidaturas somem acima de 1000 | não | sim | erro com `Message` cru | ❌ P0 |
| ReceivedApplications | Selecionar / Rejeitar | `UpdateApplicationStatusUseCase` | sim | sucesso exibe "Status atualizado para **Selected**" (inglês); rejeitar sem confirmação; botões ativos mesmo no status final | alerta | não | parcial | ❌ P0 |
| ReceivedApplications | Ver mensagem do candidato / currículo anexado | `ApplicationResponse.Notes/ResumeId` (dados existem) | dado sim / **UI não** | recrutador nunca vê a mensagem nem o currículo | — | — | — | ❌ P0 (fluxo core — implementar) |
| AdvertiserProfile `/profile/advertiser` | Salvar | `UpdateAdvertiserUseCase` | sim | **exceção crua**; validação silenciosa; status em inglês | alerta | sim | não | ❌ P0 |
| AdvertiserProfile | Logo | campo de URL; upload existe em página sem link | — | — | — | — | — | ⚠️ P1 |
| UploadLogo `/advertiser/upload-logo` | Enviar logo | `IStorageProvider` direto | sim | **não atualiza `LogoUrl` do anunciante — upload sem efeito** | alerta | sim | sim | ❌ P0 |
| UploadLogo | Cancelar | `/advertiser/profile` | **não** (rota real é `/profile/advertiser`) | 404 | — | — | — | ❌ P0 |
| MySubscription `/my-subscription` | Carregar | `GetAvailablePlansUseCase` **[G4]** | sim | idem Plans | não | sim | erro sem retry | ⚠️ P1 |
| MySubscription | Status (`Active`, `Cancelled`…) e recursos do plano | — | recursos sem backend | status em inglês | — | — | — | ❌ P0 / 🙈 |
| MySubscription | Cancelar assinatura | `CancelSubscriptionUseCase` | sim | modal ad-hoc; `Navigation.Refresh(forceReload)` perde contexto; disponível até no plano grátis | modal | não | parcial | ⚠️ P1 |

## 4. Web — administração e endereço

| Tela/Componente | Ação de UI | Endpoint/UseCase chamado | Existe? | Retorna erro? | Feedback (toast/modal)? | Loading? | Trata vazio/erro? | Status |
|---|---|---|---|---|---|---|---|---|
| CandidatesList `/admin/candidates` | Buscar / filtrar / paginar | `ListCandidatesUseCase` → `GetAllAsync(page, size)` e **filtra depois de paginar** | sim | resultados e `TotalCount` errados | não | sim | erro silencioso ("Poderia mostrar mensagem") | ❌ P0 |
| CandidatesList | **Ver detalhes** | `/admin/candidates/{id}` | **não** (rota inexistente) | 404 | — | — | — | ❌ P0 esconder |
| CandidatesList | Excluir | `DeleteCandidateUseCase` | sim | erro silencioso | modal ad-hoc | não | não | ⚠️ P1 |
| AdvertisersList `/admin/advertisers` | Buscar / filtrar / paginar | `ListAdvertisersUseCase` (mesmo defeito) | sim | idem | não | sim | não | ❌ P0 |
| AdvertisersList | Excluir | `DeleteAdvertiserUseCase` | sim | erro silencioso | modal ad-hoc | não | não | ⚠️ P1 |
| Layout | Acesso do admin às páginas | — | — | nenhuma entrada de menu para `Admin` | — | — | — | ⚠️ P1 |
| Address `/address/list` | Listar / Novo / Editar / Excluir | `AddressService` → `AddressRepository` | sim | páginas órfãs (sem link) | não | parcial | parcial | ⚠️ P1 |
| Address `/address/create`, `/address/edit/{id}` | Salvar | `AddressService.AddAsync/UpdateAsync` | sim | `try/finally` sem `catch`: exceção derruba o circuito (tela "Algo inesperado") | não | sim | não | ❌ P0 |
| Address `/address/delete/{id}` | Confirmar exclusão | `AddressService.DeleteAsync` | sim | — | página de confirmação | — | — | ⚠️ P1 |

## 5. Web — conta (ASP.NET Identity, SSR)

| Tela/Componente | Ação de UI | Endpoint/UseCase chamado | Existe? | Retorna erro? | Feedback (toast/modal)? | Loading? | Trata vazio/erro? | Status |
|---|---|---|---|---|---|---|---|---|
| Login | Entrar / Esqueci a senha / Reenviar confirmação | `SignInManager` / `UserManager` / `IdentityEmailSender` | sim | não | `StatusMessage` | n/a (SSR) | sim | ✅ |
| Register | Criar conta (candidato/anunciante) | `UserManager.CreateAsync` + `Create{Candidate,Advertiser}UseCase` | sim | erros do Identity em inglês **[G5]** | `ValidationSummary` | n/a | sim | ❌ P0 |
| ForgotPassword / ResetPassword / ConfirmEmail / ResendEmailConfirmation | Fluxos de e-mail | `UserManager` + use cases de e-mail | sim | confirmação não sincroniza `IsEmailVerified` do perfil | `StatusMessage` | n/a | sim | ❌ P0 (dado) |
| Manage `/Account/Manage/*` | Perfil, e-mail, senha, 2FA, códigos de recuperação, dados pessoais | Identity | sim | **textos em inglês** ("Disable 2FA", "Reset recovery codes", "You have N recovery codes left", "Delete", "Delete data and close my account"…) | `StatusMessage` | n/a | sim | ❌ P0 |
| Manage | Navegação até a área | — | — | nenhum link no layout/perfil | — | — | — | ⚠️ P1 |
| DeletePersonalData | Excluir conta | `UserManager.DeleteAsync` | sim | perfil de candidato/anunciante (nome, e-mail, telefone) permanece; e-mail fica bloqueado para novo cadastro | redirect | n/a | — | ❌ P0 (LGPD) |
| Layout | Sair | `POST Account/Logout` | sim | não | redirect | n/a | n/a | ✅ |

## 6. Mobile (MAUI Blazor Hybrid — escopo candidato)

| Tela/Componente | Ação de UI | Endpoint chamado | Existe? | Retorna erro? | Feedback (toast/modal)? | Loading? | Trata vazio/erro? | Status |
|---|---|---|---|---|---|---|---|---|
| Login `/login` | Entrar | `POST /api/auth/login` | sim | não | alerta | sim | sim | ✅ (sem envio pelo Enter; labels sem `for` — P2) |
| Register `/register` | Criar conta | `POST /api/auth/register` | sim | erros do Identity em inglês **[G5]**; erros não aparecem por campo | alerta | sim | sim | ❌ P0 |
| ForgotPassword `/forgot-password` | Enviar link | `POST /api/auth/forgot-password` | sim | não | estado de sucesso | sim | sim | ✅ |
| Jobs `/` | Buscar / filtrar (Todas, Remoto, Presencial) | `GET /api/v1/jobs` | sim | não | n/a | sim | sim | ✅ |
| Jobs | Salário no card | `JobResponse.SalaryMin/Max` | sim | **exibe salário mesmo com `SalaryDisplay = false`**; moeda fixa "R$" | — | — | — | ❌ P0 privacidade |
| Jobs | Carregar mais | `GET /api/v1/jobs?page=n` | sim | erro **substitui a lista inteira** por ErrorState | não | sim | parcial | ⚠️ P1 |
| JobDetails `/jobs/{id}` | Carregar | `GET /api/v1/jobs/{id}` | sim | não | n/a | sim | sim (retry) | ✅ |
| JobDetails | Candidatar-se | `/apply/{id}` | sim | botão aparece para vaga encerrada; salário oculto ignorado | — | — | — | ⚠️ P1 / ❌ P0 |
| Apply `/apply/{id}` | Carregar vaga + currículos | `GET /api/v1/jobs/{id}`, `GET /api/v1/resumes/mine` | sim | **falha → tela em branco** (sem ErrorState) | — | sim | não | ❌ P0 |
| Apply | Confirmar candidatura | `POST /api/v1/applications` | sim | não | estado de sucesso / alerta | sim | sim | ✅ |
| MyApplications `/applications` | Listar | `GET /api/v1/applications/mine` | sim | backend **[G4]** | n/a | sim | sim | ✅ (corrigir backend) |
| Profile `/profile` | Carregar / Salvar | `GET`/`PUT /api/v1/candidates/me` | sim | "E-mail verificado" nunca aparece (flag não sincronizado) | alerta | sim | sim | ⚠️ P1 |
| Profile | Sair | `TokenService.ClearSessionAsync` | sim | não | navegação | n/a | n/a | ✅ |
| Resumes `/resumes` | Criar | `POST /api/v1/resumes` | sim | não | alerta | sim | sim | ✅ |
| Resumes | Enviar PDF | `POST /api/v1/files/upload?fileType=Pdf` + `POST /api/v1/resumes` | sim | não | alerta | sim | sim | ✅ |
| Resumes | **Excluir** | `DELETE /api/v1/resumes/{id}` | sim | **sem confirmação** | alerta | não | sim | ❌ P0 |
| Notifications `/notifications` | Listar | `GET /api/v1/notifications/mine` | sim | backend **[G4]**; horário UTC **[G6]** | n/a | sim | sim | ⚠️ P1 |
| Notifications | Marcar como lida (toque) | `PUT /api/v1/notifications/{id}/read` | sim | **falha silenciosa** (`catch` vazio) | não | não | não | ❌ P0 |
| MainLayout | Badge de não lidas | `GET /api/v1/notifications/mine/unread-count` | sim | erro ignorado (aceitável — badge é acessório) | n/a | n/a | n/a | ✅ |
| MainLayout | Sessão expira (refresh falha) | `TokenService.SessionExpired` | sim | estado limpo, mas a tela atual não volta ao login | não | — | — | ⚠️ P1 |
| NotFound `/not-found` | Voltar para vagas | rota (`Router.NotFoundPage`) | sim | não | n/a | n/a | n/a | ✅ |
| Todas | Toast / modal de confirmação | — | **não existem** **[G1]** | — | — | — | — | ❌ P0 |

## 7. Contrato `Monburan.Mobile.Shared` ↔ API

Verificado por leitura (rota, verbo, DTO, casing via `JsonSerializerDefaults.Web`, enums como string). A desserialização real é validada por testes de contrato na Fase 5.

| Cliente | Método | Verbo / rota | Request → Response | Endpoint existe? | Contrato | Usado por |
|---|---|---|---|---|---|---|
| `AuthApiClient` | `LoginAsync` | `POST api/auth/login` | `UserLoginRequest` → `UserLoginResponse` | sim | ✅ | Login |
| `AuthApiClient` | `RegisterAsync` | `POST api/auth/register` | `RegisterRequest` → `UserLoginResponse` (201) | sim | ✅ (mensagens [G5]) | Register |
| `AuthApiClient` | `ForgotPasswordAsync` | `POST api/auth/forgot-password` | `ForgotPasswordRequest` → 200 vazio | sim | ✅ | ForgotPassword |
| `TokenService` | `RefreshAsync` | `POST api/auth/refresh` | `RefreshTokenRequest` → `UserLoginResponse` | sim | ✅ | `AuthHeaderHandler` (401 → renova e repete 1×) |
| `MonburanApiClient` | `GetJobsAsync` | `GET api/v1/jobs?…` | query → `ListJobsResponse` | sim | ✅ (`advertiserId` é ignorado pelo endpoint — correto) | Jobs |
| `MonburanApiClient` | `GetJobAsync` | `GET api/v1/jobs/{id}` | → `JobResponse` | sim | ✅ | JobDetails, Apply |
| `MonburanApiClient` | `GetMyCandidateAsync` | `GET api/v1/candidates/me` | → `CandidateResponse` | sim | ✅ | AuthStateService, Profile |
| `MonburanApiClient` | `UpdateMyCandidateAsync` | `PUT api/v1/candidates/me` | `UpdateCandidateRequest` → `CandidateResponse` | sim | ✅ | Profile |
| `MonburanApiClient` | `ApplyAsync` | `POST api/v1/applications` | `CreateApplicationRequest` → `ApplicationResponse` (201) | sim | ✅ | Apply |
| `MonburanApiClient` | `GetMyApplicationsAsync` | `GET api/v1/applications/mine` | → `List<ApplicationResponse>` | sim | ✅ (backend [G4]) | MyApplications |
| `MonburanApiClient` | `GetMyResumesAsync` | `GET api/v1/resumes/mine` | → `List<ResumeResponse>` | sim | ✅ (backend [G4]) | Apply, Resumes |
| `MonburanApiClient` | `GetResumeAsync` | `GET api/v1/resumes/{id}` | → `ResumeResponse` | sim | ✅ | — (não usado) |
| `MonburanApiClient` | `CreateResumeAsync` | `POST api/v1/resumes` | `CreateResumeRequest` → `ResumeResponse` (201) | sim | ✅ | Resumes |
| `MonburanApiClient` | `UpdateResumeAsync` | `PUT api/v1/resumes/{id}` | `UpdateResumeRequest` → `ResumeResponse` | sim | ✅ | — (não usado) |
| `MonburanApiClient` | `DeleteResumeAsync` | `DELETE api/v1/resumes/{id}` | → 200 (corpo ignorado) | sim | ✅ | Resumes |
| `MonburanApiClient` | `UploadPdfAsync` | `POST api/v1/files/upload?fileType=Pdf` (multipart) | → `UploadFileResponse` | sim | ✅ | Resumes |
| `MonburanApiClient` | `GetMyNotificationsAsync` | `GET api/v1/notifications/mine` | → `List<NotificationResponse>` | sim | ✅ (backend [G4]) | Notifications |
| `MonburanApiClient` | `MarkNotificationAsReadAsync` | `PUT api/v1/notifications/{id}/read` | → 200 (corpo ignorado) | sim | ✅ | Notifications |
| `MonburanApiClient` | `GetUnreadCountAsync` | `GET api/v1/notifications/mine/unread-count` | → `{ count }` | sim | ✅ | MainLayout |
| `MonburanApiClient` | `GetPlansAsync` | `GET api/v1/plans` | → `AvailablePlansResponse` | sim | ✅ | — (não usado no app candidato) |
| API | 401 | `JwtBearerEvents.OnChallenge` | corpo `{ "error": "Unauthorized" }` (não é ProblemDetails) | — | ⚠️ P1 (cliente cai na mensagem padrão) | todos |
| API | Exceção não tratada em produção | `UseExceptionHandler("/Error")` | devolve **HTML** da página de erro para `/api/*` | — | ⚠️ P1 | todos |
| API | Salário oculto | `GET api/v1/jobs`, `GET api/v1/jobs/{id}` | expõe `SalaryMin/Max` mesmo com `SalaryDisplay = false` | — | ❌ P0 privacidade | Jobs, JobDetails |

## 8. Backend — achados transversais

| Área | Achado | Status |
|---|---|---|
| Use cases de listagem | **[G4]** `ListApplicationsByCandidate`, `ListApplicationsByJob`, `ListResumesByCandidate`, `ListNotificationsByUser` (1000 linhas), `GetAvailablePlans`, `CheckJobLimit`, `UpgradePlan` (100 assinaturas) filtram em memória | ❌ P0 |
| Admin | `ListCandidates`/`ListAdvertisers` filtram depois de paginar | ❌ P0 |
| Planos | `CheckJobLimitUseCase` nunca chamado — limite de vagas não aplicado | ❌ P0 |
| `UpgradePlanUseCase` | ativa plano pago sem cobrança (não há gateway) | lacuna de produto |
| Upload | só content-type do cliente; sem verificação de assinatura (magic bytes); páginas web chamam o storage direto | ⚠️ P1 |
| Repositórios | `SearchAsync`/`GetByIdAsync` sem `AsNoTracking` em leitura | ⚠️ P1 |
| Dashboard | agregações feitas em memória sobre todas as candidaturas | ⚠️ P1 |
| Notificações por e-mail | falhas engolidas sem log (`catch (Exception) { }`) em `CreateApplication`/`UpdateApplicationStatus` | ⚠️ P1 |
| `CurrentUserHelper` | depende de `IHttpContextAccessor` dentro de circuitos interativos (padrão desaconselhado; frágil fora de WebSockets) | ⚠️ P1 |
| Identity | mensagens em inglês **[G5]** | ❌ P0 |
| Perfis | `IsEmailVerified` nunca sincronizado com `EmailConfirmed` | ❌ P0 |
| Segredos | `appsettings.Development.json` contém apenas chave JWT de desenvolvimento; produção exige variáveis de ambiente (`ValidateOnStart`) | ✅ |

## 9. Resumo do inventário

Contagem por linha das tabelas acima (uma linha pode reunir mais de um defeito).

| Severidade | Linhas |
|---|---|
| ❌ P0 | 48 |
| 🙈 esconder (sem backend / fora de escopo) | 4 |
| ⚠️ P1 | 40 |
| ⚠️ P2 / lacunas de produto | 6 |
| ✅ ok | 48 |
