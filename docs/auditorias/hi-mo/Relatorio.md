# HI-MO — consulta consentida HiMarshall

## Escopo implementado

- `POST /api/v1/integrations/tokens`: o titular candidato emite token opaco `mbi_` com escopos explícitos (`jobs:read`, `readiness:read`) e expiração de 1 a 90 dias. O valor aparece apenas na resposta da criação.
- `GET /api/v1/integrations/tokens` e `DELETE /api/v1/integrations/tokens/{id}`: listagem sem segredos e revogação pelo próprio titular.
- `GET /api/v1/integrations/jobs`: bearer `mbi_` valida hash SHA-256, expiração, revogação, escopo `jobs:read`, conta ativa e perfil de candidato antes de consultar vagas.
- Resposta mínima: id, título, empresa, localidade e link de candidatura. Não expõe currículo, salário oculto, dados financeiros nem perfil completo.
- `/conta/integracoes`: emissão, cópia única e revogação pelo titular, atrás da feature flag `integracao-himarshall`.

## Regras de segurança e rollback

- O token real nunca é persistido; somente hash SHA-256. Revogação impede replay imediatamente.
- Conta inativa ou perfil de candidato removido bloqueia a consulta mesmo antes da expiração.
- Feature flag sem grupos por padrão em produção; rollback operacional é desligar a flag e revogar tokens ativos. A migration é aditiva e reversível (`Down` remove `IntegrationTokens`).
- Não há banco compartilhado com HiMarshall nem dados reais neste ensaio.

## Evidência local

- `dotnet build src/Monburan.Web/Monburan.Web.csproj --configuration Release`: 0 warnings/0 erros.
- `dotnet test tests/Monburan.ApiTests/Monburan.ApiTests.csproj --filter FullyQualifiedName~IntegrationTokensApiTests`: 4/4.
- `dotnet test tests/Monburan.UnitTests/Monburan.UnitTests.csproj --configuration Release`: 1315/1315.

A suíte API completa foi executada no branch antes do PR; resultado final fica registrado no PR/Issue.

## Ensaio E2E local HiMarshall ↔ Monburan — 2026-11-26

Ambiente descartável local: `monburan_local`/`himarshall_local` em PostgreSQL de teste, sem dados reais. O ensaio usou conta fixture `joao.silva@email.com` no Monburan e `pro@himarshall.io` no HiMarshall.

| Etapa | Resultado |
|---|---|
| Emissão `mbi_` | `POST /api/v1/integrations/tokens` → 200 |
| Consulta direta Monburan | `GET /api/v1/integrations/jobs` → 200 com vagas públicas mínimas |
| Consentimento HiMarshall | `POST /api/v1/integrations/monburan/consent` → `pending` |
| Anexação protegida | `POST /api/v1/integrations/monburan/token` → 200/`attached:true` |
| Status HiMarshall | `active`, `tokenAttached:true`, `canAttachToken:true` |
| Consulta consentida | `GET /api/v1/integrations/monburan/jobs` → 200 com `title`, `company`, `location`, `applyUrl`; sem currículo nem dados financeiros |

O primeiro ensaio expôs falha real de tradução EF nas consultas de titular/revogação (`Id.Value` em consulta SQL). A correção `dd4a00f504d9f8b7bad655fe4be8860c80db4514` foi validada pelos testes `IntegrationTokensApiTests` 4/4 e pela consulta E2E 200 acima.
