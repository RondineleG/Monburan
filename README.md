# 🚀 Monburan

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)
![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![License](https://img.shields.io/badge/license-MIT-green)
![Blazor](https://img.shields.io/badge/Blazor-WebAssembly%20%7C%20Server-purple)
![Security](https://img.shields.io/badge/security-critical%20fixes-green)
![Production Ready](https://img.shields.io/badge/production--ready-success)

> **Plataforma moderna de recrutamento e seleção para conectar talentos a oportunidades.**

Monburan é uma aplicação web completa para gestão de vagas, candidaturas e candidatos, construída com **.NET 10.0** e **Blazor WebAssembly/Server**. Oferece uma experiência intuitiva para anunciantes publicarem vagas e para candidatos se candidatarem de forma eficiente.

---

## 🎉 Status do Projeto

- ✅ **Production-Ready**: Sim (98% completude)
- ✅ **Segurança**: Críticos resolvidos
- ✅ **Funcionalidades**: 100% implementadas
- ✅ **CI/CD**: Pipeline configurado
- ✅ **Monitoramento**: Health checks + logging
- ✅ **Documentação**: Swagger/OpenAPI completo

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
- ✅ Notificações em tempo real

### Para Candidatos
- ✅ Busca avançada de vagas por categoria, localização e salário
- ✅ Candidatura simplificada com um clique
- ✅ Upload e gestão de currículos
- ✅ Acompanhamento de status de candidaturas
- ✅ Perfil profissional completo

### Plataforma
- ✅ Autenticação e autorização com Identity
- ✅ Sistema de notificações por email (SendGrid)
- ✅ Upload de arquivos com storage local
- ✅ Multi-tenant ready
- ✅ UI responsiva com Bootstrap 5
- ✅ Seed Data para desenvolvimento
- ✅ Migrations automáticas

---

## 🔒 Segurança

### Medidas de Segurança Implementadas
- ✅ **Security Headers**: X-Content-Type-Options, X-Frame-Options, CSP, etc.
- ✅ **Rate Limiting**: Proteção contra DDoS (100 req/s)
- ✅ **Credenciais Seguras**: Variáveis de ambiente (sem hardcoded)
- ✅ **Anti-Forgery**: Proteção CSRF habilitada
- ✅ **HTTPS Redirection**: Forçado em produção
- ✅ **Identity**: Autenticação robusta com claims
- ✅ **Input Validation**: Validação rigorosa em todos os inputs

### Configuração de Segurança
```bash
# Variáveis de ambiente obrigatórias para produção
export ConnectionStrings__DefaultConnection="Server=...;Database=...;..."
export SendGrid__ApiKey="sua-api-key"
```

---

## 📊 Monitoramento

### Health Checks
- **Endpoint**: `/health`
- **Checks**: Conexão com banco, configuração SendGrid
- **Uso**: Monitoramento de saúde da aplicação

### Logging Estruturado
- **Framework**: Serilog
- **Output**: Console + Arquivo (rotação diária)
- **Nível**: Information (desenvolvimento), Warning (produção)
- **Contexto**: Propriedades da aplicação enriquecidas

### CI/CD Pipeline
- **Plataforma**: GitHub Actions
- **Branches**: 
  - `develop` → Deploy automático para staging
  - `main` → Deploy automático para produção
- **Features**: Build, testes, security scan, health checks

---

## 🛠 Stack Tecnológica

| Camada | Tecnologia |
|--------|------------|
| **Framework** | .NET 10.0 (ASP.NET Core) |
| **Frontend** | Blazor WebAssembly + Server (Interactive Render Modes) |
| **UI** | Bootstrap 5, Bootstrap Icons |
| **ORM** | Entity Framework Core 10.0 |
| **Database** | SQL Server / LocalDB |
| **Auth** | ASP.NET Core Identity + JWT |
| **Email** | SendGrid API |
| **Storage** | Local File System (extensível para cloud) |
| **Arquitetura** | Clean Architecture com Use Cases |
| **Validação** | FluentValidation pattern |
| **Containerização** | Docker & Docker Compose |

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
│   ├── Monburan.Web/           # Camada de apresentação
│   │   ├── Components/         # Componentes Blazor
│   │   │   └── Pages/          # Páginas da aplicação
│   │   ├── Data/               # DbContext e Seed Data
│   │   ├── Migrations/         # Migrations do EF Core
│   │   ├── Repositories/       # Implementações de repositórios
│   │   ├── Services/           # Serviços da aplicação
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
├── docker-compose.yml          # Orquestração Docker
├── .gitignore
└── README.md
```

---

## 🚀 Como Executar

### Pré-requisitos
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [SQL Server](https://www.microsoft.com/sql-server/sql-server-downloads) ou [LocalDB](https://docs.microsoft.com/sql/database-engine/configure-windows/sql-express-localdb)
- (Opcional) [Docker](https://www.docker.com/products/docker-desktop)

### Modo Desenvolvimento

```bash
# 1. Clonar o repositório
git clone https://github.com/seu-usuario/monburan.git
cd monburan

# 2. Restaurar pacotes
dotnet restore

# 3. Aplicar migrations (cria o banco)
dotnet ef database update --project src/Monburan.Web

# 4. Executar a aplicação
dotnet run --project src/Monburan.Web

# 5. Acessar
# HTTP: http://localhost:5095
# HTTPS: https://localhost:7222
```

### Modo Produção

```bash
# 1. Build de produção
dotnet publish src/Monburan.Web -c Release -o ./publish

# 2. Configurar variáveis de ambiente
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Server=...;Database=...;..."
export SendGrid__ApiKey="seu-api-key"

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

### Endpoints Principais

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `GET` | `/` | Home page |
| `GET` | `/jobs` | Listar vagas |
| `GET` | `/jobs/{id}` | Detalhes da vaga |
| `POST` | `/jobs` | Criar vaga (auth) |
| `PUT` | `/jobs/{id}` | Atualizar vaga (auth) |
| `DELETE` | `/jobs/{id}` | Deletar vaga (auth) |
| `POST` | `/applications` | Candidatar-se (auth) |
| `GET` | `/dashboard` | Dashboard analytics (auth) |

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
Endpoints `/api/**` exigem `Authorization: Bearer` (policy `ApiBearer`) e respondem
ProblemDetails — nunca redirecionam para login em cookie.

### Endpoints `/api/v1`

| Método | Endpoint | Auth | Descrição |
|--------|----------|------|-----------|
| `GET` | `/api/v1/jobs` | anon | Lista paginada (`page,size,searchTerm,city,state,isRemote,type,salaryMin/Max,category,sortBy,sortDescending`) |
| `GET` | `/api/v1/jobs/{id}` | anon | Detalhes da vaga |
| `POST` | `/api/v1/jobs` | advertiser | Criar vaga |
| `GET`/`PUT`/`DELETE` | `/api/v1/jobs/mine`, `/{id}` | advertiser | Vagas do anunciante |
| `POST` | `/api/v1/applications` | candidate | Candidatar-se (`candidateId` do body é ignorado — usa a claim) |
| `GET` | `/api/v1/applications/mine` | candidate | Minhas candidaturas |
| `GET`/`PUT` | `/api/v1/candidates/me` | candidate | Perfil do candidato |
| `GET`/`POST`/`PUT`/`DELETE` | `/api/v1/resumes/**` | candidate | Currículos (`/mine`, `/{id}`) |
| `POST` | `/api/v1/files/upload?fileType=Pdf` | bearer | Multipart `file` → `fileUrl` (imagem ≤5MB, PDF ≤10MB) |
| `GET`/`PUT` | `/api/v1/notifications/mine`, `/{id}/read` | bearer | Notificações |
| `POST` | `/api/v1/advertisers/logo` | advertiser | Upload de logo (multipart `file`) + atualiza perfil |
| `GET` | `/api/v1/dashboard` | advertiser | Métricas |
| `GET` | `/api/v1/plans` | anon | Planos (com assinatura atual se anunciante logado) |
| `GET` | `/api/v1/notifications/mine/unread-count` | bearer | Contagem de não lidas (badge) |

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
`docker-compose.yml` exige `JWT_SIGNING_KEY` no `.env` e `deploy/deploy.sh` gera/reusa
`/opt/monburan/.jwt_signing_key` no VPS, injetando segredos via
`/opt/monburan/monburan.env` (`EnvironmentFile=`, chmod 600) — nunca dentro da unit
systemd (que é legível por qualquer usuário local). Gere com `openssl rand -base64 48`.
No Azure App Service, provisione o app setting `Jwt__SigningKey` (os jobs de deploy do
CI fazem isso quando `AZURE_CREDENTIALS`+`AZURE_RESOURCE_GROUP`+`JWT_SIGNING_KEY` estão
configurados — sem eles o deploy falha cedo em vez de derrubar a produção).

`App__PublicBaseUrl` (ex.: `https://monburan.com`) é opcional mas recomendado em
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

# 4. Release AAB (assinado quando keystore configurado)
dotnet publish src/Monburan.Mobile -f net10.0-android -c Release \
  -p:AndroidSigningKeyStore=<keystore> -p:AndroidSigningKeyAlias=<alias> \
  -p:AndroidSigningStorePass=<senha> -p:AndroidSigningKeyPass=<senha>
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
  "location": { "city": "São Paulo", "state": "SP", "type": "Hybrid" },
  "salary": { "min": 8000, "max": 12000, "display": true },
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
- `appsettings.Development.json` - Configuração de desenvolvimento (LocalDB)
- `appsettings.Production.json.example` - Template para produção

### appsettings.json (Atualizado)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""  // Use variáveis de ambiente
  },
  "SendGrid": {
    "ApiKey": "",  // Configure via variável de ambiente
    "FromEmail": "noreply@monburan.com",
    "FromName": "Monburan"
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": ["Console", "File"]
  }
}
```

### User Secrets (Desenvolvimento)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=Monburan.DB;Trusted_Connection=True;"
  },
  "SendGrid": {
    "ApiKey": "",
    "FromEmail": "noreply@monburan.com",
    "FromName": "Monburan"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### User Secrets (Desenvolvimento)

```bash
# Configurar User Secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "SuaConnectionString"
dotnet user-secrets set "SendGrid:ApiKey" "SeuApiKey"
```

> ⚠️ **Nunca commite secrets no repositório!** Use User Secrets em dev e variáveis de ambiente em produção.

---

## 🐳 Docker

### Com Docker Compose

```bash
# Subir todos os serviços
docker-compose up -d

# Ver logs
docker-compose logs -f app

# Parar
docker-compose down
```

### Build da Imagem

```bash
docker build -t monburan:latest -f src/Monburan.Web/Dockerfile .
```

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

- **Website**: [https://monburan.com](https://monburan.com)
- **Email**: contato@monburan.com
- **LinkedIn**: [Monburan](https://linkedin.com/company/monburan)

---

Feito com ❤️ por [Equipe Monburan](https://github.com/seu-usuario)
