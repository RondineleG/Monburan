# PROMPT — Auditoria e Correção Completa do Monburan (rodar com Opus)

> Copie todo o conteúdo abaixo da linha `---` e cole como prompt no Opus.

---

## Papel

Você é um engenheiro .NET sênior + especialista em UX fazendo uma **auditoria completa, mapeamento e correção** do projeto Monburan para deixá-lo **100% funcional e pronto para produção** — tanto o projeto **web** quanto o **mobile**.

Trabalhe de forma autônoma: analise, mapeie, priorize, corrija e verifique. Ao final, entregue um relatório objetivo do que foi encontrado e corrigido.

---

## Contexto do projeto

Plataforma de recrutamento e seleção (vagas, candidaturas, candidatos, anunciantes, currículos, planos/assinaturas, notificações) em **.NET 10**.

```
src/
├── Monburan.Core            → domínio: Entities, DTOs, UseCases, Validations, ValueObjects
├── Monburan.Web             → Blazor Server + API REST (/api/auth/*, /api/v1/*), EF Core,
│                              Identity, JWT Bearer, middlewares (SecurityHeaders, RateLimit),
│                              Services (email Resend/SendGrid/SMTP, IdentityService,
│                              LocalStorageProvider, JobExpirationBackgroundService)
├── Monburan.Web.Client      → Blazor WebAssembly (auth state persistente)
├── Monburan.Mobile          → .NET MAUI Blazor Hybrid (Android) — lado CANDIDATO apenas
└── Monburan.Mobile.Shared   → MonburanApiClient, AuthApiClient, TokenService,
                               AuthHeaderHandler, SecureStorageTokenStore
tests/
├── Monburan.UnitTests / IntegrationTests / ApiTests / E2ETests
```

Fatos conhecidos (não re-audite, use como ponto de partida):

- **Não existe componente nem serviço compartilhado de toast/snackbar ou modal/dialog** — cada página improvisa feedback à sua maneira (ou não dá feedback nenhum).
- O mobile cobre **somente o lado candidato** (Login, Register, ForgotPassword, Jobs, JobDetails, Apply, MyApplications, Profile, Resumes, Notifications, NotFound). Não há fluxo de anunciante no app.
- `MobileApiConfig`: Release aponta `https://monburan.com.br`; Debug usa `127.0.0.1:5095` via `adb reverse`.
- Pendências documentadas e aceitas (fora de escopo, não implementar): SignalR tempo real, push notifications, cache offline, iOS, publicação em stores.
- Backend já tem: security headers, rate limiting, JWT Bearer com `token_use=access`, health check `/health`, Serilog, migrations automáticas via `APPLY_MIGRATIONS=true`.

---

## Objetivo

1. **Mapear** cada interação de UI (botão, form, link, aba, ícone, menu) → serviço chamado → endpoint → handler → banco.
2. **Corrigir** tudo que estiver quebrado, incompleto, ou sem feedback ao usuário.
3. **Esconder ou remover** qualquer elemento de UI cuja funcionalidade não exista no backend (sem endpoint = não exibe). Nada de botão morto, link quebrado, menu que leva a 404 ou placeholder "em breve" em produção.
4. **Padronizar a camada de UX**: toasts de feedback, modais de confirmação, estados de loading/empty/error, mensagens claras em pt-BR.
5. Garantir **paridade funcional do mobile** dentro do escopo candidato.
6. **Verificar**: build limpo, testes passando, fluxos críticos funcionando ponta a ponta.

---

## Fases de execução (siga na ordem)

### Fase 0 — Inventário (produzir artefato, não corrigir ainda)

Gere a **matriz de cobertura UI ↔ API** em um arquivo `docs/AUDIT-MATRIX.md`:

| Tela/Componente | Ação de UI | Endpoint chamado | Existe? | Retorna erro? | Tem feedback (toast/modal)? | Tem loading? | Trata vazio/erro? | Status |

Varra **todas** as páginas:

- Web (`src/Monburan.Web/Components/Pages` + `Components/Account`): Jobs, JobDetails, ApplyJob, MyApplications, JobCreate, JobEdit, MyJobs, ReceivedApplications, CandidatesList, AdvertisersList, CandidateProfile, AdvertiserProfile, Dashboard, Plans, MySubscription, ResumeList/Create/Edit/Details, UploadLogo, UploadResume, Notifications, NotFound, Error, TermsOfUse, PrivacyPolicy + páginas de Account (Login, Register, Manage/*).
- Mobile (`src/Monburan.Mobile/Components/Pages`): todas as 11 páginas.
- Mobile.Shared: todo método do `MonburanApiClient`/`AuthApiClient` deve ter endpoint correspondente e DTO compatível (nome, casing, nullability). Verifique **desserialização** — contrato quebrado silencioso é o bug mais comum aqui.

### Fase 1 — Correções de funcionalidade (P0)

Para cada célula "Existe? = não" ou "Retorna erro? = sim":

1. **Decida**: implementar o endpoint/recurso que falta OU remover/esconder o elemento de UI. Regra:
   - Fluxo core do domínio (vagas, candidatura, currículo, perfil, auth, planos, notificações) → **implementar**.
   - Nice-to-have sem backend (analytics avançado, boost premium, share social, etc.) → **esconder** o elemento.
2. Corrija desserialização/contratos quebrados entre `Mobile.Shared` e a API.
3. Corrija navegação quebrada: links 404, rotas erradas, `NotFound` mal configurado.
4. Garanta autorização correta: página de anunciante não pode ser acessível por candidato e vice-versa; botões restritos por role não devem renderizar para quem não tem permissão.

### Fase 2 — Camada de UX compartilhada (P0)

Crie os serviços/componentes reutilizáveis que hoje não existem e aplique em **todas** as telas:

1. **`IToastService` + componente Toast** (sucesso / erro / info / warning, auto-dismiss, empilhamento, acessível via injeção). Substituir todos os `Console.WriteLine`, `alert`-style inline, mensagens soltas em `@code` e `try/catch` silenciosos.
2. **Modal de confirmação** reutilizável (`ConfirmDialog`) para toda ação destrutiva: excluir vaga, excluir currículo, cancelar candidatura, remover arquivo. Nenhum delete direto sem confirmação.
3. **Estados de tela padronizados** em toda página que carrega dados:
   - Loading (skeleton ou spinner, nunca tela branca/travada)
   - Empty state amigável com call-to-action ("Nenhuma candidatura ainda — explore vagas")
   - Error state com botão "Tentar novamente" (nunca stack trace ou tela vermelha de exception em produção)
4. **Forms**: validação visível por campo, mensagens em pt-BR claras, botão de submit desabilitado durante envio (sem double-submit), sucesso confirmado por toast + navegação.
5. **Erros de API**: mapear `ProblemDetails`/status codes para mensagens humanas. 401 → redirecionar para login (web) ou renovar token/logout (mobile). 403 → mensagem de permissão. 422 → erros de campo. 500 → "Algo deu errado, tente novamente".

Aplique o mesmo padrão no mobile (componentes podem ser duplicados se `Mobile.Shared` não comportar UI — se comportar, compartilhe).

### Fase 3 — Mobile: funcionalidade 100% (P0)

- Login/logout/register/forgot-password funcionando end-to-end, incluindo **refresh token** automático no `AuthHeaderHandler`/`TokenService` (renova em 401 e retenta a chamada uma vez; se falhar, logout limpo para Login).
- Toda chamada do `MonburanApiClient` usada por alguma página precisa bater em endpoint real — valide método, rota e DTO.
- Falha de rede/API fora do ar → tela de erro com retry, não crash nem tela infinita de loading.
- `NotFound` e rotas inexistentes tratadas.
- Corrija warnings do `Monburan.Mobile.csproj` e confirme que `dotnet build -f net10.0-android` compila (ou documente blocker de SDK/Workload se o ambiente não tiver workload Android).

### Fase 4 — Backend: lacunas e consistência (P1)

- Todo endpoint chamado pela UI existe e responde com `ProblemDetails` padronizado em erro.
- Validações de request presentes e retornando 400/422 com mensagens úteis.
- N+1 óbvios nos UseCases lidos pelas telas mais pesadas (Jobs, Dashboard, ReceivedApplications) — `AsNoTracking` em leitura.
- Nada de secrets/credenciais hardcoded; upload de arquivo valida tipo/tamanho.

### Fase 5 — Verificação (obrigatório antes de encerrar)

1. `dotnet build Monburan.sln` → **0 erros, 0 warnings novos** introduzidos por você.
2. `dotnet test` nos 4 projetos de teste → todos verdes; **adicione testes** para endpoints/serviços novos que você criar (siga o padrão de `Monburan.ApiTests` — WebApplicationFactory + EF InMemory).
3. Checklist manual de fluxos críticos (valide navegando/rodando, não só lendo código):
   - Candidato: register → login → listar vagas → ver detalhe → candidatar → ver em MyApplications → upload currículo → perfil → notificações.
   - Anunciante (web): login → criar vaga → ver em MyJobs → ver candidaturas recebidas → mudar status → planos/assinatura.
   - Mobile: login → jobs → detalhe → apply → my applications → profile → resumes → notifications → logout.
4. Responda explicitamente no relatório: cada fluxo ✅/❌ e evidência.

---

## Regras duras

- **Proibido** deixar elemento de UI que chama endpoint inexistente ou retorna erro sem tratamento.
- **Proibido** silenciar exceção (`catch {}`), `Console.WriteLine` de erro, ou mostrar exception crua ao usuário.
- **Proibido** texto em inglês em mensagem de UI (projeto é pt-BR) — exceto nomes de marca.
- **Proibido** quebrar contrato da API já consumida pelo mobile publicado — se mudar contrato, atualize `Mobile.Shared` no mesmo commit.
- Não crie features fora do escopo (SignalR, push, iOS, stores, pagamentos reais). Se encontrar UI que promete isso, esconda.
- Mantenha as convenções do projeto: UseCases, `Result`/`ResultMapper`, estilo dos endpoints minimal API, nomes existentes.
- Commits atômicos por fase, mensagens descritivas.

---

## Relatório final (obrigatório)

Entregue `docs/AUDIT-REPORT.md` com:

1. Resumo executivo (quantidade de issues por severidade).
2. Matriz de auditoria preenchida (`docs/AUDIT-MATRIX.md`).
3. Lista de correções: arquivo → problema → correção → status.
4. Lista do que foi **escondido/removido** e por quê.
5. Lacunas remanescentes que exigem decisão de produto (não técnicas).
6. Resultado da verificação (build, testes, checklist de fluxos ✅/❌).

Critério de pronto: **build verde + testes verdes + zero elemento de UI sem backend + feedback por toast/modal em 100% das ações de usuário + fluxos críticos validados**.
