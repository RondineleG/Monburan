# 🚀 Monburan

![CI](https://github.com/RondineleG/Monburan/actions/workflows/ci-cd.yml/badge.svg)
![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![Blazor](https://img.shields.io/badge/Blazor-WebAssembly%20%7C%20Server-purple)

> **Plataforma moderna de recrutamento e seleção para conectar talentos a oportunidades.**

Monburan é uma aplicação web completa para gestão de vagas, candidaturas e candidatos, construída com **.NET 10.0** e **Blazor WebAssembly/Server**. Oferece uma experiência intuitiva para anunciantes publicarem vagas e para candidatos se candidatarem de forma eficiente.

---

## 🎉 Status do Projeto

- ✅ **Pronto para produção com ressalvas**: pendências listadas em [IMPROVEMENTS.md](IMPROVEMENTS.md)
- ✅ **Segurança**: principais medidas implementadas (ver seção abaixo)
- ✅ **Escopo da Etapa 1**: implementado; tempo real (SignalR) previsto para a Etapa 2
- ✅ **CI/CD**: Pipeline configurado
- ✅ **Monitoramento**: Health checks + logging
- ✅ **Documentação**: Swagger/OpenAPI em desenvolvimento

📄 **[Veja as melhorias implementadas](IMPROVEMENTS.md)**

---

## 📋 Índice

- [✨ Features](#-features)
- [🛠 Stack Tecnológica](#-stack-tecnológica)
- [📦 Estrutura do Projeto](#-estrutura-do-projeto)
- [🔒 Segurança](#-segurança)
- [📊 Monitoramento](#-monitoramento)
- [🚀 Como Executar](#-como-executar)
- [📚 Documentação da API](#-documentação-da-api)
- [🔧 Configuração](#-configuração)
- [🐳 Docker](#-docker)
- [🤝 Contribuindo](#-contribuindo)
- [📄 Licença](#-licença)

---

## ✨ Features

### Para Anunciantes
- ✅ Criação e gestão completo de vagas de emprego
- ✅ Dashboard com métricas e analytics
- ✅ Gestão de candidaturas por vaga
- ✅ Moderação e aprovação de candidatos
- ✅ Destaque e boost de vagas (premium)
- ✅ Notificações na plataforma (tempo real via SignalR planejado para a Etapa 2)

### Para Candidatos
- ✅ Busca avançada de vagas por categoria, localização e salário
- ✅ Candidatura simplificada com um clique
- ✅ Upload e gestão de currículos
- ✅ Acompanhamento de status de candidaturas
- ✅ Perfil profissional completo

### Plataforma
- ✅ Autenticação e autorização com Identity + API REST com JWT Bearer
- ✅ Sistema de notificações por e-mail (SMTP via Resend; SendGrid como fallback)
- ✅ Upload de arquivos com storage local
- ✅ Multi-tenant: campo `TenantId` nas entidades (isolamento por tenant ainda não implementado)
- ✅ Design system próprio (dark) para web e mobile
- ✅ App Android (.NET MAUI Blazor Hybrid)
- ✅ Administração: contas (inativar, reativar, desbloquear, 2FA, papéis), moderação das vagas de todas as empresas, trilha de auditoria e provedores de IA
- ✅ Seed Data para desenvolvimento
- ✅ Migrations sob demanda (`APPLY_MIGRATIONS=true`); seed roda no boot em Development

---

## 🔒 Segurança

### Medidas de Segurança Implementadas
- ✅ **Senhas**: mínimo de 10 caracteres, lista de senhas comuns recusadas e **bloqueio da conta** após 5 erros (15 minutos)
- ✅ **Rate Limiting por IP**: 50 req/s no site todo e 20 envios/min em login, cadastro e recuperação de senha
- ✅ **Sessão**: cookies `Secure`/`HttpOnly` mesmo atrás do nginx, chaves de sessão persistidas (o deploy não desloga ninguém)
- ✅ **Security Headers**: CSP sem `unsafe-inline` nem `unsafe-eval` nos scripts, HSTS, X-Content-Type-Options, X-Frame-Options, etc.
- ✅ **Administração protegida**: 2FA obrigatório para administradores e trilha de auditoria de toda ação administrativa
- ✅ **VPS**: firewall só com SSH/80/443, SSH só por chave, fail2ban, atualizações automáticas e backup criptografado fora do servidor
- ✅ **Repositório**: Dependabot e CodeQL; scan de segredos no CI
- ✅ **Credenciais**: segredos de produção por variáveis de ambiente; defaults de desenvolvimento ficam só no `appsettings.Development.json`
- ✅ **Anti-Forgery**: Proteção CSRF habilitada
- ✅ **HTTPS Redirection**: Forçado em produção
- ✅ **Identity**: ASP.NET Core Identity com claims
- ✅ **Input Validation**: validação de entrada nos formulários e na API

Detalhes, configuração e passo a passo para produção: [docs/SEGURANCA.md](docs/SEGURANCA.md).

### Configuração de Segurança
```bash
# Obrigatórias para produção
export ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=..."
export Jwt__SigningKey="$(openssl rand -base64 48)"   # obrigatório — app falha no boot sem ele
# Recomendadas: e-mail (SMTP — Resend em produção; sem Smtp__Host cai no SendGrid)
export Smtp__Host="smtp.resend.com"
export Smtp__User="resend"
export Smtp__Password="sua-api-key-resend"
```

---

## 📊 Monitoramento

### Health Checks
- **Liveness** — `/health` e `/health/live`: o app está no ar. Não dependem do banco, para o deploy e o orquestrador não reiniciarem o contêiner porque o Postgres caiu. `/health` mantém o corpo de texto simples (`Healthy`), de que o `deploy.sh` e o nginx dependem.
- **Readiness** — `/health/ready`: o app está pronto para receber tráfego. Verifica o Postgres e devolve JSON com o estado de cada checagem (503 quando o banco não responde).

### Logging Estruturado
- **Framework**: Serilog
- **Output**: Console + Arquivo (rotação diária)
- **Nível**: Information em todos os ambientes; `Microsoft` e `System` em Warning
- **Contexto**: Propriedades da aplicação enriquecidas

### CI/CD Pipeline
- **Plataforma**: GitHub Actions
- **Workflows**:
  - `ci-cd.yml` → build + testes (unit, integração e API, com coverage), E2E (Playwright + Postgres de serviço, só em PR para a `main`), scan de segredos e de pacotes NuGet vulneráveis; deploy de produção no Azure (`main`) só quando o Azure está configurado
  - `docker-ghcr.yml` → build e push da imagem Docker para `ghcr.io/rondineleg/monburan` a cada push na `main`; em seguida, o job `deploy-vps` envia `infra/vps` e roda o `deploy.sh` na VPS (backup, rollback e espera do `/health`)
  - `android-release.yml` → APK release assinado a partir da `main`; publica a release `v<versão>` quando a versão do csproj é nova
  - `ios-build.yml` → em macOS: build do app para o simulador (com print) e `.ipa` sem assinatura anexado à release; sem conta Apple Developer não há TestFlight nem App Store
- **Features**: Build, testes, security scan, health checks, artefatos (app + APK)
- **Deploy VPS**: imagem do GHCR + `infra/vps` (docker compose + nginx + TLS via certbot) em `https://monburan.com.br`
- **Como lançar uma versão e publicar a web**: [docs/CI-CD.md](docs/CI-CD.md)

---

## 🛠 Stack Tecnológica

| Camada | Tecnologia |
|--------|------------|
| **Framework** | .NET 10.0 (ASP.NET Core) |
| **Frontend** | Blazor WebAssembly + Server (Interactive Render Modes) |
| **UI** | Design system próprio (dark) — folhas em `src/Monburan.Web/wwwroot` (ex.: `tokens.css`, `components.css`) |
| **Mobile** | .NET MAUI Blazor Hybrid (Android) |
| **ORM** | Entity Framework Core 10.0 |
| **Database** | PostgreSQL (Npgsql) |
| **Auth** | ASP.NET Core Identity + JWT Bearer (API) |
| **Email** | SMTP via MailKit (Resend em produção) / SendGrid API (fallback) |
| **Storage** | Local File System (extensível para cloud) |
| **Arquitetura** | Clean Architecture com Use Cases |
| **Validação** | Validadores próprios em `src/Monburan.Core/Validations` |
| **Containerização** | Docker & Docker Compose; imagem publicada no GHCR |
| **Reverse proxy** | nginx + TLS (certbot) na VPS |

---

## 📦 Estrutura do Projeto

```
Monburan/
├── src/
│   ├── Monburan.Core/          # Camada de domínio
│   │   ├── Entities/           # Entidades do domínio
│   │   ├── DTOs/               # Request/Response DTOs
│   │   ├── Enums/              # Enumerações
│   │   ├── Exceptions/         # Exceções customizadas
│   │   ├── Extensions/         # Extension methods
│   │   ├── Interfaces/         # Contratos de serviços/repos
│   │   ├── Requests/           # Request objects
│   │   ├── Resources/          # Recursos de localização
│   │   ├── UseCases/           # Casos de uso (Clean Architecture)
│   │   ├── Validations/        # Validadores de entidades
│   │   └── ValueObjects/       # Value Objects
│   │
│   ├── Monburan.Web/           # Camada de apresentação + API REST
│   │   ├── Components/         # Componentes Blazor
│   │   │   └── Pages/          # Páginas da aplicação
│   │   ├── Endpoints/          # API REST (/api/v1 + /api/auth) para o app mobile
│   │   ├── Data/               # DbContext e Seed Data
│   │   ├── Middleware/         # Security headers e rate limiting
│   │   ├── Migrations/         # Migrations do EF Core
│   │   ├── Repositories/       # Implementações de repositórios
│   │   ├── Services/           # Serviços (e-mail, JWT, storage, background)
│   │   ├── UseCases/           # Casos de uso específicos da Web
│   │   ├── wwwroot/            # Arquivos estáticos
│   │   ├── Program.cs          # Entry point
│   │   └── appsettings.json    # Configurações
│   │
│   ├── Monburan.Web.Client/    # Client Blazor WebAssembly
│   │   └── UserInfo.cs         # Modelo de usuário client
│   │
│   ├── Monburan.Mobile.Shared/ # Camada compartilhada do app
│   │   ├── Api/                # MonburanApiClient, AuthApiClient, ApiException
│   │   └── Auth/               # TokenService, AuthHeaderHandler, AuthStateService
│   │
│   └── Monburan.Mobile/        # App .NET MAUI Blazor Hybrid (Android/iOS)
│       ├── Components/         # Páginas e layout Razor
│       ├── Services/           # SecureStorageTokenStore, MobileApiConfig
│       └── Platforms/          # AndroidManifest, network_security_config
│
├── tests/
│   ├── Monburan.UnitTests/       # Testes unitários (xUnit + NSubstitute)
│   ├── Monburan.IntegrationTests/# Testes de repositórios (EF InMemory)
│   ├── Monburan.ApiTests/        # Testes da API REST (WebApplicationFactory)
│   └── Monburan.E2ETests/        # Testes E2E (Playwright)
│
├── infra/vps/                  # Deploy de produção na VPS
│   ├── docker-compose.prod.yml # Stack app + Postgres (imagem do GHCR)
│   ├── nginx.conf              # Reverse proxy + TLS (certbot)
│   ├── provision.sh            # Prepara um servidor novo: Docker, nginx, certbot, age, rclone, swap
│   ├── deploy.sh               # Sobe/atualiza a stack e o nginx; na 1ª vez cria o .env e emite o certificado
│   ├── harden.sh               # Endurece a VPS: firewall, SSH por chave, fail2ban, atualizações
│   ├── validate.sh             # Valida o servidor, só lendo: de fora (HTTPS, cabeçalhos, cookies) e por dentro (disco, SSH, firewall, backup)
│   ├── backup.sh               # Backup criptografado (age) do banco e dos uploads, enviado com rclone
│   ├── restore-test.sh         # Restaura o backup em um Postgres descartável e confere os dados
│   ├── systemd/                # Timer diário do backup
│   ├── tests/                  # Testes dos scripts acima, em contêineres (CI: infra-scripts.yml)
│   ├── backup.env.example      # Configuração do backup
│   └── env.example             # Variáveis de produção
│
├── .github/                    # Workflows do GitHub Actions (ver .github/workflows/README.md)
├── docs/                       # Documentação: segurança, CI/CD, IA, Docker
├── Monburan.sln                # Solução (inclui o app MAUI, que exige o workload Android)
├── docker-compose.yml          # Orquestração Docker (produção local)
├── docker-compose.dev.yml      # Orquestração Docker (desenvolvimento)
├── .env.example                # Variáveis de ambiente (template)
├── .gitignore
└── README.md
```

---

## 🚀 Como Executar

### Pré-requisitos
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL](https://www.postgresql.org/download/) (ou via `docker compose up -d postgres`)
- CLI do EF Core para migrations: `dotnet tool install --global dotnet-ef`
- (Opcional) [Docker](https://www.docker.com/products/docker-desktop)
- (Opcional) workload MAUI Android para build do app mobile: `dotnet workload install maui-android`

### Modo Desenvolvimento

```bash
# 1. Clonar o repositório
git clone https://github.com/RondineleG/Monburan.git
cd Monburan

# 2. Subir o PostgreSQL (Docker) — ou use uma instância local.
#    O compose cria o banco `monburan` por padrão; o appsettings.Development usa `monburan_dev`.
#    Para alinhar, defina DB_NAME=monburan_dev no .env antes de subir.
docker compose up -d postgres

# 3. Restaurar pacotes
dotnet restore

# 4. Aplicar as migrations (o seed roda no boot em Development)
dotnet ef database update --project src/Monburan.Web
# ou, na execução: APPLY_MIGRATIONS=true dotnet run --project src/Monburan.Web

# 5. Executar a aplicação
dotnet run --project src/Monburan.Web

# 6. Acessar
# HTTP: http://localhost:5095
# Swagger (dev): http://localhost:5095/swagger
```

### Modo Produção

```bash
# 1. Build de produção
dotnet publish src/Monburan.Web -c Release -o ./publish

# 2. Configurar variáveis de ambiente
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=..."
export Jwt__SigningKey="$(openssl rand -base64 48)"
export Smtp__Host="smtp.resend.com" Smtp__User="resend" Smtp__Password="sua-chave"

# 3. Executar
./publish/Monburan.Web
```

---

## 📚 Documentação da API

### Swagger/OpenAPI
- **Endpoint**: `/swagger` (desenvolvimento)
- **Framework**: Swashbuckle.AspNetCore
- **Versão**: v1
- **Features**: Documentação interativa, testes de endpoints, schemas

### Páginas e API

As páginas públicas (`/`, `/jobs`, `/jobs/{id}`) funcionam sem login; as áreas do
anunciante e do candidato exigem autenticação por cookie. A API REST para o app
mobile fica sob `/api/auth/*` e `/api/v1/*` — detalhada na seção
[API REST + App Mobile](#-api-rest--app-mobile).

---

## 📱 API REST + App Mobile

A camada API REST (JWT Bearer) fica em `src/Monburan.Web/Endpoints` e serve o app
`src/Monburan.Mobile` (.NET MAUI Blazor Hybrid, `com.monburan.app`).

### Autenticação (cookies no site, bearer na API)

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `POST` | `/api/auth/login` | `{email, password}` → `{accessToken, refreshToken}` |
| `POST` | `/api/auth/refresh` | `{refreshToken}` → novo par de tokens |
| `POST` | `/api/auth/register` | `{accountType: candidate\|advertiser, name, email, password, confirmPassword}` → tokens |
| `POST` | `/api/auth/forgot-password` | `{email}` → e-mail de reset |

Claims emitidas: `sub`, `candidate_id`/`advertiser_id`, `user_type`.
As rotas `/api/v1/**` exigem `Authorization: Bearer` (policy `ApiBearer`), exceto as marcadas
como anônimas nas tabelas abaixo; `/api/auth/*` é anônimo. As respostas são ProblemDetails —
nunca redirecionam para login em cookie.

### Endpoints `/api/v1`

| Método | Endpoint | Auth | Descrição |
|--------|----------|------|-----------|
| `GET` | `/api/v1/jobs` | anon | Lista paginada (`page,size,searchTerm,city,state,isRemote,type,salaryMin/Max,category,contractType,salaryPeriod,publishedWithinDays,sortBy,sortDescending`) |
| `GET` | `/api/v1/jobs/{id}` | anon | Detalhes da vaga |
| `GET` | `/api/v1/jobs/recommended`, `/api/v1/jobs/saved` | candidate | Vagas recomendadas e salvas |
| `PUT`/`DELETE` | `/api/v1/jobs/{id}/save` | candidate | Salvar / remover vaga salva |
| `POST` | `/api/v1/jobs/{id}/views` | anon | Registra visualização da vaga |
| `POST` | `/api/v1/jobs` | advertiser | Criar vaga |
| `GET` | `/api/v1/jobs/mine` | advertiser | Vagas do anunciante |
| `PUT`/`DELETE` | `/api/v1/jobs/{id}` | advertiser | Editar / excluir vaga do anunciante |
| `POST` | `/api/v1/applications` | candidate | Candidatar-se (`candidateId` do body é ignorado — usa a claim) |
| `GET` | `/api/v1/applications/mine` | candidate | Minhas candidaturas |
| `GET` | `/api/v1/applications/{id}/history` | bearer | Histórico de status da candidatura |
| `GET` | `/api/v1/jobs/{jobId}/applications` | advertiser | Candidaturas recebidas para a vaga |
| `PUT` | `/api/v1/applications/{id}/status` | advertiser | Atualiza o status da candidatura |
| `GET`/`POST`/`PUT`/`DELETE` | `/api/v1/job-alerts/**` | candidate | Alertas de vagas |
| `GET`/`PUT` | `/api/v1/candidates/me` | candidate | Perfil do candidato |
| `GET`/`PUT` | `/api/v1/candidates/me/career-profile` | candidate | Perfil profissional |
| `GET` | `/api/v1/candidates/me/progress` | candidate | Progresso do perfil |
| `GET`/`POST`/`PUT`/`DELETE` | `/api/v1/resumes/**` | candidate | Currículos (`/mine`, `/{id}`) |
| `GET` | `/api/v1/resumes/improvements/availability` | candidate | Se as sugestões de IA estão disponíveis ao candidato |
| `POST` | `/api/v1/resumes/{id}/improvements` | candidate | Sugestões de melhoria com IA (plano Premium) |
| `POST` | `/api/v1/files/upload?fileType=Pdf` | bearer | Multipart `file` → `fileUrl` (imagem ≤5MB, PDF ≤10MB) |
| `GET` | `/api/v1/notifications/mine` | bearer | Notificações |
| `PUT` | `/api/v1/notifications/mine/read-all` | bearer | Marca todas como lidas |
| `PUT` | `/api/v1/notifications/{id}/read` | bearer | Marca uma como lida |
| `GET` | `/api/v1/notifications/mine/unread-count` | bearer | Contagem de não lidas (badge) |
| `GET`/`PUT` | `/api/v1/advertisers/me` | advertiser | Perfil da empresa |
| `POST` | `/api/v1/advertisers/logo` | advertiser | Upload de logo (multipart `file`) + atualiza perfil |
| `GET` | `/api/v1/dashboard` | advertiser | Métricas |
| `GET` | `/api/v1/plans` | anon | Planos (com assinatura atual se anunciante logado) |
| `POST` | `/api/v1/subscriptions/upgrade`, `/api/v1/subscriptions/cancel` | advertiser | Troca e cancelamento de plano |

Lista completa e schemas: Swagger em desenvolvimento (`/swagger`).

### JWT config

```json
"Jwt": {
  "Issuer": "monburan.api",
  "Audience": "monburan.app",
  "AccessTokenExpiration": 3600,       // segundos
  "RefreshTokenExpiration": 2592000,   // segundos (30 dias)
  "SigningKey": ""                     // produção: JWT_SIGNING_KEY / Jwt__SigningKey
}
```

A app falha no boot sem `Jwt__SigningKey` (`ValidateOnStart`). Em produção:
`docker-compose.yml` exige `JWT_SIGNING_KEY` no `.env` e `infra/vps/deploy.sh`
configura a stack Docker (imagem do GHCR) com os segredos no `.env` da VPS
(chmod 600). Gere com `openssl rand -base64 48`. No Azure App Service, provisione o app setting
`Jwt__SigningKey`. O job `deploy-production` do CI exige `AZURE_CREDENTIALS` e
`AZURE_RESOURCE_GROUP`; `JWT_SIGNING_KEY` é opcional, para provisionar a chave no App Service.
Sem a chave, o deploy falha cedo em vez de derrubar a produção.

`App__PublicBaseUrl` (ex.: `https://monburan.com.br`) é opcional mas recomendado em
produção: os links de confirmação de e-mail e reset de senha usam essa base. Sem ela, a
app usa `scheme://host` da request — atrás de proxy TLS sem `ForwardedHeaders` os links
saem como `http://interno`.

### Rodando o app mobile

Pré-requisito: o workload MAUI Android é necessário inclusive para
`dotnet restore`/`dotnet build` da solution:

```bash
dotnet workload install maui-android
```

```bash
# 1. API de pé (Postgres via docker compose + migrations/seed)
docker compose up -d
APPLY_MIGRATIONS=true dotnet run --project src/Monburan.Web

# 2. Emulador OU device físico (USB): expor a API local ao app
adb reverse tcp:5095 tcp:5095   # app usa http://127.0.0.1:5095 em Debug — funciona
                                # em emulador e em device físico com depuração USB.
                                # Nota: o network_security_config de Debug só libera
                                # cleartext para 127.0.0.1/localhost/10.0.2.2, então
                                # http://<IP-da-LAN>:5095 NÃO funciona no device físico
                                # — use adb reverse (ou ajuste MobileApiConfig + config).

# 3. Build + deploy
dotnet build src/Monburan.Mobile -f net10.0-android -c Debug -t:Run
# ou gerar APK autocontido:
dotnet build src/Monburan.Mobile -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true

# 4. Release APK assinado (a esteira faz isso na main: ver docs/CI-CD.md)
dotnet publish src/Monburan.Mobile -f net10.0-android -c Release -p:AndroidPackageFormat=apk \
  -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=<keystore> -p:AndroidSigningKeyAlias=<alias> \
  -p:AndroidSigningStorePass=env:KEYSTORE_PASSWORD -p:AndroidSigningKeyPass=env:KEY_PASSWORD
```

Estrutura mobile:
- `src/Monburan.Mobile.Shared` — `MonburanApiClient`, `AuthApiClient`, `TokenService`
  (refresh transparente em 401), `AuthHeaderHandler`, `AuthStateService`, `ITokenStore`
- `src/Monburan.Mobile` — app MAUI Blazor Hybrid; `SecureStorageTokenStore` persiste a
  sessão; `network_security_config.xml` permite cleartext só para localhost/emulador

Testes de API: `tests/Monburan.ApiTests` (WebApplicationFactory + EF InMemory).

### Modelos de Dados

#### Job (Vaga)
```json
{
  "id": "guid",
  "title": "Desenvolvedor Full Stack",
  "description": "...",
  "requirements": ".NET, React, SQL",
  "city": "São Paulo",
  "state": "SP",
  "type": "Hybrid",
  "isRemote": false,
  "salaryMin": 8000,
  "salaryMax": 12000,
  "salaryDisplay": true,
  "status": "Active",
  "expirationDate": "2026-06-01T00:00:00Z"
}
```

#### Candidate (Candidato)
```json
{
  "id": "guid",
  "name": "João Silva",
  "email": "joao@email.com",
  "phone": "(11) 97777-7777",
  "linkedIn": "https://linkedin.com/in/joaosilva",
  "status": "Active"
}
```

---

## 🔧 Configuração

### Arquivos de Configuração
- `appsettings.json` - Configuração base (sem credenciais)
- `appsettings.Development.json` - Configuração de desenvolvimento (PostgreSQL local + chave JWT de dev)
- `appsettings.Production.json.example` - Template para produção

### appsettings.json (trecho)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""  // Use variáveis de ambiente
  },
  "SendGrid": {
    "ApiKey": "",  // Fallback caso Smtp:Host não esteja configurado
    "FromEmail": "noreply@monburan.com.br",
    "FromName": "Monburan"
  },
  "Jwt": {
    "Issuer": "monburan.api",
    "Audience": "monburan.app",
    "AccessTokenExpiration": 3600,
    "RefreshTokenExpiration": 2592000,
    "SigningKey": ""  // obrigatório — configure via env/User Secrets
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": { "Microsoft": "Warning", "System": "Warning" }
    },
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/monburan-.log", "rollingInterval": "Day" } }
    ]
  }
}
```

> **E-mail:** quando `Smtp:Host` está configurado (env `Smtp__Host`, `Smtp__User`,
> `Smtp__Password`, `Smtp__Port`, `Smtp__FromEmail`, `Smtp__FromName`), o envio é
> feito via SMTP — em produção usamos o **Resend** (`smtp.resend.com:465`,
> usuário `resend`, senha = API key). Sem `Smtp:Host`, cai no SendGrid.

> **IA (sugestões de melhoria de currículo):** o administrador cadastra os provedores (OpenAI, OpenRouter, DeepSeek,
> Anthropic, Xiaomi ou qualquer serviço compatível) na tela **Provedores de IA** (`/admin/ai-providers`), escolhe o padrão e
> informa o nome da variável de ambiente da chave (`AI_KEY_*`). A chave fica só no servidor:
> `dotnet user-secrets set "AI_KEY_OPENROUTER" "<chave>" --project src/Monburan.Web` em desenvolvimento e
> `ai-keys.env` na VPS. Sem provedor padrão e chave, o recurso fica desligado. Guia da tela, segurança e erros em
> [docs/IA-PROVEDORES.md](docs/IA-PROVEDORES.md); privacidade e limites em [docs/IA-CURRICULO.md](docs/IA-CURRICULO.md).

### User Secrets (Desenvolvimento)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=monburan_dev;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "SigningKey": "dev-only-signing-key-change-me-7f3a9c1e5b8d4f6a"
  },
  "SendGrid": {
    "ApiKey": "",
    "FromEmail": "noreply@monburan.com.br",
    "FromName": "Monburan"
  },
  "AllowedHosts": "*"
}
```

```bash
# Configurar User Secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=monburan_dev;Username=postgres;Password=postgres"
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
dotnet user-secrets set "Smtp:Password" "sua-api-key-resend"
```

> ⚠️ **Nunca commite secrets no repositório!** Use User Secrets em dev e variáveis de ambiente em produção.

---

## 🐳 Docker

### Com Docker Compose

```bash
# Subir todos os serviços (PostgreSQL + app)
docker compose up -d

# Só o banco (desenvolvimento local com dotnet run)
docker compose up -d postgres

# Ver logs
docker compose logs -f app

# Parar
docker compose down
```

### Build da Imagem

```bash
docker build -t monburan:latest -f src/Monburan.Web/Dockerfile .
```

### Imagem no GHCR

O workflow `docker-ghcr.yml` publica `ghcr.io/rondineleg/monburan:latest` (e tag
por SHA) a cada push na `main` e toda segunda (`rebuild-semanal.yml`, para os
patches das imagens base) e, em seguida, faz o deploy na VPS (job `deploy-vps`).
Só a `main` publica.

### Deploy na VPS (produção)

`infra/vps/` contém a stack de produção para `https://monburan.com.br`:

- `deploy.sh` — instala nginx + certbot, sobe app + Postgres (imagem do GHCR)
  e emite o certificado TLS
- `docker-compose.prod.yml` — app exposto só em `127.0.0.1:8080`, Postgres
  interno, `APPLY_MIGRATIONS=true`, SMTP Resend via env
- `nginx.conf` — proxy reverso com WebSocket (Blazor Server) e redirect 80→443
- `env.example` — `DB_*`, `JWT_SIGNING_KEY`, `APP_PUBLIC_BASE_URL`, `SMTP_*`

Detalhes completos em [DOCKER.md](DOCKER.md).

---

## 🤝 Contribuindo

Contribuições são bem-vindas! Siga os passos abaixo:

1. **Fork** o projeto
2. Crie uma branch para sua feature (`git checkout -b feature/AmazingFeature`)
3. **Commit** suas mudanças (`git commit -m 'Add: AmazingFeature'`)
4. **Push** para a branch (`git push origin feature/AmazingFeature`)
5. Abra um **Pull Request**

### Padrões de Código
- Siga as convenções do arquivo `.editorconfig`
- Use `nullable enable` em todos os arquivos
- Prefira `sealed` em classes de serviço
- Use `IDateTimeProvider` ao invés de `DateTime.Now`
- Logging via `ILogger<T>`, nunca `Console.Write`

---

## 📄 Licença

Este projeto está licenciado sob a [MIT License](LICENSE).

```
MIT License

Copyright (c) 2026 Monburan

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 📞 Contato

- **Website**: [https://monburan.com.br](https://monburan.com.br)
- **Email**: contato@monburan.com.br
- **LinkedIn**: [Monburan](https://linkedin.com/company/monburan)

---

Feito com ❤️ por [Equipe Monburan](https://github.com/RondineleG)
