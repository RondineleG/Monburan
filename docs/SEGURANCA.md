# Segurança e administração

Este documento reúne o que protege o Monburan (aplicação, VPS e repositório), o que o administrador do site pode fazer
pelas telas de administração e o passo a passo para colocar tudo isso em produção. Para a tela de provedores de IA, veja
[IA-PROVEDORES.md](IA-PROVEDORES.md); para a esteira de publicação, [CI-CD.md](CI-CD.md).

## Resumo

| Área | O que vale hoje | Onde ajustar |
|---|---|---|
| Senhas | Mínimo de 10 caracteres, com maiúscula, minúscula e número; senhas comuns, igual ao e-mail e sequências óbvias são recusadas | `PasswordPolicy.cs`, `CommonPasswordValidator.cs` |
| Tentativas de login | 5 erros seguidos bloqueiam a conta por 15 minutos | `Program.cs` (Identity) |
| Limite por IP | 50 req/s por IP no site todo; 20 envios por minuto por IP em login, cadastro e recuperação de senha | `Security:RateLimit` |
| Cookies | `HttpOnly`, `SameSite=Lax` e `Secure` sempre fora de desenvolvimento (inclusive atrás do nginx) | `ForwardedHeadersSetup.cs`, `CookieSecuritySetup.cs` |
| Sessão | Chaves de sessão guardadas em volume: o deploy não desloga ninguém; a sessão é revalidada a cada 2 minutos | `DataProtection:KeysPath` |
| Conteúdo da página (CSP) | Scripts só de arquivos do próprio site (nada inline, nada de `eval`) | `SecurityHeadersMiddleware.cs` |
| Administração | 2FA obrigatório; contas, vagas e provedores de IA gerenciáveis; toda ação fica na trilha de auditoria | `Security:RequireTwoFactorForAdmins`, `ADMIN_EMAILS` |
| VPS | Firewall só com SSH, 80 e 443; SSH só por chave; fail2ban; atualizações automáticas; backup criptografado fora do servidor | `infra/vps` |
| Repositório | Dependabot, CodeQL, secret scanning com bloqueio de push e proteção das branches `dev` e `main` | `.github`, configurações do repositório |

## Contas e senhas

- **Senha:** de 10 a 100 caracteres, com maiúscula, minúscula e número (símbolo não é obrigatório). Além disso, o servidor
  recusa as senhas mais comuns (`Senha@12345` conta como `senha12345`), a senha igual ao e-mail ou ao nome de usuário, um
  caractere repetido e as sequências óbvias. O site, a API e o app mostram a mesma regra.
- **Bloqueio por tentativas:** 5 senhas erradas seguidas bloqueiam a conta por 15 minutos (vale para candidatos, empresas e
  administradores). O administrador pode desbloquear antes em **Administração → Contas**.
- **Conta inativa:** uma conta inativada pelo administrador não entra, e a mensagem só aparece depois de a senha estar
  correta, para a tela não revelar quais e-mails têm conta.

## Sessões e cookies

- Atrás do nginx, o app só confia nos cabeçalhos `X-Forwarded-*` vindos de proxy local ou de rede privada, e de um salto só.
  É isso que faz o cookie de login sair com `Secure` e o limite por IP enxergar o IP real. O `nginx.conf` envia só o
  endereço que o próprio nginx viu: um `X-Forwarded-For` mandado pelo cliente não chega ao app.
- As chaves que assinam a sessão (DataProtection) ficam no volume `app_keys` (`/app/keys`). Sem isso, cada deploy gerava
  chaves novas e deslogava todo mundo. Perder o volume desloga todos uma vez e invalida links de e-mail ainda não usados;
  não perde dado nenhum.
- A sessão é revalidada a cada 2 minutos: inativar a conta, encerrar as sessões ou trocar a senha derruba o acesso em até
  esse tempo. O token da API (app) também confere se a conta está ativa.

## Limite de requisições por IP

| Limite | Padrão | Configuração |
|---|---|---|
| Site todo, por IP | 50 req/s (rajada de 100) | `Security__RateLimit__RequestsPerSecondPerIp` |
| Envios (POST) de login, 2FA, código de recuperação, cadastro, "esqueci a senha", reenvio de confirmação e redefinição, nas páginas e na API (`/api/auth/login`, `register`, `refresh` e `forgot-password`), por IP | 20 por minuto | `Security__RateLimit__AuthRequestsPerMinutePerIp` |

Passou do limite: resposta `429` com `Retry-After`, registrada no log (`Monburan.RateLimit`). No IPv6 o "mesmo cliente" é o
bloco `/64`. `/health` não é limitado. O limite complementa o bloqueio da conta: o bloqueio protege uma conta; o limite
protege contra quem tenta muitas contas. O contador fica em memória, por instância do app (hoje há uma).

## Política de conteúdo (CSP)

Scripts só de arquivos do próprio site: nada de `<script>` com código dentro da página, nada de `eval`. Estilos só do site e
do Google Fonts; sem plugins; formulários só para o próprio site; a página nunca dentro de um frame.

- **Script novo:** coloque em um arquivo de `wwwroot` e referencie em `Components/App.razor`. Código inline não roda.
- **Emergência:** se uma política nova quebrar uma página em produção, `Security__CspReportOnly=true` passa a política para
  modo de aviso (o navegador só registra a violação no console). Volte para `false` depois de corrigir.
- Os testes `ContentSecurityPolicyApiTests` e `CspEnforcementTests` garantem a política; os testes de ponta a ponta falham se
  qualquer página gerar violação de CSP.

## Administração do site

Só o papel **Admin** abre as telas abaixo. Em produção, o primeiro administrador vem de `ADMIN_EMAILS` (veja
[IA-PROVEDORES.md](IA-PROVEDORES.md#quem-pode-abrir-a-tela)).

**Primeiro administrador em produção.** O seed que cria administradores só roda em desenvolvimento (`admin@monburan.com.br` e
`adm@monburan.com.br`, com as senhas do próprio seed; o repositório é público, então essas senhas nunca servem fora do ambiente
local). Em produção:

1. Coloque o e-mail em `ADMIN_EMAILS` no `.env` da VPS e reinicie o app.
2. Cadastre-se no site com esse e-mail e uma senha que cumpra a regra (10 ou mais caracteres, com maiúscula, minúscula e número).
3. Confirme o e-mail pelo link recebido; sem e-mail configurado, o comando de confirmação direto no banco está em
   [IA-PROVEDORES.md](IA-PROVEDORES.md#quem-pode-abrir-a-tela).
4. No primeiro acesso, ligue o 2FA (veja abaixo).

**Verificação em duas etapas (2FA) obrigatória.** Quem é administrador só abre a administração com o 2FA ligado na conta;
sem ele vê um aviso com o botão para a configuração (`/Account/Manage/TwoFactorAuthentication`). Perdeu o aparelho e os
códigos de recuperação:

1. Outro administrador abre **Contas**, escolhe a pessoa e usa **Redefinir 2FA**; ou
2. se for o único administrador, `REQUIRE_ADMIN_2FA=false` no `.env` da VPS e `docker compose up -d --force-recreate app`
   reabre o acesso até a pessoa reconfigurar. Volte para `true` em seguida.

### Contas (`/admin/accounts`)

Busca por nome ou e-mail, filtros por tipo (candidato, empresa, administrador) e por situação (ativa, inativa, bloqueada por
tentativas, e-mail não confirmado), e um painel por conta com:

| Ação | Efeito |
|---|---|
| **Inativar** (com motivo) | A conta é desconectada na hora e não entra mais. As vagas ativas de uma empresa inativada saem do ar; na reativação voltam só as que a inativação tirou. |
| **Reativar** | A conta volta a entrar. |
| **Desbloquear** | Zera o bloqueio por tentativas de login. |
| **Confirmar e-mail** | Confirma o e-mail na mão (útil quando a VPS não envia e-mail). |
| **Encerrar sessões** | Derruba todas as sessões da conta. |
| **Redefinir 2FA** | Desliga o 2FA da conta, para a pessoa configurar de novo. |
| **Tornar administrador / Remover papel** | Concede ou retira o papel. Só contas ativas com e-mail confirmado viram administradoras. |

Travas: ninguém inativa a si mesmo nem remove o próprio papel; o último administrador ativo não sai; quem está em
`ADMIN_EMAILS` não perde o papel pela tela (tire o e-mail da lista).

### Vagas (`/admin/jobs`)

Todas as vagas de todas as empresas, com contadores por situação, busca (título, categoria ou empresa) e filtro por
situação. Para cada vaga: **Aprovar e ativar** ou
**Reativar** (uma vaga com validade vencida pede a nova data), **Pausar**, **Ocultar** e **Reprovar** (os dois com motivo),
**Excluir** (com motivo) e um atalho para a página pública. A empresa recebe uma notificação com o motivo, e a vaga oculta ou
reprovada some da busca, da página pública e da API pública (só a empresa dona e a administração a veem). O administrador não
edita o texto da vaga de outra empresa: a moderação é por situação, para o conteúdo continuar sendo da empresa.

### Auditoria (`/admin/audit`)

Toda ação administrativa (contas, vagas e provedores de IA) grava quem fez, em quem, quando, o motivo e o que mudou. Há
busca (quem fez, alvo ou motivo) e filtros por ação e por tipo de alvo. A trilha é só de inclusão: não há tela nem rota para apagar ou editar registros.
Falha ao gravar a trilha nunca derruba a ação, mas é registrada no log.

## Colocar em produção (VPS)

Faça nesta ordem. Os arquivos citados estão em `infra/vps/` e são copiados para `/opt/monburan` na VPS.

> **Antes de começar:** rode um backup do banco (`pg_dump`, como em [CI-CD.md](CI-CD.md#publicar-a-web-vps)) e mantenha uma
> segunda sessão SSH aberta durante os passos 3 e 4.

### 1. Atualizar o compose e o `.env`

1. Copie o `docker-compose.prod.yml` novo para `/opt/monburan/docker-compose.yml` (ele cria o volume `app_keys`, passa as
   variáveis abaixo e carrega o `ai-keys.env`). Exige Docker Compose 2.24 ou mais novo (`docker compose version`).
2. Confira o `.env` (modelo em `env.example`):

| Variável | Para quê |
|---|---|
| `ADMIN_EMAILS` | E-mails dos administradores (a conta precisa ter o e-mail confirmado) |
| `REQUIRE_ADMIN_2FA` | `true` (padrão): administrador só entra com 2FA; `false` só em emergência |
| `ALLOWED_HOSTS` | `monburan.com.br;www.monburan.com.br;127.0.0.1`: qualquer outro `Host` recebe 400 |

3. `chmod 600 .env ai-keys.env` e `docker compose up -d --force-recreate app`. As migrations rodam no boot e só
   **acrescentam** (tabelas e colunas novas, sem alterar os dados existentes): nenhuma conta existente fica inativa.
4. Confira: entre no site, faça um novo deploy (`docker compose up -d --force-recreate app`) e atualize a página: você
   continua logado. Com o DevTools aberto, o cookie `.AspNetCore.Identity.Application` aparece com `Secure` e `HttpOnly`.

### 2. nginx

Copie o `nginx.conf` novo para o arquivo do site que `/etc/nginx/sites-enabled/` aponta (`ls -l /etc/nginx/sites-enabled/`: o
`deploy.sh` usa `/etc/nginx/sites-available/monburan.com.br`, mas uma instalação feita à mão pode ter outro nome; guarde uma
cópia do arquivo antigo fora do `sites-enabled`), rode `nginx -t` e `systemctl reload nginx`. Ele esconde a versão, descarta acessos por IP ou por domínio de terceiros (resposta
vazia), envia só o IP visto pelo nginx e encurta os tempos de cabeçalho e corpo. Se o `nginx -t` reclamar de
`default_server` duplicado, tire esse `default_server` do outro site.

### 3. Firewall, SSH, fail2ban e atualizações automáticas

```bash
cd /opt/monburan
sudo bash harden.sh                    # plano: mostra tudo e não altera nada
sudo bash harden.sh --apply            # aplica
```

O script instala `ufw`, `fail2ban` e `unattended-upgrades` e liga o firewall só com o SSH (porta lida do `sshd` e da sua
sessão), 80 e 443. O SSH passa a aceitar **só chave**. Travas: só mexe no SSH se a conta de administração já tem chave em
`~/.ssh/authorized_keys`, pergunta se você já entrou por chave, valida a configuração (`sshd -t` e `sshd -T`) antes de
recarregar e desfaz o que escreveu se algo não bater. **Depois de aplicar, não feche a sessão: abra outra e confirme que
entra por chave.** O fail2ban bloqueia por 1 hora quem erra o SSH 4 vezes em 10 minutos
(`FAIL2BAN_IGNOREIP="seu.ip.fixo"` evita bloquear você). As atualizações de segurança do sistema entram sozinhas, **sem
reinício automático**: de vez em quando confira `/var/run/reboot-required` e reinicie numa hora calma. Isso só protege um
sistema que ainda recebe correções: confira a versão (`cat /etc/os-release`) e veja o fim de "O que ainda não está coberto".

Para desfazer o SSH: apague `/etc/ssh/sshd_config.d/00-monburan-hardening.conf` e recarregue o `ssh`. O Docker publica
portas pelo iptables e **ignora** o ufw: o compose só publica o app em `127.0.0.1:8080` e não publica o Postgres; qualquer
porta nova publicada em `0.0.0.0` ficaria exposta. A verificação final do script lista o que escuta fora do loopback.

Recomendado em seguida: criar um usuário próprio com `sudo`, copiar a chave para ele e trocar `PermitRootLogin` para `no`.

### 4. Backup criptografado fora do servidor

O que vai: o banco (`pg_dump`) e a pasta de uploads, criptografados com [age](https://github.com/FiloSottile/age) antes de
tocar o disco e enviados com [rclone](https://rclone.org). Quem invadir a VPS ou o armazenamento remoto só encontra
arquivos que não consegue abrir, porque a **chave privada não fica na VPS**.

1. Na sua máquina (não na VPS): `age-keygen -o monburan-backup-key.txt`. A linha `Public key: age1...` vai para a VPS; o
   arquivo inteiro é a chave privada: guarde no gerenciador de senhas e em uma cópia offline. **Sem ela, os backups não
   abrem.**
2. Na VPS: `apt-get install -y age rclone`, copie `backup.sh`, `restore-test.sh` e `backup.env.example` para `/opt/monburan`
   e faça `cp backup.env.example backup.env && chmod 600 backup.env`. Preencha `BACKUP_AGE_RECIPIENT` e `BACKUP_REMOTE`.
3. Configure o destino com `rclone config` (como root, que roda o timer) e teste com `rclone lsd <nome>:`. Qualquer serviço
   do rclone serve (Backblaze B2, S3, Google Drive...). Ou ponha as variáveis `RCLONE_CONFIG_<NOME>_*` no `backup.env`.
4. Rode o primeiro backup: `bash backup.sh`. Depois ligue o diário:

```bash
sudo cp systemd/monburan-backup.service systemd/monburan-backup.timer /etc/systemd/system/
sudo systemctl daemon-reload && sudo systemctl enable --now monburan-backup.timer
systemctl list-timers monburan-backup.timer          # próxima execução (todo dia por volta de 03:30)
journalctl -u monburan-backup -n 30 --no-pager       # resultado da última
```

Retenção: 7 dias no disco da VPS e 30 dias no remoto (`KEEP_LOCAL_DAYS`, `KEEP_REMOTE_DAYS`). **Quem rouba a VPS leva a
credencial do remoto junto**: prefira uma credencial que só envia e lista (sem apagar) e deixe a retenção por conta do
armazenamento (regra de ciclo de vida do bucket); nesse caso use `KEEP_REMOTE_DAYS=0`. Opcional: `HEALTHCHECK_URL` com um
monitor (por exemplo healthchecks.io) que avisa quando o backup falha **ou para de rodar**.

**Teste a restauração todo mês** (e depois de mexer no backup): um backup que nunca foi restaurado é só uma esperança.

```bash
# na SUA máquina (Docker, age e rclone instalados; rclone com uma credencial que lê o remoto), não na VPS
BACKUP_REMOTE=<nome-do-rclone>:monburan-backups/vps1 \
  bash restore-test.sh --latest-remote --identity /caminho/monburan-backup-key.txt
```

Rodar fora da VPS mantém a chave privada longe dela. O script baixa o backup mais recente, descriptografa, restaura em um PostgreSQL descartável (sem portas publicadas, dados em
memória), confere contas, vagas, empresas, candidatos e migrations e confere que o backup dos uploads abre. Não toca em
produção.

**Restauração de verdade** (servidor novo ou banco perdido):

```bash
age -d -i monburan-backup-key.txt monburan-db-<data>.dump.age > monburan.dump
docker compose up -d postgres                       # banco novo e vazio
docker exec -i monburan-postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --no-owner' < monburan.dump
age -d -i monburan-backup-key.txt monburan-uploads-<data>.tar.gz.age | docker exec -i monburan-app tar -xzf - -C /app/wwwroot
docker compose up -d app
```

As chaves de sessão (`app_keys`) não entram no backup: depois de uma restauração em servidor novo, todos entram de novo uma
vez. Isso é esperado.

### Como conferir que está valendo

| O quê | Como |
|---|---|
| Firewall | `ufw status verbose`; `ss -ltn` só com SSH, 80 e 443 fora do loopback |
| SSH | `sshd -T \| grep -E '^(passwordauthentication\|permitrootlogin)'` mostra `no` e `without-password`; de outra máquina, `ssh -o PubkeyAuthentication=no usuario@servidor` recusa |
| fail2ban | `fail2ban-client status sshd` |
| Cabeçalhos | `curl -sI https://monburan.com.br/ \| grep -iE 'strict-transport\|content-security\|^server'` |
| Limite por IP | 25 envios seguidos de login pela API devolvem `429` a partir do 21º (você fica bloqueado por até 1 minuto): `for i in $(seq 25); do curl -s -o /dev/null -w '%{http_code}\n' -X POST https://monburan.com.br/api/auth/login -H 'Content-Type: application/json' -d '{"email":"a@a.com","password":"x"}'; done \| sort \| uniq -c` |
| Backup | `systemctl list-timers monburan-backup.timer`; `rclone ls <remoto>`; `restore-test.sh` todo mês |

## Repositório (GitHub)

- **Dependabot** (`.github/dependabot.yml`): PRs semanais **para a `dev`** com atualizações de NuGet (correções e melhorias
  juntas em um PR; versão maior em PR próprio), ações do GitHub, imagens do Dockerfile e dos composes. O PostgreSQL fica de
  fora das versões maiores: trocar de versão maior com dados existentes exige dump e restauração. Alertas e atualizações de
  segurança do Dependabot estão ligados nas configurações do repositório e abrem PR fora dessa agenda.
- **CodeQL** (`.github/workflows/codeql.yml`): análise de C#, JavaScript e dos próprios workflows em PRs e pushes para `dev`
  e `main`, e toda segunda-feira. Os alertas aparecem em *Security → Code scanning*.
- **Secret scanning** com **bloqueio de push** ligado: um push com chave conhecida (token do GitHub, chave de nuvem, etc.) é
  recusado. A esteira ainda varre os arquivos versionados atrás de formatos de chave (`security-scan`).
- **Scripts da VPS** (`.github/workflows/infra-scripts.yml`): quando algo em `infra/vps` muda, roda ShellCheck e testes de
  verdade em contêineres descartáveis (`infra/vps/tests`): o endurecimento (incluindo as travas contra ficar sem acesso),
  o backup com a restauração e o `nginx.conf`. Para rodar na sua máquina (só precisa de Docker):
  `bash infra/vps/tests/harden-smoke.sh`, `backup-restore-smoke.sh` e `nginx-smoke.sh`.
- **Proteção das branches `dev` e `main`:** as mudanças entram por pull request, com `build-and-test`, `e2e` e
  `security-scan` passando, e a branch não aceita push forçado nem exclusão. Não exige aprovação de revisão (o repositório
  tem uma pessoa só) e não vale para administradores do repositório, que continuam podendo agir numa emergência (é o botão
  "mesclar sem esperar os requisitos"). A regra "a `main` só recebe PR vindo da `dev`" continua sendo de processo (veja
  `AGENTS.md`). Os testes de `infra/vps` não são exigidos: o workflow só roda quando `infra/vps` muda. Para desfazer:
  *Settings → Branches*, ou `gh api -X DELETE repos/RondineleG/Monburan/branches/<branch>/protection`.

## Banco de dados: local ou gerenciado?

**Hoje: fique com o PostgreSQL local** (contêiner na mesma VPS), com o backup criptografado diário e o teste mensal de
restauração acima. Para um site de uma instância só, o que mais faltava era justamente o backup fora do servidor, e ele
agora existe. Migrar agora trocaria um problema pequeno (operar um contêiner) por custo fixo e uma dependência a mais.

O que o banco local **não** dá: recuperação até um instante qualquer (o backup é diário: o pior caso é perder até 24 h),
failover automático, correções do PostgreSQL aplicadas por terceiros e um banco em um servidor separado do app (hoje um disco
ou uma VPS perdida leva os dois; o backup remoto cobre o dado, não o tempo fora do ar).

**Migre para um PostgreSQL gerenciado** (região do Brasil, mesma versão maior ou mais nova, TLS obrigatório) quando algum
destes pontos aparecer: pagantes ou contrato que não aceitam perder 24 h de dados; o banco disputando CPU e memória com o
app; necessidade de mais de uma instância do app (aí o limite por IP em memória, o limitador de IA e as chaves de sessão em
disco local também precisam sair do processo); ou quando operar o banco ocupar mais tempo do que vale.

Como migrar (a conta é barata, porque só muda a string de conexão):

1. Crie o banco gerenciado e libere o IP da VPS no firewall dele.
2. Em uma janela curta: pare o app, rode um `pg_dump` novo, restaure no gerenciado com
   `pg_restore --no-owner --no-acl` e confira as contagens (como o `restore-test.sh` faz).
3. Troque `ConnectionStrings__DefaultConnection` no compose para o host novo, com `SSL Mode=Require`, tire o serviço
   `postgres` do compose e suba o app. Para voltar atrás, aponte de novo para o banco local (guarde o contêiner e o volume
   por uma semana).
4. Adapte o `backup.sh`: ele faz o `pg_dump` pelo contêiner local; com o banco gerenciado o dump passa a vir de um contêiner
   avulso do `postgres` apontando para o host novo. Os backups do próprio serviço entram como camada extra, não no lugar do
   backup criptografado e restaurável por você.

## Se algo der errado

| Situação | O que fazer |
|---|---|
| Uma chave ou senha vazou | Troque **na origem primeiro** (provedor de IA, SMTP, GitHub), atualize `.env` ou `ai-keys.env` e rode `docker compose up -d --force-recreate app`. Trocar `JWT_SIGNING_KEY` derruba os tokens do app (todos entram de novo). Senha do banco: `ALTER USER` no Postgres e depois o `.env`. |
| Conta suspeita de invasão | **Administração → Contas → Inativar** (desconecta na hora), veja a **Auditoria**, reative depois de a pessoa trocar a senha e o 2FA. |
| Derrubar todas as sessões | Troque o carimbo de segurança de todas as contas (comando abaixo): todos saem em até 2 minutos. |
| Servidor suspeito de invasão | Isole (bloqueie tudo no firewall do painel da hospedagem ou desligue), restaure em um servidor novo a partir do backup remoto e **troque tudo**: `JWT_SIGNING_KEY`, senha do banco, SMTP, chaves de IA e chaves SSH. |
| O app não sobe depois de uma mudança de configuração | `docker logs monburan-app --tail 50` mostra o motivo; desfaça a última alteração do `.env` ou do compose e suba de novo. |
| Trancou-se fora do SSH | Console da hospedagem (acesso de emergência pelo painel): apague `/etc/ssh/sshd_config.d/00-monburan-hardening.conf`, `systemctl reload ssh` e, se foi o firewall, `ufw disable`. |

Comando da terceira linha (derrubar todas as sessões):

```bash
docker exec monburan-postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  -c "UPDATE \"Identity\".\"Users\" SET \"SecurityStamp\" = gen_random_uuid()::text"'
```

## O que ainda não está coberto

- **Sistema operacional sem suporte:** o Debian 11 (bullseye) saiu do suporte de segurança e seus pacotes vêm de
  `archive.debian.org`: kernel, OpenSSH, OpenSSL e o resto não recebem mais correção. O firewall, o SSH só por chave e o
  fail2ban reduzem a exposição, mas não substituem trocar para uma versão suportada (Debian 12 ou 13, Ubuntu LTS), de
  preferência em um servidor novo restaurado a partir do backup desta página. Confira com `cat /etc/os-release`.
- **Contêiner sem root:** o app roda como root dentro do contêiner. Mudar exige ajustar o dono dos volumes que já existem.
- **WAF ou CDN** (por exemplo Cloudflare) na frente: o nginx e os limites tratam abuso simples, não ataque volumétrico. Se
  colocar um proxy na frente, o `nginx.conf` e a confiança em `X-Forwarded-For` precisam ser revistos.
- **CAPTCHA** no cadastro e no login: hoje a defesa é bloqueio de conta, limite por IP e 2FA (obrigatório só para
  administradores; candidatos e empresas podem ligar).
- **Monitoramento:** não há alerta de queda do site nem de erro em produção além do `HEALTHCHECK_URL` do backup.
- **Varredura da imagem Docker** (por exemplo Trivy) e inventário de componentes (SBOM) na esteira.
- **Revisão externa:** teste de invasão e revisão jurídica da política de privacidade (LGPD).
