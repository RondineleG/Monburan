# Próximo refactor: rotas em português

Status: **aplicado** na branch `refactor/ui-padronizada-web-app`. Rotas antigas redirecionam (301).

## Motivação

- As rotas de identidade usam `/Account/...` em inglês (77 arquivos citam esses caminhos).
- Outras rotas já estão em português (`/jobs`, `/plans`, `/notifications`, `/sugerir-vaga`, `/admin/sugestoes`), mas algumas ainda estão em inglês (`/dashboard`, `/my-applications`, `/resumes`, `/privacy`, `/terms`, `/admin/accounts`, `/career-profile`, `/profile/...`).
- Links já enviados por e-mail (confirmação e reset de senha) e favoritos precisam continuar funcionando.

## Mapeamento proposto

| Atual | Proposta |
|---|---|
| `/Account/Login` | `/entrar` |
| `/Account/Register` | `/criar-conta` |
| `/Account/RegisterConfirmation` | `/criar-conta/confirmacao` |
| `/Account/ForgotPassword` e `ForgotPasswordConfirmation` | `/esqueci-senha` e `/esqueci-senha/enviado` |
| `/Account/ResetPassword` e `ResetPasswordConfirmation` | `/redefinir-senha` e `/redefinir-senha/concluido` |
| `/Account/ConfirmEmail` e `ConfirmEmailChange` | `/confirmar-email` e `/confirmar-email/alteracao` |
| `/Account/ResendEmailConfirmation` | `/reenviar-confirmacao` |
| `/Account/LoginWith2fa` e `LoginWithRecoveryCode` | `/entrar/2fa` e `/entrar/codigo-recuperacao` |
| `/Account/Manage` | `/conta` |
| `/Account/Manage/TwoFactorAuthentication` (e demais `Manage/*`) | `/conta/seguranca-2fa` (e `/conta/email`, `/conta/alterar-senha`, `/conta/dados-pessoais`, `/conta/logins-externos`) |
| `/Account/AccessDenied` | `/acesso-negado` |
| `/Account/Lockout` | `/conta-bloqueada` |
| `/Account/ExternalLogin`, `InvalidUser`, `InvalidPasswordReset` | `/entrar/externo`, `/conta/usuario-invalido`, `/redefinir-senha/invalido` |
| `/dashboard` | `/painel` |
| `/my-applications` | `/minhas-candidaturas` |
| `/my-jobs` | `/minhas-vagas` |
| `/my-subscription` | `/minha-assinatura` |
| `/resumes`, `/resumes/new`, `/resumes/upload`, `/resumes/wizard`, `/resumes/{id}`, `/resumes/{id}/edit`, `/resumes/{id}/cv`, `/resumes/{id}/wizard` | `/curriculos`, `/curriculos/novo`, `/curriculos/upload`, `/curriculos/assistente`, `/curriculos/{id}`, `/curriculos/{id}/editar`, `/curriculos/{id}/cv`, `/curriculos/{id}/assistente` |
| `/career-profile` | `/perfil-profissional` |
| `/profile/candidate` | `/perfil/candidato` |
| `/profile/advertiser` | `/perfil/empresa` |
| `/received-applications` | `/candidaturas-recebidas` |
| `/apply/{jobId}` | `/candidatar/{jobId}` |
| `/companies/{advertiserId}` | `/empresas/{advertiserId}` |
| `/advertiser/upload-logo` | `/empresa/logo` |
| `/jobs/create` | `/vagas/nova` |
| `/jobs/edit/{jobId}` | `/vagas/{jobId}/editar` |
| `/jobs/{jobId}/applications` | `/vagas/{jobId}/candidaturas` |
| `/address/list`, `/address/create`, `/address/edit/{Id}`, `/address/delete/{Id}` | `/enderecos`, `/enderecos/novo`, `/enderecos/{id}/editar`, `/enderecos/{id}/excluir` |
| `/admin/accounts` | `/admin/contas` |
| `/admin/advertisers` | `/admin/empresas` |
| `/admin/candidates` | `/admin/candidatos` |
| `/admin/jobs` | `/admin/vagas` |
| `/admin/audit` | `/admin/auditoria` |
| `/admin/ai-providers` | `/admin/provedores-ia` |
| `/privacy` | `/privacidade` |
| `/terms` | `/termos` |

Mantidas como estão (já em português): `/jobs`, `/jobs/{id}`, `/plans`, `/notifications`, `/sugerir-vaga`, `/admin/sugestoes`, `/404`, `/`.

## Decisões

1. **Mapeamento:** aprovado e aplicado conforme a tabela, com um complemento: as páginas `Account/Manage/*` que a tabela não listava ganharam nomes em português (`/conta/desativar-2fa`, `/conta/ativar-autenticador`, `/conta/codigos-recuperacao`, `/conta/redefinir-autenticador`, `/conta/definir-senha`, `/conta/excluir-dados`).
2. **Redirecionamentos 301:** feitos pelo middleware `LegacyRouteRedirects` (`src/Monburan.Web/Middleware/LegacyRouteRedirects.cs`), só para GET e HEAD, com a query string preservada. Links de confirmação e de redefinição de senha já enviados por e-mail continuam funcionando.
3. **Branch:** feito na própria branch `refactor/ui-padronizada-web-app`, a pedido, e não em uma branch separada da `dev`.

Ficou fora de propósito:

- Endpoints de formulário (POST) do Identity: `/Account/Logout`, `/Account/PerformExternalLogin`, `/Account/Manage/LinkExternalLogin` e `/Account/Manage/DownloadPersonalData`. Não são páginas. Trocar exige mudar também os formulários e os testes que os usam.
- Rotas de API (`/api/...`), inclusive as que o app móvel consome.
- Rotas internas do app MAUI (`src/Monburan.Mobile`): são rotas do aplicativo, não URLs do servidor.
- Documentos históricos de auditoria (`docs/AUDIT-*.md` e `docs/UX-REVISAO.md`): mantêm as rotas da época.

## Impacto técnico

- Páginas de identidade em `src/Monburan.Web/Components/Account/Pages` (`@page`).
- Configuração de cookie de login e URLs de retorno em `Program.cs`.
- Links de confirmação e reset gerados pelo Identity, nos e-mails enviados.
- Testes e2e (`tests/Monburan.E2ETests`) e harness de UX (`tests/ux-smoke`) usam as URLs antigas.
- Documentação com links (`README.md`, `docs/*.md`).
