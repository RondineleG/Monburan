#!/usr/bin/env bash
# ============================================================
# Monburan — Teste de fumaça do provision.sh e do deploy.sh
#
# Roda os dois scripts de verdade em um Debian 13 descartável (contêiner):
#   - provision.sh: instala nginx, certbot, age e rclone (confirma que os pacotes existem no Debian 13), baixa a chave do
#     repositório do Docker, confere a impressão digital e confirma que o docker-ce está disponível para o Debian 13;
#   - deploy.sh: primeira vez (sem certificado: nginx em HTTP, certbot, nginx.conf completo), segunda vez, backup que falha,
#     certbot que falha, --skip-tls, site com outro nome e nginx.conf recusado.
#   - deploy.sh, permissões: arquivos com outro dono voltam a ser só do root.
#   - deploy.sh, versão nova: download da imagem que falha (--skip-pull) e app que fica na imagem anterior.
#   - deploy.sh, disco: limpeza das tags de rollback antigas.
# Docker, certbot e systemctl são trocados por registradores de chamadas (o contêiner não tem systemd nem Docker); o nginx é o de
# verdade. Não altera a máquina de quem roda: precisa só de Docker.
#
#   bash infra/vps/tests/deploy-smoke.sh
# ============================================================
set -uo pipefail

if [[ "${INSIDE_CONTAINER:-0}" != "1" ]]; then
    repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
    exec docker run --rm -e INSIDE_CONTAINER=1 -v "$repo":/repo:ro "${TEST_IMAGE:-debian:trixie}" \
        bash /repo/infra/vps/tests/deploy-smoke.sh
fi

PROVISION=/repo/infra/vps/provision.sh
DEPLOY=/repo/infra/vps/deploy.sh
OUT=/tmp/smoke.out
DOCKER_CALLS=/tmp/docker-calls.log
CERTBOT_CALLS=/tmp/certbot-calls.log
DIR=/tmp/monburan-test
SITE=/etc/nginx/sites-available/monburan.com.br
passed=0
failed=0

ok() { passed=$((passed + 1)); printf '  ok     %s\n' "$1"; }
bad() {
    failed=$((failed + 1))
    printf '  FALHOU %s\n' "$1"
    tail -12 "$OUT" 2>/dev/null | sed 's/^/           | /'
}

# expect <descrição> <comando...>: o comando precisa terminar com sucesso
expect() {
    local name=$1
    shift
    if "$@" >/dev/null 2>&1; then ok "$name"; else bad "$name"; fi
}

# expect_soon: igual ao expect, mas repete por até 5 s (o reload do nginx é assíncrono: o socket novo leva alguns décimos de segundo)
expect_soon() {
    local name=$1 _
    shift
    for _ in $(seq 1 10); do
        if "$@" >/dev/null 2>&1; then ok "$name"; return; fi
        sleep 0.5
    done
    bad "$name"
}

# Roda um script com a saída guardada em $OUT; o status fica em $status
runit() {
    "$@" >"$OUT" 2>&1
    status=$?
}

export DEBIAN_FRONTEND=noninteractive

echo "1. provision.sh: plano (não altera nada)"
apt-get update -qq >/dev/null 2>&1
apt-get install -y -qq ca-certificates curl gnupg openssl >/dev/null 2>&1
runit bash "$PROVISION"
expect "termina sem erro" test "$status" -eq 0
expect "mostra o plano" grep -q '\[plano\] apt-get install -y ca-certificates curl gnupg openssl nginx certbot' "$OUT"
expect "mostra o repositório do Docker para o Debian 13" grep -q 'download.docker.com/linux/debian trixie stable' "$OUT"
expect "não instalou o nginx" test ! -x /usr/sbin/nginx
expect "não criou a pasta do app" test ! -d /opt/monburan

echo
echo "2. provision.sh: aplicação (sem Docker nem swap, que o contêiner não comporta)"
runit bash "$PROVISION" --apply --skip-docker --skip-swap
expect "termina sem erro" test "$status" -eq 0
expect "instalou nginx, certbot, age e rclone (os pacotes existem no Debian 13)" bash -c "command -v nginx certbot age rclone"
expect "criou /opt/monburan e os backups só do dono (700)" bash -c "test \"\$(stat -c %a /opt/monburan/backups)\" = 700"
expect "criou /var/www/certbot" test -d /var/www/certbot
expect "removeu o site padrão do nginx" test ! -e /etc/nginx/sites-enabled/default
expect "limitou os registros do Docker" grep -q '"max-size": "10m"' /etc/docker/daemon.json
expect "limitou o journal do sistema" grep -q '^SystemMaxUse=300M$' /etc/systemd/journald.conf.d/monburan.conf
runit bash "$PROVISION" --apply --skip-docker --skip-swap
expect "rodar de novo também funciona (idempotente)" test "$status" -eq 0

echo
echo "3. provision.sh: repositório do Docker (rede de verdade)"
# Roda comandos com as funções do provision.sh carregadas (o script em si não executa)
with_provision() { PROVISION_SOURCE_ONLY=1 bash -c "source $PROVISION; APPLY=1; $1"; }
runit with_provision "detect_os; add_docker_repo"
expect "baixa a chave, confere a impressão digital e escreve o repositório" bash -c "grep -q 'signed-by=/etc/apt/keyrings/docker.asc' /etc/apt/sources.list.d/docker.list && test -s /etc/apt/keyrings/docker.asc"
expect "o docker-ce está disponível para o Debian 13" bash -c "apt-cache policy docker-ce | grep -q 'Candidate: [0-9]'"
expect "a instalação do Docker e do compose é possível (simulada)" apt-get install -s docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

rm -f /etc/apt/sources.list.d/docker.list /etc/apt/keyrings/docker.asc
runit with_provision "DOCKER_KEY_FPR=0000000000000000000000000000000000000000; detect_os; add_docker_repo"
expect "com a impressão digital errada recusa" test "$status" -ne 0
expect "explica que não reconhece a origem" grep -q 'impressão digital da chave do Docker não confere' "$OUT"
expect "e não instala a chave nem o repositório" bash -c "test ! -e /etc/apt/keyrings/docker.asc && test ! -e /etc/apt/sources.list.d/docker.list"

cp /etc/os-release /tmp/os-release.orig
printf 'ID=fedora\nPRETTY_NAME="Fedora Linux"\n' >/etc/os-release
runit bash "$PROVISION"
cp /tmp/os-release.orig /etc/os-release
expect "sistema que não é Debian nem Ubuntu é recusado" grep -q 'sistema não suportado' "$OUT"

echo
echo "4. deploy.sh: preparação do ambiente de teste"
mkdir -p /stubs /stubs/pki
cat >/stubs/docker <<'STUB'
#!/bin/sh
echo "docker $*" >> /tmp/docker-calls.log
if [ "$1 $2" = "compose version" ]; then echo "Docker Compose version v2.test"; exit 0; fi
# Download da imagem que falha (token do GHCR vencido, registro fora do ar)
if [ "$1 $2 $3" = "compose pull app" ] && [ -e /tmp/docker-pull-fail ]; then echo "pull: falha simulada" >&2; exit 1; fi
if [ "$1 $2" = "compose up" ]; then touch /tmp/docker-has-app; exit 0; fi
# A imagem baixada; com /tmp/docker-stale o contêiner ficou na anterior
if [ "$1 $2" = "image inspect" ]; then
    if [ -e /tmp/docker-stale ]; then echo "sha256:def456"; else echo "sha256:abc123"; fi
    exit 0
fi
if [ "$1 $2" = "image ls" ]; then
    [ -e /tmp/docker-rollback-tags ] && cat /tmp/docker-rollback-tags
    exit 0
fi
if [ "$1" = "inspect" ]; then
    [ -e /tmp/docker-has-app ] || exit 1
    case "$*" in
        *Config.Image*) echo "ghcr.io/rondineleg/monburan:latest" ;;
        *.Image*) echo "sha256:abc123" ;;
    esac
    exit 0
fi
exit 0
STUB
cat >/stubs/certbot <<'STUB'
#!/bin/sh
echo "certbot $*" >> /tmp/certbot-calls.log
if [ -e /tmp/certbot-fail ]; then echo "certbot: falha simulada" >&2; exit 1; fi
live=/etc/letsencrypt/live/monburan.com.br
mkdir -p "$live"
cp /stubs/pki/fullchain.pem /stubs/pki/privkey.pem "$live/"
cp /stubs/pki/options-ssl-nginx.conf /stubs/pki/ssl-dhparams.pem /etc/letsencrypt/
exit 0
STUB
cat >/stubs/systemctl <<'STUB'
#!/bin/sh
case "$*" in
    "is-active --quiet nginx") [ -s /run/nginx.pid ] && kill -0 "$(cat /run/nginx.pid)" 2>/dev/null ;;
    "reload nginx") nginx -s reload ;;
    "restart nginx") nginx -s stop 2>/dev/null; sleep 1; nginx ;;
    *) exit 0 ;;
esac
STUB
chmod +x /stubs/docker /stubs/certbot /stubs/systemctl
openssl req -x509 -newkey rsa:2048 -nodes -days 30 -subj "/CN=monburan.com.br" \
    -keyout /stubs/pki/privkey.pem -out /stubs/pki/fullchain.pem >/dev/null 2>&1
openssl dhparam -dsaparam -out /stubs/pki/ssl-dhparams.pem 2048 >/dev/null 2>&1
printf 'ssl_session_cache shared:le_nginx_SSL:10m;\nssl_protocols TLSv1.2 TLSv1.3;\n' >/stubs/pki/options-ssl-nginx.conf
export PATH="/stubs:$PATH"

# O "app" de mentira: responde 200 em 127.0.0.1:8080
cat >/etc/nginx/conf.d/fakeapp.conf <<'CONF'
server {
    listen 127.0.0.1:8080;
    location / { default_type text/plain; return 200 "ok\n"; }
}
CONF
rm -f /etc/nginx/sites-enabled/default
nginx
sleep 1

fresh_dir() {
    rm -rf "$DIR"
    mkdir -p "$DIR"
    cp /repo/infra/vps/nginx.conf /repo/infra/vps/env.example /repo/infra/vps/docker-compose.prod.yml "$DIR/"
    rm -f /tmp/docker-has-app /tmp/certbot-fail
    rm -f /tmp/docker-pull-fail /tmp/docker-stale
    rm -f /tmp/docker-rollback-tags
    : >"$DOCKER_CALLS"
    : >"$CERTBOT_CALLS"
}
reset_site() {
    rm -f "$SITE" /etc/nginx/sites-available/monburan.conf /etc/nginx/sites-enabled/* /etc/nginx/sites-available/*.bak-*
    rm -rf /etc/letsencrypt/live /etc/letsencrypt/options-ssl-nginx.conf /etc/letsencrypt/ssl-dhparams.pem
    nginx -s reload 2>/dev/null
}
deploy() { INSTALL_DIR="$DIR" runit bash "$DEPLOY" "$@"; }

echo
echo "5. deploy.sh: primeira vez (sem .env, sem certificado)"
fresh_dir
reset_site
deploy --email teste@example.com --admin-email adm@example.com
expect "termina sem erro" test "$status" -eq 0
expect ".env criado só para o dono (600)" test "$(stat -c %a "$DIR/.env")" = 600
expect ".env sem valores de exemplo" bash -c "! grep -q TROQUE '$DIR/.env'"
expect "senha do banco gerada (48 caracteres hexadecimais)" bash -c "grep -qE '^DB_PASSWORD=[0-9a-f]{48}\$' '$DIR/.env'"
expect "chave JWT gerada (60 caracteres ou mais)" bash -c "test \$(awk -F= '/^JWT_SIGNING_KEY=/ { print length(\$0) - 16 }' '$DIR/.env') -ge 60"
expect "hosts permitidos restritos ao domínio" grep -q '^ALLOWED_HOSTS=monburan.com.br;www.monburan.com.br;127.0.0.1$' "$DIR/.env"
expect "ADMIN_EMAILS definido" grep -q '^ADMIN_EMAILS=adm@example.com$' "$DIR/.env"
expect "os segredos gerados não aparecem na saída" bash -c "! grep -qF \"\$(awk -F= '/^DB_PASSWORD=/ { print \$2 }' '$DIR/.env')\" $OUT"
expect "criou o docker-compose.yml" test -f "$DIR/docker-compose.yml"
expect "baixou a imagem e subiu a stack" bash -c "grep -q 'compose pull app' $DOCKER_CALLS && grep -q 'compose up -d --remove-orphans' $DOCKER_CALLS"
expect "pediu o certificado ao certbot com o e-mail" grep -q 'certbot --nginx -d monburan.com.br -d www.monburan.com.br --non-interactive --agree-tos -m teste@example.com' "$CERTBOT_CALLS"
expect "o nginx.conf completo entrou no lugar do provisório" cmp -s "$DIR/nginx.conf" "$SITE"
expect "o site está habilitado" test -L /etc/nginx/sites-enabled/monburan.com.br
expect_soon "HTTPS responde pelo proxy" bash -c "curl -sk --resolve monburan.com.br:443:127.0.0.1 https://monburan.com.br/health | grep -q ok"
expect_soon "HTTP redireciona para HTTPS" bash -c "test \"\$(curl -s -o /dev/null -w '%{http_code}' --resolve monburan.com.br:80:127.0.0.1 http://monburan.com.br/)\" = 301"
expect "quem chega pelo IP é descartado" bash -c "! curl -sf --max-time 5 http://127.0.0.1/"

echo
echo "6. deploy.sh: segunda vez (nada muda, nada é pedido de novo)"
sum_before=$(sha256sum "$DIR/.env" | cut -c1-64)
: >"$CERTBOT_CALLS"
: >"$DOCKER_CALLS"
touch /tmp/docker-has-app
deploy
expect "termina sem erro" test "$status" -eq 0
expect "o .env não muda" test "$(sha256sum "$DIR/.env" | cut -c1-64)" = "$sum_before"
expect "o certbot não é chamado de novo" test ! -s "$CERTBOT_CALLS"
expect "guarda a imagem atual como rollback" grep -qE 'docker tag sha256:abc123 ghcr.io/rondineleg/monburan:rollback-[0-9]{8}-[0-9]{6}' "$DOCKER_CALLS"
expect_soon "o site continua respondendo" bash -c "curl -sk --resolve monburan.com.br:443:127.0.0.1 https://monburan.com.br/health | grep -q ok"

echo
echo "7. deploy.sh: backup que falha impede a troca da versão"
printf '#!/usr/bin/env bash\necho "backup de mentira falhou"; exit 1\n' >"$DIR/backup.sh"
printf 'BACKUP_AGE_RECIPIENT=age1x\n' >"$DIR/backup.env"
: >"$DOCKER_CALLS"
deploy
expect "falha" test "$status" -ne 0
expect "explica que não troca a versão sem backup" grep -q 'o backup falhou' "$OUT"
expect "não baixou nem subiu nada depois do backup" bash -c "! grep -q 'compose pull' $DOCKER_CALLS"
: >"$DOCKER_CALLS"
deploy --no-backup
expect "--no-backup pula o backup e conclui" test "$status" -eq 0
rm -f "$DIR/backup.sh" "$DIR/backup.env"

echo
echo "8. deploy.sh: .env com valores de exemplo é recusado"
cp "$DIR/.env" /tmp/env.good
sed -i 's/^DB_PASSWORD=.*/DB_PASSWORD=TROQUE_POR_UMA_SENHA_FORTE_AQUI/' "$DIR/.env"
deploy
expect "falha" test "$status" -ne 0
expect "pede para trocar" grep -q 'valores de exemplo' "$OUT"
cp /tmp/env.good "$DIR/.env"

echo
echo "9. deploy.sh: o certbot falha (DNS ainda não aponta para o servidor)"
fresh_dir
reset_site
touch /tmp/certbot-fail
deploy --email teste@example.com
expect "sai com erro (TLS pendente)" test "$status" -ne 0
expect "explica o DNS" grep -q 'DNS' "$OUT"
expect "o site fica em HTTP, com o proxy provisório" grep -q 'Provisório' "$SITE"
expect "o nginx continua válido" nginx -t
expect_soon "e o app responde em HTTP pelo nome" bash -c "curl -s --resolve monburan.com.br:80:127.0.0.1 http://monburan.com.br/health | grep -q ok"

echo
echo "10. deploy.sh: --skip-tls"
fresh_dir
reset_site
deploy --skip-tls
expect "não chama o certbot" test ! -s "$CERTBOT_CALLS"
expect "sai com erro avisando que o TLS está pendente" bash -c "test $status -ne 0 && grep -q 'sem certificado' $OUT"

echo
echo "11. deploy.sh: o site do domínio já existe com outro nome"
fresh_dir
reset_site
printf 'server { listen 80; server_name monburan.com.br www.monburan.com.br; return 200 "antigo"; }\n' >/etc/nginx/sites-available/monburan.conf
ln -sf /etc/nginx/sites-available/monburan.conf /etc/nginx/sites-enabled/monburan.conf
nginx -s reload
deploy --email teste@example.com
expect "termina sem erro" test "$status" -eq 0
expect "atualiza o arquivo que já existia" cmp -s "$DIR/nginx.conf" /etc/nginx/sites-available/monburan.conf
expect "não cria um segundo site para o mesmo domínio" test ! -e "$SITE"
expect "guarda uma cópia do arquivo antigo" bash -c "ls /etc/nginx/sites-available/monburan.conf.bak-* >/dev/null 2>&1"

echo
echo "12. deploy.sh: o nginx recusa o nginx.conf novo (a configuração anterior é mantida)"
cp /etc/nginx/sites-available/monburan.conf /tmp/site.good
printf '\nisto_nao_e_uma_diretiva_valida on;\n' >>"$DIR/nginx.conf"
deploy --email teste@example.com
expect "falha" test "$status" -ne 0
expect "volta à configuração anterior" cmp -s /tmp/site.good /etc/nginx/sites-available/monburan.conf
expect_soon "o nginx continua válido e respondendo" bash -c "nginx -t && curl -sk --resolve monburan.com.br:443:127.0.0.1 https://monburan.com.br/health | grep -q ok"

echo
echo "13. deploy.sh: arquivos com outro dono (o tar do deploy automático gravava o uid do runner) voltam para o root"
fresh_dir
reset_site
deploy --email teste@example.com
expect "a instalação de base termina sem erro" test "$status" -eq 0
chown -R 1001:1001 "$DIR"
chmod g+w "$DIR/nginx.conf"
deploy
expect "termina sem erro" test "$status" -eq 0
expect "a pasta e os arquivos voltam a ser do root" bash -c "! find $DIR \\( ! -user 0 -o ! -group 0 \\) | grep -q ."
expect "sem escrita para grupo e outros" bash -c "! find $DIR -perm /022 | grep -q ."
expect "o .env continua só do dono (600)" test "$(stat -c %a "$DIR/.env")" = 600
expect "avisa do que corrigiu" grep -q 'outro dono' "$OUT"

echo
echo "14. deploy.sh: o download da imagem nova falha (token do GHCR vencido, registro fora do ar)"
fresh_dir
reset_site
deploy --email teste@example.com
expect "a instalação de base termina sem erro" test "$status" -eq 0
touch /tmp/docker-pull-fail
: >"$DOCKER_CALLS"
deploy
expect "falha (antes terminava como concluído, ainda na versão antiga)" test "$status" -ne 0
expect "explica que nada foi trocado" grep -q 'nada foi trocado' "$OUT"
expect "não sobe a stack depois do download que falhou" bash -c "! grep -q 'compose up' $DOCKER_CALLS"
: >"$DOCKER_CALLS"
deploy --skip-pull
expect "--skip-pull sobe a imagem que já está no servidor" test "$status" -eq 0
expect "e não tenta baixar" bash -c "! grep -q 'compose pull' $DOCKER_CALLS"
rm -f /tmp/docker-pull-fail

echo
echo "15. deploy.sh: o app continua na imagem anterior depois do up"
touch /tmp/docker-stale
deploy
expect "falha" test "$status" -ne 0
expect "explica que o app ficou na imagem anterior" grep -q 'continua na imagem anterior' "$OUT"
rm -f /tmp/docker-stale

echo
echo "16. deploy.sh: as tags de rollback antigas são apagadas (ficam as 3 últimas)"
printf '%s\n' latest rollback-20261017-090925 rollback-20261019-175235 rollback-20261018-130942 \
    rollback-20261019-101313 rollback-20261019-105510 >/tmp/docker-rollback-tags
: >"$DOCKER_CALLS"
deploy
expect "termina sem erro" test "$status" -eq 0
expect "apaga as duas mais antigas" bash -c "grep -q 'rmi ghcr.io/rondineleg/monburan:rollback-20261017-090925' $DOCKER_CALLS && grep -q 'rmi ghcr.io/rondineleg/monburan:rollback-20261018-130942' $DOCKER_CALLS"
expect "mantém as três mais recentes" bash -c "! grep -qE 'rmi .*rollback-20261019-(175235|101313|105510)' $DOCKER_CALLS"
expect "não mexe na imagem em uso" bash -c "! grep -q 'rmi .*:latest' $DOCKER_CALLS"
expect "limpa as imagens sem tag" grep -q 'image prune -f' "$DOCKER_CALLS"
rm -f /tmp/docker-rollback-tags

echo
echo "$passed ok, $failed falhas"
[[ $failed -eq 0 ]]
