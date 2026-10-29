# Checklist de pré-produção — Monburan

> Lista para validar, item a item, o que falta para publicar a versão da branch `refactor/ui-padronizada-web-app`
> (já mesclada na `dev`) e para lançar o app Android. Base de verificação: `dev` em `34919f1`, `origin/main` em
> `7a02bae`, branch `refactor/ui-padronizada-web-app` em `a6c6c7d`. Data da análise: 2026-10-29.
>
> Como usar: marque `[x]` só depois de validar o item no ambiente real (não só no código). Onde houver
> **Evidência**, anote o comando, o print ou o link que comprova. Itens marcados como 🔴 bloqueiam o deploy.

Legenda: 🔴 bloqueante · 🟠 importante antes do lançamento público · 🟡 recomendado · 🔵 pós-lançamento

---

## Status no código (branch `chore/pre-prod`, 2026-10-29)

Feito e testado **no código** (os checkboxes abaixo continuam vazios até a validação no ambiente real):

| Item | O que mudou |
|---|---|
| §2 Build do Swashbuckle | Atualizado para 10.3.1 com o namespace novo (`Microsoft.OpenApi`). Não precisa mais ignorar a versão maior no Dependabot. |
| §2 Atualizar com a `dev` | `dev` (`23b1d8b`, `34919f1`) mesclada, sem conflitos. |
| §2 Warnings de nulabilidade | Zero warnings. As 4 correções em produção tratam vaga sem anunciante (§3.5). |
| §2 GitHub Actions | Liberado: runs de 26/10 concluídos (`gh run list`). |
| §3.1 Volume e backup das evidências | Volume `app_evidence` + `Evidence__StoragePath` no compose; `backup.sh` gera `monburan-evidencias-*.tar.gz.age`; `restore-test.sh` confere. |
| §3.1 Retenção | `EvidenceRetentionBackgroundService`: apaga imagens de sugestões rejeitadas/duplicadas após `Evidence:RetentionDays` (90). |
| §3.1 Permissões | Pasta com `chmod 700`, dono `app` (sem root). |
| §3.2 IA e privacidade | Fluxo documentado em `docs/IA-CURRICULO.md` e `docs/IA-PROVEDORES.md`; aviso no `/sugerir-vaga`; Política de Privacidade (itens 2.1, 5 e 6). **Falta a validação jurídica e a conferência dos termos do provedor.** |
| §3.3 IPv6 /64 e spam | Chave do limite = `RateLimiting.ClientKey` (bloco /64); campo-isca no formulário. |
| §3.3 Tamanho do upload | As imagens vão pelo SignalR em pedaços; o `client_max_body_size 20m` do nginx não as corta (anotado no `nginx.conf`). |
| §3.3 Corrida no limite de links | `ResumeShareRepository.CreateAsync` conta e grava na mesma transação, com `FOR UPDATE` na linha do currículo. |
| §3.4 Token nos logs | nginx com `log_format monburan_masked`; log do app com `RateLimiting.PathForLog`. Acesso indevido já testado (`PublicLookup_ForRevokedOrExpiredOrUnknownToken_AnswersTheSameWay`); `X-Robots-Tag` e `sitemap` conferidos. |
| §3.5 `Jobs.AdvertiserId` nulo | Consultas revisadas; 4 pontos que assumiam valor corrigidos. |
| §3.6 Rotas em inglês | Documentado como **pendente de decisão** (proposta em `docs/refactor-rotas-ptbr.md`), junto com a diferença `/curriculo` × `/curriculos`. |
| §4.2 Logs | Volume `app_logs` (`/app/logs`). |
| §4.2 Contêiner sem root | Imagem roda como `app` (UID 1654) na porta 8080; o serviço `volume-permissions` acerta o dono dos volumes antigos. |
| §4.2 SBOM | `sbom` e `provenance` no `docker-ghcr.yml`. **Trivy não foi adicionado** (ação de terceiro; fixar por digest antes). |
| §10 Documentação | `IMPROVEMENTS.md`, `TEST_STRATEGY.md`, `AUDIT-REPORT.md`, `CONFIGURACAO.md`, `SEGURANCA.md`, `DOCKER.md`, `API.md`, `APP-MOBILE.md`. |
| §11 Versão do app | 1.6.0 (build 6). |
| §11 Sessão expirada | Aviso + login com volta à mesma tela. |
| §11.1 Exclusão de conta no app | `POST /api/v1/account/delete` + seção "Excluir conta" no Perfil. |

Testes: 1276 unitários e 256 de API passando (11 novos). **Não rodados aqui:** integração (Testcontainers) e E2E
(Playwright), que precisam de Docker, e o build do app MAUI (workload `maui-android` não instalado nesta máquina; as
telas foram compiladas pelo `tests/ux-mobile-preview`). O CI roda os três.

**Atenção no deploy:** o app passa a escutar na **8080** dentro do contêiner. O `docker-compose.prod.yml` já mapeia
`127.0.0.1:8080:8080`; o `deploy.sh` copia o compose novo. O nginx do host não muda (continua em `127.0.0.1:8080`).

---

## 1. Decisões de produto (bloqueantes)

- [ ] 🔴 **Cobrança dos planos.** Hoje "Fazer upgrade" ativa Premium/Enterprise sem pagamento (`AUDIT-REPORT.md` §5.1).
      Escolher e registrar uma opção:
  - [ ] integrar gateway de pagamento (ex.: Mercado Pago, Asaas ou Stripe);
  - [ ] ativação manual pelo administrador;
  - [ ] ocultar os planos pagos até a cobrança existir.
      **Evidência:** decisão registrada em `PRODUCT.md`.
- [ ] 🟠 **Boost, destaque e analytics por plano.** Implementar ou remover do catálogo (`AUDIT-REPORT.md` §5.2).
- [ ] 🟠 **Limite de vagas ativas.** Comunicar aos anunciantes que quem excede o limite não publica nem reativa vagas
      até ficar abaixo (`AUDIT-REPORT.md` §5.3).
- [ ] 🟡 **Página pública da empresa** e **detalhe de candidato no admin:** decidir se entram no roadmap (§5.4).
- [ ] 🟡 **PDF importado como campo do currículo.** Decidir se o `FileUrl` será exibido ou baixado pelo recrutador (§5.5).
- [ ] 🟠 **Retenção na exclusão de conta (LGPD).** Validar com o jurídico que candidaturas ficam anônimas no histórico
      da empresa (§5.6).
- [ ] 🟡 **Texto da notificação de rejeição** ("não seguiu no processo") validado com o produto (§5.7).

## 2. Bloqueios técnicos da branch e do repositório

- [ ] 🔴 **Build quebrado pelo Dependabot.** O PR `Swashbuckle.AspNetCore-10.3.1` falhou com `CS0234` em
      `src/Monburan.Web/Program.cs:67` (namespace `Microsoft.OpenApi.Models`). Ajustar o código ao Swashbuckle 10
      ou manter a versão atual.
      **Evidência:** run `40302063731` do `CI/CD Pipeline`.
- [ ] 🟡 Configurar o Dependabot para ignorar atualizações **major** do Swashbuckle até a migração ser feita.
- [ ] 🔴 **Separar a branch de refactor em PRs menores** para a `dev`: (a) rotas PT-BR e redirecionamentos;
      (b) links públicos de currículo; (c) sugestões de vagas e canais externos; (d) `.gitignore` e documentação.
      Hoje a branch tem 214 arquivos e +17.4 mil linhas.
- [ ] 🔴 **Atualizar a branch de refactor com a `dev`** (ela está 2 commits atrás: `23b1d8b` e `34919f1`).
      O merge simulado não tem conflitos.
- [ ] 🟠 **Histórico `main` × `dev`.** A `main` tem 6 merge commits que não estão na `dev`, e a `dev` tem 2 que não
      estão na `main`. O `ci-cd.yml` difere entre as duas. Alinhar o histórico após a promoção.
- [ ] 🟠 **Promover `dev` → `main`** por Pull Request, com `build-and-test`, `e2e` e `security-scan` verdes
      (`AGENTS.md`, `docs/SEGURANCA.md` §Repositório).
- [ ] 🟡 **Warnings de nulabilidade** (8 no build): corrigir em `UpdateApplicationStatusUseCase`,
      `AdminChangeJobStatusUseCase`, `ListApplicationHistoryUseCase`, `NotifyOnApplicationCreatedUseCase`,
      `JobResponseMapperTests` e `ResumeShareTests`.
- [ ] 🟡 Confirmar que o GitHub Actions está liberado (a auditoria de 17/10 registrou bloqueio por cobrança; os runs de
      26/10 aparecem como concluídos, mas confirmar a conta).

## 3. Módulos novos da branch (sugestões de vagas e links de currículo)

### 3.1 Evidências de sugestões (bloqueante)

- [ ] 🔴 **Persistir `App_Data/evidencias`.** Hoje as imagens ficam em `/app/App_Data/evidencias`, mas o
      `docker-compose.prod.yml` só monta `app_uploads` e `app_keys`. Cada `docker compose up --force-recreate` apaga as
      imagens. Criar o volume `app_evidence:/app/App_Data/evidencias` e configurar `Evidence:StoragePath` se necessário.
      **Evidência:** deploy de teste, enviar sugestão, recriar o contêiner e abrir a sugestão em `/admin/sugestoes`.
- [ ] 🔴 **Incluir as evidências no backup.** O `backup.sh` só copia `uploads`. Acrescentar a pasta de evidências e testar
      a restauração (`restore-test.sh`).
- [ ] 🟠 **Política de retenção das imagens.** Definir quanto tempo guardar evidências de sugestões rejeitadas ou
      arquivadas e implementar a limpeza.
- [ ] 🟠 **Permissões do diretório** de evidências no contêiner (só o processo da aplicação deve ler).

### 3.2 Privacidade e IA

- [ ] 🔴 **Documentar o envio de imagens a provedor de IA.** `ExtractJobDraftUseCase` envia as imagens das sugestões
      anônimas ao provedor padrão. `docs/IA-CURRICULO.md` não menciona esse fluxo. Atualizar com finalidade, provedor,
      retenção e base legal (LGPD).
- [ ] 🟠 **Aviso no formulário** `/sugerir-vaga`: informar que as imagens podem ser analisadas por IA e que dados pessoais
      visíveis devem ser ocultados.
- [ ] 🟠 **Verificar o comportamento do provedor** sobre retenção e treinamento com as imagens enviadas (contrato ou
      política do provedor escolhido).
- [ ] 🟡 Confirmar que o rascunho de IA não publica nada sem aprovação humana (teste manual de ponta a ponta).

### 3.3 Limites e abuso

- [ ] 🟠 **Limite de sugestões por rede IPv6.** O limite de 5 por hora usa o IP completo (`SubmissionRateLimiter`). Aplicar
      o bloco `/64` como o restante do projeto (`RateLimiting.ClientKey`).
- [ ] 🟠 **Proteção contra spam no formulário público** (CAPTCHA ou honeypot), já que não há login.
- [ ] 🟡 **Limite de tamanho da requisição** para o upload de até 15 MB por envio: revisar o limite do Kestrel e do nginx.
- [ ] 🟡 **Condição de corrida no limite de links ativos** (5 por currículo): a checagem e a gravação não são atômicas.
      Usar transação ou restrição no banco.

### 3.4 Links públicos de currículo

- [ ] 🟠 **Token no caminho da URL** (`/curriculo/{Token}`) vai para os logs do nginx. Mascarar o token nos logs ou
      registrar a decisão na documentação.
- [ ] 🟡 **Teste de acesso indevido:** token revogado, expirado, inválido e de outro currículo devem responder igual
      (`GetPublicResumeUseCase`).
- [ ] 🟡 Confirmar que `X-Robots-Tag: noindex` aparece na página pública e que o `sitemap` não a inclui.

### 3.5 Migrations

- [ ] 🔴 **Backup antes do deploy** das 4 migrations novas (23/10). As migrations rodam no boot.
- [ ] 🟠 **Revisar `AlterColumn` em `Jobs.AdvertiserId`** (de `Guid` para `Guid?`): conferir as consultas que assumem valor
      não nulo.
- [ ] 🟠 **Testar a migração sobre uma cópia do banco de produção** (não só o banco de desenvolvimento).

### 3.6 Redirecionamentos e rotas

- [ ] 🟡 **Rotas ainda em inglês:** `/jobs`, `/jobs/{jobId}`, `/plans`, `/notifications`. Decidir se entram na padronização
      e atualizar `docs/refactor-rotas-ptbr.md`.
- [ ] 🟡 **Confusão entre `/curriculo/{Token}` (público) e `/curriculos` (privado).** Documentar ou renomear.
- [ ] 🟡 Testar os redirecionamentos 301 com query string e com links antigos enviados por e-mail (confirmação e reset).

## 4. Infraestrutura e produção (VPS)

### 4.1 Configuração e segredos

- [ ] 🔴 `ConnectionStrings__DefaultConnection` aponta para o Postgres de produção, com senha nova (não a de desenvolvimento).
- [ ] 🔴 `JWT_SIGNING_KEY` (`Jwt__SigningKey`) é de produção, gerada com `openssl rand -base64 48`, e diferente da chave
      de desenvolvimento.
- [ ] 🔴 `ADMIN_EMAILS` configurado e o e-mail confirmado (`docs/SEGURANCA.md`, "Primeiro administrador em produção").
- [ ] 🔴 `REQUIRE_ADMIN_2FA=true` no `.env` da VPS.
- [ ] 🔴 `ALLOWED_HOSTS=monburan.com.br;www.monburan.com.br;127.0.0.1`.
- [ ] 🔴 `App__PublicBaseUrl=https://monburan.com.br` (links de e-mail usam essa base).
- [ ] 🔴 `Smtp__Host`, `Smtp__User`, `Smtp__Password` configurados (Resend).
- [ ] 🔴 `chmod 600` em `.env` e `ai-keys.env`.
- [ ] 🟠 Confirmar que o seed de desenvolvimento (contas `admin@monburan.com.br` e `adm@monburan.com.br`, com senhas
      públicas no repositório) **não** roda em produção.
- [ ] 🟠 Verificar que nenhum segredo foi commitado no histórico (`security-scan` e secret scanning ativos).
      Se algum vazou, trocar na origem antes do deploy.
- [ ] 🟡 Dados sensíveis só nas variáveis de ambiente; nada em `appsettings.json` (`docs/CONFIGURACAO.md`).

### 4.2 Contêiner e imagem

- [ ] 🟠 **Volume para os logs.** Só `app_uploads` e `app_keys` estão montados. O Serilog grava em `logs/` dentro do
      contêiner, então os logs somem a cada recriação. Montar um volume ou confiar só na saída do console coletada.
- [ ] 🟠 **Health checks.** Confirmar que `/health/ready` verifica o PostgreSQL (o README diz que sim; o `IMPROVEMENTS.md`
      diz que os checks são básicos). Testar com o banco parado.
- [ ] 🟡 **Contêiner sem root.** Hoje a aplicação roda como root (`docs/SEGURANCA.md`, "O que ainda não está coberto").
- [ ] 🟡 **Varredura da imagem** com Trivy e geração de SBOM na esteira.
- [ ] 🟡 Imagem base atualizada (rebuild semanal ativo e funcionando).

### 4.3 Servidor

- [ ] 🔴 **Sistema operacional com suporte.** Confirmar com `cat /etc/os-release`. Se for Debian 11, migrar para Debian 12 ou 13
      (`docs/SEGURANCA.md`).
- [ ] 🔴 **Firewall, SSH e fail2ban.** Rodar `bash infra/vps/validate.sh --ssh ...` e conferir: só 22 (SSH), 80 e 443
      publicados; SSH só por chave.
- [ ] 🟠 **Usuário administrativo próprio**, com `sudo`, e `PermitRootLogin no`.
- [ ] 🟠 **Atualizações de segurança** entram automaticamente. Conferir `/var/run/reboot-required` e planejar reinício.
- [ ] 🟡 Cabeçalhos HTTP: `curl -sI https://monburan.com.br/` mostra HSTS, CSP e sem a versão do servidor.
- [ ] 🟡 Limite por IP testado: 25 envios de login devem gerar `429` a partir do 21º.

### 4.4 Backup e recuperação

- [ ] 🔴 **Backup remoto configurado.** `BACKUP_REMOTE` está vazio por padrão, o que deixa o backup só no disco da VPS.
      Configurar o `rclone` e testar com `rclone lsd <nome>:`.
- [ ] 🔴 **Chave `age` de backup** gerada na máquina local, com a chave privada fora da VPS (`docs/SEGURANCA.md` §4).
- [ ] 🔴 **Primeiro backup concluído** (`bash backup.sh`) e timer diário ativo (`systemctl list-timers monburan-backup.timer`).
- [ ] 🔴 **Teste de restauração** executado com `restore-test.sh --latest-remote` antes do lançamento.
- [ ] 🟠 `HEALTHCHECK_URL` configurado (ex.: healthchecks.io) para avisar quando o backup falhar ou parar.
- [ ] 🟠 **Restauração completa ensaiada** em servidor de teste (banco e uploads, incluindo as evidências do item 3.1).
- [ ] 🟡 Teste de restauração mensal agendado.

### 4.5 Deploy e rollback

- [ ] 🔴 **Permissão de escrita no GHCR** para o repositório no pacote `monburan`
      (`docs/CI-CD.md`, "Pré-requisito"). Validar com `gh workflow run docker-ghcr.yml --ref main`.
- [ ] 🔴 **Secrets do environment `prod`:** `VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY` e `VPS_KNOWN_HOSTS`, com chave dedicada
      ao GitHub Actions.
- [ ] 🔴 **Ensaio de rollback:** tag `rollback-<data>` criada, `docker tag ... latest` testado e `docker compose up -d app`.
      Lembrar que o rollback não desfaz migrations (restaurar backup, se necessário).
- [ ] 🟠 **Janela de deploy** combinada e comunicada, com backup feito antes.
- [ ] 🟡 `docker-compose.prod.yml` atualizado no servidor (Docker Compose 2.24 ou mais novo).

## 5. Observabilidade e operação

- [ ] 🔴 **Monitor de disponibilidade externo** apontando para `https://monburan.com.br/health/ready`, com alerta
      por e-mail ou celular (ex.: UptimeRobot, Better Stack).
- [ ] 🟠 **Rastreamento de exceções** (ex.: Sentry) ou alerta sobre erros 5xx nos logs.
- [ ] 🟠 **Logs persistidos e rotacionados**, com retenção definida (item 4.2).
- [ ] 🟡 **Painel de métricas** básico (CPU, memória, disco e latência do Postgres).
- [ ] 🟡 **Alerta de disco cheio** na VPS, principalmente com evidências e uploads crescendo.
- [ ] 🟡 Runbook de incidentes (`docs/SEGURANCA.md`, "Se algo der errado") revisado por quem vai operar.

## 6. E-mail e comunicação

- [ ] 🔴 **Registros DNS do domínio `monburan.com.br`:** SPF, DKIM e DMARC configurados para o Resend.
- [ ] 🔴 **Fluxos de e-mail testados** em Gmail e Outlook: confirmação de conta, reset de senha, confirmação de troca
      de e-mail, notificação de candidatura e de mudança de status.
- [ ] 🟠 Remetente, assunto e rodapé dos e-mails revisados em pt-BR.
- [ ] 🟡 Teste de entregabilidade (não cair em spam).

## 7. Qualidade e testes

- [ ] 🔴 **Testes unitários:** 1276 passando na branch (validado em 2026-10-29).
- [ ] 🔴 **Testes de API:** 245 passando na branch (validado em 2026-10-29).
- [ ] 🔴 **Testes de integração** (Testcontainers, precisa de Docker) passando na branch. *Não rodados nesta análise.*
- [ ] 🔴 **E2E (Playwright)** passando na branch, contra o app com Postgres. *Não rodado nesta análise.*
- [ ] 🟠 **Falha intermitente** em `FullSweepTests.Sweep_Advertiser_AllRoutes`: investigar a causa raiz. O job repete a
      execução para mascarar o problema (`docs/CI-CD.md`, Testes).
- [ ] 🟠 **Teste da migração** de sugestões e de links públicos com dados reais em PostgreSQL 16 (não InMemory).
- [ ] 🟠 **Teste de contrato do app** com a API da branch (`MobileClientContractTests`).
- [ ] 🟡 Rodar `ux-smoke` e comparar as capturas com as do `docs/prints`.
- [ ] 🟡 **Cobertura de código** gerada no CI e revisada (artefato `coverage-report`).

### 7.1 Roteiro manual de ponta a ponta (staging)

- [ ] Candidato: cadastro, login, busca, detalhe, candidatura, currículo (PDF), perfil, notificações.
- [ ] Anunciante: login, criar vaga, pausar e reativar, candidaturas recebidas, mudar status, planos.
- [ ] Sugestão de vaga anônima: enviar, recusar envio sem imagem, recusar imagem inválida, receber limite de envios.
- [ ] Moderação: aprovar, rejeitar, gerar rascunho com IA, conferir o rascunho antes de publicar.
- [ ] Link público de currículo: criar, abrir, revogar, abrir de novo (deve falhar), expirar.
- [ ] Administrador: entrar com 2FA, inativar conta, auditoria registrada, provedores de IA.
- [ ] Acesso negado: candidato em área de empresa e vice-versa.
- [ ] Redirecionamentos antigos (`/Account/Login`, `/my-jobs`, `/resumes/{id}/edit`).
- [ ] Erros: página 404 e página de erro sem detalhes técnicos.
- [ ] Console do navegador sem erros de CSP.

## 8. Segurança

- [ ] 🔴 **Varredura de dependências vulneráveis** sem alertas (`dotnet list package --vulnerable --include-transitive`).
- [ ] 🔴 **CodeQL** sem alertas de severidade alta em *Security → Code scanning*.
- [ ] 🟠 **Secret scanning** com bloqueio de push ligado.
- [ ] 🟠 **Teste de invasão externo** (pendente na auditoria).
- [ ] 🟠 **Revisão de IDOR** nos endpoints novos (`/api/v1/resumes/{id}`, links públicos, sugestões).
- [ ] 🟡 **Revisão do CSP** em produção (`Security__CspReportOnly=false`).
- [ ] 🟡 Cookies com `Secure`, `HttpOnly` e `SameSite=Lax` confirmados no DevTools.
- [ ] 🟡 Sessões invalidadas após troca de senha e inativação de conta (`SecurityStamp`).
- [ ] 🔵 CAPTCHA no cadastro e no login (`docs/SEGURANCA.md`).

## 9. Legal e conformidade (LGPD)

- [ ] 🔴 **Revisão jurídica da política de privacidade** (`/privacidade`) e dos termos de uso (`/termos`).
- [ ] 🟠 **Base legal e finalidade** documentadas para cada dado coletado (candidatos, empresas, currículos, imagens de
      sugestões, IA).
- [ ] 🟠 **Retenção de dados** definida e implementada (contas excluídas, candidaturas anônimas, evidências de sugestões).
- [ ] 🟠 **Canal de atendimento ao titular** de dados (e-mail ou formulário) divulgado.
- [ ] 🟠 **Exportação e exclusão de dados** testadas (`/conta/dados-pessoais`, `/conta/excluir-dados`).
- [ ] 🟡 Registro das operações de tratamento, se exigido pelo jurídico.

## 10. Documentação

- [ ] 🟠 **`IMPROVEMENTS.md`** atualizado: hoje afirma ~98% de prontidão, o que contradiz o `AUDIT-REPORT.md`.
- [ ] 🟠 **`TEST_STRATEGY.md`** atualizado com os números atuais e sem a menção à "issue #4" já resolvida com Testcontainers.
- [ ] 🟠 **`AUDIT-REPORT.md`** com o status de bloqueio do GitHub Actions atualizado.
- [ ] 🟡 **`README.md`** revisado (Monitoramento e Segurança batem com a produção).
- [ ] 🟡 **`docs/IA-CURRICULO.md`** e **`docs/IA-PROVEDORES.md`** com o fluxo de sugestões de vagas (item 3.2).
- [ ] 🟡 **`docs/CI-CD.md`** atualizado com o volume de evidências e o backup.
- [ ] 🟡 **`docs/refactor-rotas-ptbr.md`** atualizado com as rotas que ficaram em inglês.

## 11. Aplicativo Android (MAUI)

- [ ] 🔴 **URL de produção do app** (`MobileApiConfig`) aponta para `https://monburan.com.br`. Publicar a web antes de
      distribuir o APK (`docs/CI-CD.md`).
- [ ] 🔴 **Secrets da keystore** no GitHub: `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEY_ALIAS`, `ANDROID_KEYSTORE_PASSWORD`
      e `ANDROID_KEY_PASSWORD`.
- [ ] 🔴 **Keystore guardada fora do repositório**, em cofre, com cópia offline. Perdê-la impede atualizações.
- [ ] 🔴 **Versão do app** atualizada (`ApplicationDisplayVersion` e `ApplicationVersion`) no `Monburan.Mobile.csproj`.
- [ ] 🟠 **Teste em aparelho físico** (além do emulador), em Android atual e em versão mais antiga.
- [ ] 🟠 **Fluxos mobile** validados: cadastro, login, busca, candidatura, currículos, notificações e logout.
- [ ] 🟠 **Sessão expirada** tratada (refresh falhou → aviso e login).
- [ ] 🟡 Mensagens de rede sem internet (retry) testadas.

### 11.1 Distribuição

- [ ] 🟠 **Decidir o canal:** só APK no GitHub Release (atual) ou Google Play.
- [ ] 🔵 **Google Play** (se for o caso): conta de desenvolvedor (US$ 25), ficha do app, ícone de 512 px, política de
      privacidade, **Data Safety**, classificação de conteúdo e **exclusão de conta dentro do app** (exigência da loja
      para apps com cadastro).
- [ ] 🔵 **iOS:** sem conta Apple Developer (US$ 99/ano) só há `.ipa` sem assinatura para sideload (AltStore ou
      Sideloadly, validade de 7 dias). Para TestFlight e App Store: certificado, perfil, chave da App Store Connect,
      `PrivacyInfo.xcprivacy`, ícones e capturas.

## 12. Plano de lançamento

- [ ] 🔴 **Ambiente de staging** com o mesmo compose de produção e base separada.
- [ ] 🔴 **Janela de lançamento** definida, com responsável e contato de emergência.
- [ ] 🔴 **Backup feito imediatamente antes** do deploy (item 4.4).
- [ ] 🟠 **Comunicação aos usuários** (ex.: aviso sobre a nova versão e as novidades).
- [ ] 🟠 **Plano de comunicação de incidente** (quem comunica, em que canal e em quanto tempo).
- [ ] 🟡 **Métricas de sucesso** definidas para as primeiras semanas (cadastros, candidaturas, sugestões).

## 13. Pós-deploy (primeiras 24 horas)

- [ ] `bash infra/vps/validate.sh` sem falhas (de fora e de dentro do servidor).
- [ ] `curl -s https://monburan.com.br/health/ready` responde `200`.
- [ ] Roteiro manual do item 7.1 executado em produção, com contas de teste apagadas depois.
- [ ] Logs de erro revisados nas primeiras horas.
- [ ] Primeiro backup automático concluído e enviado ao remoto.
- [ ] E-mails de confirmação e reset chegando na caixa de entrada.
- [ ] Monitor de disponibilidade sem alertas falsos.
- [ ] App Android instalado a partir do APK de produção e conectado à API de produção.

---

## Histórico de revisão

| Data | Autor | Alteração |
|---|---|---|
| 2026-10-29 | análise automatizada | versão inicial, a partir da auditoria de 17/10 e da revisão da branch de refactor |
| 2026-10-29 | branch `chore/pre-prod` | seção "Status no código" com os itens resolvidos no código |
