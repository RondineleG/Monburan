# 📱 Mobile Plan — Monburan (.NET MAUI Blazor Hybrid)

> Plano ponta a ponta para criar o app móvel Monburan (Android + iOS) em C#,
> reaproveitando o backend existente. Estratégia escolhida: **.NET MAUI Blazor Hybrid**
> consumindo uma **nova camada de API REST** exposta pelo `Monburan.Web`.
>
> Gerado em: 2026-10-15
>
> ---
>
> ## ✅ Status de implementação (atualizado em 2026-10-16)
>
> **Fases 0, 1 e 2 estão implementadas** — os checkboxes abaixo permanecem como
> o plano original; este resumo reflete o que já existe no repositório:
>
> - **Fase 0 (API):** endpoints `/api/auth/*` (login, refresh, register,
>   forgot-password) e `/api/v1/*` (jobs, applications, candidates, resumes,
>   files, notifications, advertisers, dashboard, plans) em
>   `src/Monburan.Web/Endpoints`, com JWT Bearer (`token_use=access`) e
>   `IdentityService` registrado no DI — inclui os endpoints do "lado anunciante"
>   da Fase 0.4
> - **Fase 1 (testes):** `tests/Monburan.ApiTests` com 38 testes via
>   `WebApplicationFactory` + EF InMemory
> - **Fase 2 (app):** `src/Monburan.Mobile` (MAUI Blazor Hybrid,
>   `com.monburan.app`) + `src/Monburan.Mobile.Shared` (`MonburanApiClient`,
>   `AuthApiClient`, `TokenService`, `AuthHeaderHandler`, `SecureStorageTokenStore`)
>   — páginas de candidato implementadas (Login, Register, ForgotPassword, Jobs,
>   JobDetails, Apply, MyApplications, Profile, Resumes, Notifications) e
>   branding MONBURAN; `MobileApiConfig` aponta para `https://monburan.com.br`
>   em Release e `127.0.0.1:5095` (via `adb reverse`) em Debug
> - **Fase 5 (parcial):** job `android-build` no CI gera APK debug e AAB release
>   assinado quando os secrets do keystore existem
> - **Pendente:** publicação nas stores (Fases 3/4 — Google Play, iOS/TestFlight
>   que exige macOS), push notifications, cache offline e demais itens da Fase 6
>
> ---

---

## 🗺️ Visão geral da arquitetura alvo

```
Monburan.sln
├── src/
│   ├── Monburan.Core            (INTOCADO — domínio, UseCases, DTOs)   ✅ reusa 100%
│   ├── Monburan.Web             (Blazor + NOVO: endpoints /api/v1)   ✅ reusa DI/repos
│   ├── Monburan.Web.Client      (WASM — intocado)
│   ├── Monburan.Mobile          (NOVO — .NET MAUI Blazor Hybrid)     🆕
│   └── Monburan.Mobile.Shared   (NOVO — ApiClient, auth, helpers)    🆕
└── tests/
    └── Monburan.ApiTests        (NOVO — WebApplicationFactory)       🆕
```

**Princípio:** zero mudança no domínio. A API é uma casca fina sobre os UseCases já
registrados no DI do `Monburan.Web`.

---

## FASE 0 — Fundação da API (backend)

### 0.1 Autenticação bearer (mobile não usa cookie)

> **⚠️ DESCOBERTA NA AUDITORIA:** a infra JWT **já existe escrita** —
> `Monburan.Web/Services/IdentityService.cs` (login → access token + refresh token
> JWT, já inclui claims `candidate_id`/`advertiser_id`/`user_type`) e
> `Monburan.Web/Configurations/JwtOptions.cs` — **mas NÃO está registrada no DI**
> (`IIdentityService` não aparece no `Program.cs`), não há `AddJwtBearer` e não
> existe seção `Jwt` no `appsettings.json`. Ou seja: o emissor de token existe,
> o validador e a configuração não. Falta só "ligar".

- [ ] **Decisão:** ligar o `IdentityService` existente (recomendado — já tem
      claims custom de perfil, refresh token e DTOs `UserLoginRequest`/`UserLoginResponse`
      do Core) em vez do `MapIdentityApi` nativo — este último **não cria o perfil
      Candidate/Advertiser** (ver 0.1.2). Pode-se ainda assim expor partes do
      `MapIdentityApi` (ex.: `manage/info`) como complemento.
- [ ] Criar seção `Jwt` no `appsettings.json` + `appsettings.Development.json`:
  `Issuer`, `Audience`, `AccessTokenExpiration` (seg), `RefreshTokenExpiration` (seg),
  `SigningKey` — **secret via env var/User Secrets em prod, nunca commitada**
- [ ] Registrar no `Program.cs`: `builder.Services.Configure<JwtOptions>(...)` +
  `builder.Services.AddScoped<IIdentityService, IdentityService>()`
- [ ] Adicionar `AddJwtBearer()` ao `AddAuthentication` existente (validação do
  token emitido pelo IdentityService) + `Microsoft.AspNetCore.Authentication.JwtBearer`
- [ ] Endpoint `POST /api/auth/login` → `IdentityService.Login(UserLoginRequest)`
      → `UserLoginResponse` (accessToken + refreshToken)
- [ ] Endpoint `POST /api/auth/refresh` → aceita refreshToken, reemite par de tokens
      (o `Login` atual não persiste refresh token — decidir: refresh JWT stateless
      validando assinatura/exp, ou persistir hash por usuário)
- [ ] Garantir que endpoints de API usem **só** o bearer scheme
  (`RequireAuthorization()` com policy/JwtBearer scheme no route group `/api/v1`)
  para não cair no redirect de cookie do site
- [ ] Validar: login via curl/Postman retorna `accessToken` + `refreshToken`

#### 0.1.2 Registro customizado (OBRIGATÓRIO — não vem de graça)

- [ ] `POST /api/auth/register` custom — replicar o fluxo do
      `Components/Account/Pages/Register.razor`:
  - [ ] `UserManager.CreateAsync` com email+senha (ou `IdentityService.RegisterUser`)
  - [ ] Conforme `accountType` ("candidate"|"advertiser"): chamar
        `CreateCandidateUseCase`/`CreateAdvertiserUseCase` com nome + email
  - [ ] Resposta: tokens (auto-login) ou confirmação
  - [ ] ⚠️ O vínculo usuário↔perfil hoje é **por email** (`ApplicationUser.Email ==
        Candidate.Email` na `ApplicationUserClaimsPrincipalFactory`) — funciona,
        mas é frágil (troca de email quebra o vínculo). Registrar como limitação
        conhecida; ideal futuro: FK real
- [ ] Password reset / confirmação de email no app: páginas existem só em Razor.
      Decisão: endpoints próprios (`/api/auth/forgot-password`, `/reset-password`)
      reutilizando `PasswordResetUseCase`/`EmailConfirmationUseCase`, ou deep-link
      para o site. MVP pode linkar o site.

### 0.2 Estrutura de endpoints

- [ ] Criar pasta `src/Monburan.Web/Endpoints/`
- [ ] Criar extension `MapApiEndpoints(this WebApplication app)` com
  `app.MapGroup("/api/v1")`
- [ ] Cada handler chama o UseCase já registrado (casca fina, sem lógica nova)
- [ ] **Mapper `RequestResult` → HTTP** (o pattern Result JÁ existe — os UseCases
      retornam `RequestResult<T>` com `EResultStatus`, não lançam exceções):
  | `EResultStatus` | HTTP |
  |---|---|
  | `Success` | 200 / 201 |
  | `HasValidation` | 400 + `ValidationResult.Errors` (field+message) no `ProblemDetails.errors` |
  | `EntityNotFound` | 404 |
  | `EntityAlreadyExists` | 409 |
  | `EntityHasError` / `HasError` | 400 ou 500 conforme `RequestError` |
  | `NoContent` | 204 |
  - [ ] Helper único `ToHttpResult(this RequestResult<T>)` usado por todos endpoints
- [ ] Padronizar erros com `ProblemDetails` (RFC 7807) via o mapper acima;
      exceptions de `Monburan.Core/Exceptions/` (DomainException, NotFoundException…)
      como rede de segurança num exception handler global
- [ ] Rate limit (middleware `UseRateLimit` já existe) cobre `/api/*` — confirmar
- [ ] Antiforgery NÃO se aplica a chamadas bearer — sem conflito, validar
- [ ] Swagger já configurado — endpoints aparecem automaticamente ✅

### 0.3 Endpoints — MVP (lado candidato)

- [ ] `GET  /api/v1/jobs` → `ListJobsUseCase` / `SearchJobsUseCase`
      (query: `ListJobsRequest` com filtros + `PagedResponse<JobResponse>`) — **público**
- [ ] `GET  /api/v1/jobs/{id}` → `GetJobByIdUseCase` (`JobResponse`) — **público**
- [ ] `POST /api/v1/applications` → `CreateApplicationUseCase`
      (`CreateApplicationRequest` → `ApplicationResponse`) — **bearer**
  - [ ] ⚠️ **SEGURANÇA (IDOR):** `CreateApplicationRequest.CandidateId` vem do body
        — o endpoint DEVE sobrescrever com `ICurrentUserHelper.GetCandidateId()`
        (claim `candidate_id`) e retornar 403 se o usuário não tiver perfil de
        candidato. Nunca confiar no id enviado pelo cliente.
- [ ] `GET  /api/v1/applications/mine` → `ListApplicationsByCandidateUseCase`
      usando `GetCandidateId()` das claims — **bearer** (mesma regra: id nunca
      vem do cliente nos endpoints `/mine`)
- [ ] `GET  /api/v1/candidates/me` → `GetCandidateByIdUseCase` com id da claim — **bearer**
- [ ] `PUT  /api/v1/candidates/me` → `UpdateCandidateUseCase`
      (`UpdateCandidateRequest`) — **bearer**
- [ ] `GET  /api/v1/resumes/mine` → `ListResumesByCandidateUseCase` — **bearer**
- [ ] `POST /api/v1/resumes` → `CreateResumeUseCase` (`CreateResumeRequest`) — **bearer**
- [ ] `POST /api/v1/files/upload` (multipart) → `UploadFileUseCase`
      + `IStorageProvider`/`LocalStorageProvider` (`UploadFileResponse`) — **bearer**
      - [ ] Endpoint lê `IFormFile` → `file.OpenReadStream()` → `UploadFileRequest.FileStream`
      - [ ] Validações JÁ existem no UseCase: imagem ≤5MB, PDF/outros ≤10MB,
            content-types jpeg/png/gif/webp/pdf — só repassar erros 400
      - [ ] `UploadFileResponse.FileUrl` é **relativa** (`/uploads/yyyy/MM/guid.ext`,
            servida pelo `UseStaticFiles`/`wwwroot`) — o app concatena com a
            base URL da API
- [ ] `GET  /api/v1/notifications/mine` → `ListNotificationsByUserUseCase` — **bearer**
- [ ] `PUT  /api/v1/notifications/{id}/read` → `MarkNotificationAsReadUseCase` — **bearer**
- [ ] `ICurrentUserHelper` funciona com bearer via `IHttpContextAccessor` +
      claims `candidate_id`/`advertiser_id`/`user_type` — confirmado no código ✅
      (claims já emitidas tanto no cookie quanto no JWT do `IdentityService`)

### 0.4 Endpoints — Fase 2 do app (lado anunciante)

- [ ] `POST   /api/v1/jobs` → `CreateJobUseCase` (`CreateJobRequest`) — **bearer**
- [ ] `PUT    /api/v1/jobs/{id}` → `UpdateJobUseCase` (`UpdateJobRequest`) — **bearer**
- [ ] `DELETE /api/v1/jobs/{id}` → `DeleteJobUseCase` — **bearer**
- [ ] `GET    /api/v1/jobs/mine` → vagas do anunciante logado — **bearer**
- [ ] `GET    /api/v1/jobs/{id}/applications` → `ListApplicationsByJobUseCase` — **bearer**
- [ ] `PUT    /api/v1/applications/{id}/status` → `UpdateApplicationStatusUseCase`
      (`UpdateApplicationStatusRequest`) — **bearer**
- [ ] `GET    /api/v1/dashboard` → `GetDashboardAnalyticsUseCase`
      (`AnalyticsResponse`) — **bearer**
- [ ] `GET    /api/v1/plans` → `GetAvailablePlansUseCase` (`AvailablePlansResponse`)
- [ ] `POST   /api/v1/subscriptions/upgrade` → `UpgradePlanUseCase`
      (`UpgradePlanRequest`) — **bearer**
- [ ] `POST   /api/v1/subscriptions/cancel` → `CancelSubscriptionUseCase` — **bearer**
- [ ] `POST   /api/v1/advertisers/logo` (multipart) → `UploadFileUseCase` — **bearer**

### 0.5 Checklist de qualidade da API

- [ ] Nenhum endpoint expõe entidade de domínio — só DTOs do `Monburan.Core` ✅
- [ ] Todos os métodos async propagam `CancellationToken`
- [ ] HTTPS obrigatório em produção (app já tem `UseHttpsRedirection`)
- [ ] `GET /health` já existe — incluir no monitoramento do app
- [ ] Documentar no `README.md` a seção "API para mobile" com exemplos curl

---

## FASE 1 — Testes da API

- [ ] Criar `tests/Monburan.ApiTests` (xUnit — mesmo padrão dos projetos de teste)
  - [ ] `Microsoft.AspNetCore.Mvc.Testing` + `WebApplicationFactory<Program>`
  - [ ] EF Core InMemory (já usado no projeto) ou Testcontainers Postgres
- [ ] Testes de auth:
  - [ ] `POST /api/auth/login` retorna tokens com credenciais válidas
  - [ ] `POST /api/auth/refreshToken` renova access token
  - [ ] Endpoint protegido sem token → 401
  - [ ] Endpoint protegido com token inválido/expirado → 401
- [ ] Testes de fluxo:
  - [ ] Listar vagas público retorna paginação (`PagedResponse`)
  - [ ] Candidatura end-to-end: login → criar candidatura → listar `/applications/mine`
  - [ ] Upload de arquivo: multipart válido → 200; tipo inválido → 400
- [ ] Adicionar projeto de teste ao pipeline CI existente

---

## FASE 2 — Projeto mobile (scaffold + infra)

### 2.1 Pré-requisitos de ambiente (Windows)

- [ ] .NET 10 SDK já instalado ✅ (`global.json` na raiz)
- [ ] Instalar workload MAUI: `dotnet workload install maui`
  (ou `maui-android` + `maui-ios` separados)
- [ ] Android SDK + emulador (via instalador do workload / Android Studio)
- [ ] Aceitar licenças: `dotnet build -t:InstallAndroidDependencies ...` ou
  Android SDK Manager

### 2.2 Criar os projetos

- [ ] `dotnet new maui-blazor -n Monburan.Mobile -o src/Monburan.Mobile`
  - targets: `net10.0-android;net10.0-ios` (remover `maccatalyst`/`windows` se
    não forem alvo — simplifica build)
- [ ] `dotnet new classlib -n Monburan.Mobile.Shared -o src/Monburan.Mobile.Shared -f net10.0`
- [ ] `dotnet sln Monburan.sln add src/Monburan.Mobile src/Monburan.Mobile.Shared`
- [ ] `Monburan.Mobile` referencia `Monburan.Mobile.Shared`
- [ ] `Monburan.Mobile.Shared` referencia `Monburan.Core`
  (reuso direto de DTOs/Enums — EF Core vem transitivo mas não é usado em runtime)
  - [ ] *Alternativa futura:* extrair `Monburan.Contracts` só com DTOs/Enums se o
        peso do EF Core incomodar

### 2.3 Camada de acesso à API (`Monburan.Mobile.Shared`)

- [ ] `MonburanApiClient` — HttpClient tipado via `IHttpClientFactory`
  - [ ] Métodos por domínio: `GetJobsAsync`, `GetJobByIdAsync`, `ApplyAsync`,
        `GetMyApplicationsAsync`, `GetMyResumesAsync`, `UploadFileAsync`,
        `GetMyNotificationsAsync`, `MarkNotificationReadAsync`…
  - [ ] Deserialização direta nos Response DTOs do `Monburan.Core`
  - [ ] Erros HTTP → mapear `ProblemDetails` para `Result`/exception tipada
- [ ] `AuthTokenService`
  - [ ] Salvar/ler access + refresh token em `SecureStorage` (Keychain/Keystore)
  - [ ] `GetValidTokenAsync()` — renova via `/api/auth/refreshToken` quando expirado
  - [ ] `ClearAsync()` no logout
- [ ] `AuthHeaderHandler : DelegatingHandler`
  - [ ] Injeta `Authorization: Bearer {token}` em toda request
  - [ ] Em 401: tenta refresh uma vez e repete a request; falhou → logout
- [ ] `MobileAuthStateProvider : AuthenticationStateProvider` (Blazor Hybrid)
  - [ ] Lê token do SecureStorage, notifica `NotifyAuthenticationStateChanged`
- [ ] Config por ambiente:
  - [ ] `ApiSettings` com `BaseUrl` (dev: `http://10.0.2.2:PORT` p/ emulador
        Android, `http://localhost` p/ iOS simulator; prod: HTTPS do VPS)
  - [ ] **Android dev:** liberar cleartext HTTP em `AndroidManifest.xml`
        (`usesCleartextTraffic`) ou usar dev certificate — **nunca em Release**

### 2.4 Telas do MVP (lado candidato)

Escopo consciente: **só candidato no MVP**; anunciante continua no site responsivo.

- [ ] `Components/Layout/` — MainLayout + NavMenu mobile (bottom nav ou drawer)
  - [ ] Reaproveitar CSS/paleta do `Monburan.Web/wwwroot` (copiar estilos-chave)
- [ ] `Pages/Login.razor` + `Pages/Register.razor` → `/api/auth/*`
- [ ] `Pages/Jobs.razor` — lista + busca + filtros (categoria/local/salário)
  → `GET /jobs` — **pública**
- [ ] `Pages/JobDetails.razor` → `GET /jobs/{id}` — **pública**
- [ ] `Pages/ApplyJob.razor` → `POST /applications` — **requer login**
- [ ] `Pages/MyApplications.razor` → `GET /applications/mine`
- [ ] `Pages/Profile.razor` → `GET/PUT /candidates/me`
- [ ] `Pages/Resumes.razor` + upload → `/resumes` + `/files/upload`
  - [ ] File picker nativo: `FilePicker.Default.PickAsync()`
- [ ] `Pages/Notifications.razor` → `/notifications/mine`
- [ ] Route guards: `[Authorize]`/`AuthorizeView` nas páginas protegidas;
      anônimo navega vagas livremente (parity com o site)
- [ ] Estados de UX: loading skeleton, lista vazia, erro de rede
  (`Connectivity.Current.NetworkAccess`), retry

### 2.5 Recursos do app

- [ ] `Resources/AppIcon/appicon.svg` + `Resources/Splash/splash.svg`
- [ ] Nome/Package ID: `ApplicationId` (ex.: `com.monburan.app`),
  `ApplicationDisplayName`, `ApplicationVersion`
- [ ] Localização pt-BR: reusar `.resx` do `Monburan.Core/Resources` onde couber
- [ ] Permissões Android: `INTERNET` (default MAUI já inclui)
- [ ] Tratamento de deep links (opcional pós-MVP)

---

## FASE 3 — Android (build + distribuição)

- [ ] Testar no emulador Android (Windows OK) + device físico via USB
- [ ] Gerar keystore de release:
  `keytool -genkey -v -keystore monburan.keystore -alias monburan -keyalg RSA -keysize 2048 -validity 10000`
  - [ ] ⚠️ Guardar keystore + senhas em cofre — perder = nunca mais atualizar o app
- [ ] Configurar assinatura no `Monburan.Mobile.csproj` (`Release`):
  `<AndroidSigningKeyStore>`, `<AndroidSigningKeyAlias>`, senhas via env/secrets
- [ ] Publicar AAB: `dotnet publish -f net10.0-android -c Release`
- [ ] Google Play Console (conta $25 única):
  - [ ] Criar app, preencher ficha (descrição, screenshots, ícone 512px,
        privacy policy URL — site já tem `/privacypolicy`)
  - [ ] Internal Testing → Closed Track → Production
  - [ ] Data Safety form + content rating

## FASE 4 — iOS (build + distribuição)

> ⚠️ **Bloqueante: build iOS exige macOS** (Xcode + certificados). Sem Mac, usar CI.

- [ ] Definir estratégia de build iOS:
  - [ ] Opção A — Mac físico na rede + "Pair to Mac" no Visual Studio Windows
  - [ ] Opção B — CI: GitHub Actions `macos-15` / Codemagic / App Center
- [ ] Apple Developer Program — conta $99/ano
- [ ] Criar App ID + capabilities no portal Apple
- [ ] Certificado de distribuição + provisioning profile
- [ ] `Info.plist`: nome, bundle id, descrições de permissão
  (NSPhotoLibraryUsageDescription se usar file picker de imagens)
- [ ] Publicar: `dotnet publish -f net10.0-ios -c Release` (no Mac/CI)
- [ ] App Store Connect → build → **TestFlight** (QA interno) → review → produção
- [ ] Screenshots + metadados + privacy policy URL

---

## FASE 5 — CI/CD mobile

- [ ] Estender pipeline GitHub Actions existente (`develop`/`main`):
  - [ ] Job Android (`ubuntu-latest` ou `windows-latest`):
        `dotnet workload install maui-android` → build AAB assinado → artifact
        → upload Play (fastlane supply ou action de terceiros)
  - [ ] Job iOS (`macos-15`): workload `maui-ios` → assinar → IPA →
        TestFlight via fastlane / `xcrun altool`
- [ ] Secrets no repositório: keystore (base64) + senhas, cert .p12 + senha,
  provisioning profile, App Store Connect API key, Google service account JSON
- [ ] Versionamento automático: `ApplicationDisplayVersion`/`ApplicationVersion`
  a partir do tag/run number do CI
- [ ] Gate: app só publica se `dotnet test` passar (testes de API + unit)

---

## FASE 6 — Pós-MVP (backlog)

- [ ] Push notifications: FCM (Android) + APNs (iOS)
  - [ ] Modelo de eventos já existe (`Notification` entity +
        `JobExpirationBackgroundService`) — falta o delivery push
  - [ ] Registrar device token no backend (`POST /api/v1/devices`)
- [ ] Lado anunciante no app: dashboard, gestão de candidaturas
  (endpoints da Fase 0.4 já estarão prontos)
- [ ] Cache offline leve: SQLite local para vagas já vistas
- [ ] PWA do site (manifest + service worker no WASM) — complemento barato
- [ ] Refactor de dívida consciente: extrair `Monburan.Infrastructure`
  (repos + DbContext) e `Monburan.Api` separado do `Monburan.Web`
  — só se a API crescer; hoje o mesmo deploy é mais simples
- [ ] Dark mode no app (site já tem padrão visual a seguir)
- [ ] Telemetria: App Center / Sentry para crash reports

---

## ✅ Critérios de aceite do MVP

- [ ] Login/registro funciona no app (token persistido, sessão sobrevive a restart)
- [ ] Anônimo lista e busca vagas; detalhe abre corretamente
- [ ] Usuário logado se candidata a uma vaga e vê em "Minhas candidaturas"
- [ ] Upload de currículo PDF pelo file picker funciona
- [ ] Notificações listadas e marcar-como-lida funciona
- [ ] Token expirado → refresh transparente (sem novo login)
- [ ] Sem internet → mensagem amigável + retry
- [ ] Build Release Android assinado gera AAB válido
- [ ] Build iOS sobe para TestFlight
- [ ] Nada do site quebrou — testes E2E Playwright continuam verdes

---

## 📊 Matriz de reaproveitamento (referência)

| Ativo | Destino | Esforço |
|---|---|---|
| `Monburan.Core` (UseCases, validadores) | chamados pelos endpoints | **Zero** |
| `RequestResult<T>` + `EResultStatus` | mapper `ToHttpResult` p/ códigos HTTP | **Pequeno** — 1 helper |
| DTOs Request/Response + Enums | contratos API + models do app | **Zero** |
| `ApplicationDbContext` + repositórios | já no DI do Web | **Zero** |
| `IdentityService` + `JwtOptions` (JWT pronto, DESLIGADO) | registrar no DI + `AddJwtBearer` + config | **Pequeno** — existe mas nunca foi plugado |
| Claims `candidate_id`/`advertiser_id`/`user_type` | já emitidas no JWT e no cookie | **Zero** |
| `ICurrentUserHelper` | funciona com bearer (claims) | **Zero** |
| `UploadFileUseCase` + `LocalStorageProvider` | endpoint multipart (IFormFile→Stream); validação já embutida | **Pequeno** |
| CSS Bootstrap do site | copiado pro app Hybrid | **Pequeno** |
| Páginas Razor (57) | **não reusa** — UI mobile nova | Alto se reusar (não recomendado) |
| Serilog / health checks / rate limit | já cobrem a API | **Zero** |
| Registro com perfil (Candidate/Advertiser) | `MapIdentityApi` NÃO cobre — endpoint próprio | **Médio** |

**Backend ~95% reaproveitado. O trabalho real está na UI do app + distribuição nas stores.**

---

## ❓ Decisões pendentes — informação que NÃO existe no código

Estes pontos não podem ser deduzidos do repositório — dependem de produto/negócio.
Responder antes de iniciar a Fase 2 (ou assumir o default indicado):

| # | Pergunta | Default assumido no plano |
|---|---|---|
| 1 | App cobre **só candidato** ou também anunciante? | Só candidato no MVP; anunciante na Fase 2 do app (endpoints já previstos) |
| 2 | Existe **Mac disponível** para build iOS? (físico, na rede, ou CI pago) | Sem Mac → GitHub Actions `macos-15` ou Codemagic |
| 3 | Contas **Google Play ($25)** e **Apple Developer ($99/ano)** já existem? Em nome de quem (pessoa/empresa)? | Criar na Fase 3/4 |
| 4 | **URL/domínio HTTPS de produção** da API — qual é? O deploy VPS já expõe HTTPS estável que o app pode apontar? | Confirmar — app exige HTTPS |
| 5 | **Bundle ID** (`com.empresa.monburan`), nome público do app, ícone/splash | `com.monburan.app` + "Monburan" — precisa de arte do ícone |
| 6 | Refresh token: stateless (só JWT assinado) ou persistido (revogável)? | Stateless no MVP (mais simples); persistido depois se precisar "logout em todos devices" |
| 7 | Password reset dentro do app ou link pro site? | Link pro site no MVP |
| 8 | Idiomas do app | pt-BR only (site já é pt-BR; resx en existe se precisar) |
| 9 | Versões mínimas: Android (API 24+?) / iOS (15+?) | Default do MAUI net10.0 |
| 10 | Modo offline / cache de vagas? | Não no MVP |
| 11 | Crash reporting / analytics no app? | Sentry/App Center — pós-MVP (Fase 6) |
| 12 | Notificações: hoje são **email (SendGrid)** — push real é trabalho novo (FCM/APNs). Vale a pena no MVP? | Não — Fase 6 |

---

## 🚀 Ordem de execução sugerida

1. **Fase 0 + 1** — API funcionando no Swagger/Postman (destrava tudo)
2. **Fase 2** — app rodando no emulador Android contra API local
3. **Fase 3** — Android na Play Store
4. **Fase 4** — iOS quando houver Mac/conta Apple
5. **Fase 5** — automatizar publicação
