# API REST

A API REST (JWT Bearer) fica em `src/Monburan.Web/Endpoints` e atende o aplicativo Android (`src/Monburan.Mobile`, `com.monburan.app`). O Swagger está disponível em `/swagger` no ambiente de desenvolvimento.

As páginas públicas (`/`, `/jobs`, `/jobs/{id}`) funcionam sem login. As áreas do anunciante e do candidato usam autenticação por cookie no site. A API para o aplicativo fica em `/api/auth/*` e `/api/v1/*`.

## Autenticação

| Método | Endpoint | Descrição |
|--------|----------|-----------|
| `POST` | `/api/auth/login` | `{email, password}` → `{accessToken, refreshToken}` |
| `POST` | `/api/auth/refresh` | `{refreshToken}` → novo par de tokens |
| `POST` | `/api/auth/register` | `{accountType: candidate\|advertiser, name, email, password, confirmPassword}` → tokens |
| `POST` | `/api/auth/forgot-password` | `{email}` → e-mail de reset |

Claims emitidas: `sub`, `candidate_id`/`advertiser_id`, `user_type`.

As rotas `/api/v1/**` exigem `Authorization: Bearer` (policy `ApiBearer`), exceto as marcadas como anônimas na tabela abaixo. `/api/auth/*` é anônimo. As respostas são ProblemDetails; a API nunca redireciona para login em cookie.

## Endpoints `/api/v1`

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
| `GET` | `/api/v1/features` | anônimo ou bearer | Feature flags ligadas para quem chama (`{ "features": [...] }`); veja `docs/FEATURE-FLAGS.md` |
| `POST` | `/api/v1/account/delete` | bearer | Exclui a própria conta e anonimiza os dados (`{ "password": "..." }`; senha errada → `400` com erro em `password`; sucesso → `204`). Exigência das lojas de app. |
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

## Configuração do JWT

```json
"Jwt": {
  "Issuer": "monburan.api",
  "Audience": "monburan.app",
  "AccessTokenExpiration": 3600,       // segundos
  "RefreshTokenExpiration": 2592000,   // segundos (30 dias)
  "SigningKey": ""                     // produção: JWT_SIGNING_KEY / Jwt__SigningKey
}
```

A aplicação falha no boot sem `Jwt__SigningKey` (`ValidateOnStart`). Gere a chave com `openssl rand -base64 48`. Detalhes de produção em [CONFIGURACAO.md](CONFIGURACAO.md).

## Modelos de dados

### Job (vaga)
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

### Candidate (candidato)
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
