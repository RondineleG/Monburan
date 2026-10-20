#!/usr/bin/env bash
# ============================================================
# Monburan — Deploy na VPS
#
# Sobe (ou atualiza) a stack Docker (app + Postgres, imagem do GHCR), instala o nginx.conf e, na primeira vez, emite o certificado
# TLS (certbot). Pressupõe o servidor preparado (provision.sh) e os arquivos de infra/vps em /opt/monburan.
#
# Primeira vez: cria o .env com a senha do banco e a chave JWT NOVAS (geradas aqui, nunca exibidas). Se o certificado ainda não
# existe, o nginx sobe só em HTTP, o certbot emite o certificado e só então entra o nginx.conf completo (que depende dele).
# Nas vezes seguintes: tira um backup (se o backup.sh estiver configurado), guarda a imagem atual como rollback, baixa a nova,
# reinicia o app e espera o /health.
# Se o download da imagem nova falhar, para sem trocar nada; depois de subir, confere que o app ficou na imagem baixada.
# Arquivos de /opt/monburan com outro dono (ou com escrita para grupo e outros) voltam a ser só do root.
#
# Uso (como root, na VPS):
#   cd /opt/monburan
#   bash deploy.sh --email voce@exemplo.com --admin-email adm@exemplo.com   # primeira vez
#   bash deploy.sh                                                            # atualizações
#
# Opções:
#   --domain NOME        domínio do site (padrão: monburan.com.br)
#   --email EMAIL        e-mail dos avisos de vencimento do certificado (ou CERTBOT_EMAIL)
#   --admin-email EMAIL  grava ADMIN_EMAILS no .env (quem vira administrador depois de confirmar o e-mail)
#   --skip-tls           não pede certificado (o site fica em HTTP)
#   --no-backup          não roda o backup.sh antes de trocar a versão
#   --skip-pull          não baixa a imagem: sobe a que já está no servidor (sem acesso ao ghcr.io)
# ============================================================
set -euo pipefail

DOMAIN="monburan.com.br"
INSTALL_DIR="${INSTALL_DIR:-/opt/monburan}"
APP_PORT=8080
EMAIL="${CERTBOT_EMAIL:-}"
ADMIN_EMAIL=""
SKIP_TLS=0
NO_BACKUP=0
SKIP_PULL=0
TLS_PENDING=0

usage() {
    sed -n '2,/^# =====/p' "$0" | sed 's/^# \{0,1\}//' | sed '$d'
}

log() { printf '\n==> %s\n' "$*"; }
warn() { printf '    ATENÇÃO: %s\n' "$*" >&2; }
fail() { printf 'ERRO: %s\n' "$*" >&2; exit 1; }

require_tools() {
    local tool
    for tool in docker nginx certbot openssl curl; do
        command -v "$tool" >/dev/null 2>&1 || fail "'$tool' não está instalado: rode provision.sh --apply antes"
    done
    docker compose version >/dev/null 2>&1 || fail "o plugin 'docker compose' não está instalado: rode provision.sh --apply antes"
}

# Troca (ou acrescenta) CHAVE=valor no arquivo, sem passar o valor pela linha de comando nem por sed
set_env_value() {
    local file=$1 key=$2 value=$3
    (
        umask 077
        VALUE="$value" awk -v key="$key" 'BEGIN { FS = "=" }
            $1 == key { print key "=" ENVIRON["VALUE"]; found = 1; next }
            { print }
            END { if (!found) print key "=" ENVIRON["VALUE"] }' "$file" > "$file.tmp"
    )
    mv "$file.tmp" "$file"
}

ensure_env() {
    local env_file="$INSTALL_DIR/.env"
    if [[ ! -f "$env_file" ]]; then
        [[ -f "$INSTALL_DIR/env.example" ]] || fail "falta $INSTALL_DIR/env.example (copie infra/vps para $INSTALL_DIR)"
        install -m 600 "$INSTALL_DIR/env.example" "$env_file"
        set_env_value "$env_file" DB_PASSWORD "$(openssl rand -hex 24)"
        set_env_value "$env_file" JWT_SIGNING_KEY "$(openssl rand -base64 48 | tr -d '\n')"
        set_env_value "$env_file" ALLOWED_HOSTS "$DOMAIN;www.$DOMAIN;127.0.0.1"
        set_env_value "$env_file" APP_PUBLIC_BASE_URL "https://$DOMAIN"
        chmod 600 "$env_file"
        log ".env criado com a senha do banco e a chave JWT novas (não são exibidas)"
        warn "preencha SMTP_PASSWORD no .env (sem ele o app não envia e-mail) e reinicie o app: docker compose up -d --force-recreate app"
    fi

    if [[ -n "$ADMIN_EMAIL" ]]; then
        set_env_value "$env_file" ADMIN_EMAILS "$ADMIN_EMAIL"
        echo "    ADMIN_EMAILS definido no .env"
    fi

    if grep -q 'TROQUE' "$env_file"; then
        fail "$env_file ainda tem valores de exemplo (TROQUE_...): troque ou apague o arquivo para eu gerar novos"
    fi
    chmod 600 "$env_file"
}

ensure_compose() {
    cd "$INSTALL_DIR"
    [[ -f docker-compose.prod.yml ]] || fail "falta docker-compose.prod.yml em $INSTALL_DIR"
    if [[ ! -f docker-compose.yml ]]; then
        cp docker-compose.prod.yml docker-compose.yml
        echo "    docker-compose.yml criado a partir de docker-compose.prod.yml"
    elif ! cmp -s docker-compose.prod.yml docker-compose.yml; then
        cp -p docker-compose.yml "docker-compose.yml.bak-$(date +%Y%m%d-%H%M%S)"
        cp docker-compose.prod.yml docker-compose.yml
        echo "    docker-compose.yml atualizado a partir de docker-compose.prod.yml (cópia anterior guardada)"
    fi
}

# Rede de segurança antes de trocar a versão: backup (se configurado) e a imagem atual guardada com uma tag de rollback
safety_net() {
    if ! docker inspect monburan-app >/dev/null 2>&1; then
        return
    fi

    local image image_id stamp
    image=$(docker inspect -f '{{.Config.Image}}' monburan-app)
    image_id=$(docker inspect -f '{{.Image}}' monburan-app)
    stamp=$(date +%Y%m%d-%H%M%S)
    docker tag "$image_id" "${image%%:*}:rollback-${stamp}"
    echo "    imagem atual guardada como ${image%%:*}:rollback-${stamp}"

    if [[ $NO_BACKUP -eq 0 && -f "$INSTALL_DIR/backup.sh" && -f "$INSTALL_DIR/backup.env" ]]; then
        echo "    backup antes de trocar a versão"
        BACKUP_CONFIG="$INSTALL_DIR/backup.env" bash "$INSTALL_DIR/backup.sh" || fail "o backup falhou: não vou trocar a versão sem backup (use --no-backup para pular)"
    fi
}

# deploy.sh e backup.sh (pelo timer) rodam como root: quem puder editar esses arquivos vira root. Arquivos copiados com tar,
# scp ou rsync -a chegam com o dono da máquina de origem (o deploy automático gravava o uid 1001 do runner)
fix_ownership() {
    local foreign
    foreign=$(find "$INSTALL_DIR" -xdev \( ! -user 0 -o ! -group 0 -o -perm /022 \) -print 2>/dev/null | head -1)
    [[ -n "$foreign" ]] || return 0
    chown -R root:root "$INSTALL_DIR"
    chmod -R go-w "$INSTALL_DIR"
    warn "havia arquivos em $INSTALL_DIR com outro dono ou com escrita para grupo/outros (ex.: $foreign): agora são só do root"
}

# O compose só recria o contêiner quando a imagem muda: confere que o app ficou na imagem que acabou de ser baixada
check_app_image() {
    local reference running expected
    reference=$(docker inspect -f '{{.Config.Image}}' monburan-app 2>/dev/null) || fail "o contêiner monburan-app não existe depois do docker compose up"
    running=$(docker inspect -f '{{.Image}}' monburan-app)
    expected=$(docker image inspect -f '{{.Id}}' "$reference")
    if [[ "$running" != "$expected" ]]; then
        fail "o app continua na imagem anterior ($running), não na baixada ($expected): rode docker compose up -d --force-recreate app"
    fi
    echo "    o app está na imagem ${expected#sha256:}"
}

wait_for_health() {
    local code _
    for _ in $(seq 1 60); do
        code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:$APP_PORT/health" || true)
        if [[ "$code" == "200" ]]; then
            echo "    o app respondeu /health"
            return 0
        fi
        sleep 3
    done
    docker logs --tail 30 monburan-app 2>&1 || true
    fail "o app não respondeu /health em 3 minutos (confira: docker logs monburan-app)"
}

start_stack() {
    log "Stack (app + Postgres)"
    safety_net
    if [[ $SKIP_PULL -eq 1 ]]; then
        warn "--skip-pull: sobe a imagem que já está no servidor, sem baixar a nova"
    elif ! docker compose pull app; then
        # Antes só avisava e seguia com a imagem antiga: o deploy terminava "concluído" sem trocar a versão
        fail "não consegui baixar a imagem nova e nada foi trocado (confira o acesso ao ghcr.io; para subir a imagem que já está no servidor, use --skip-pull)"
    fi
    docker compose up -d --remove-orphans
    wait_for_health
    check_app_image
}

# ---------------------------------------------------------------------------------------------------------------------
# nginx e TLS
# ---------------------------------------------------------------------------------------------------------------------
reload_nginx() {
    nginx -t || return 1
    if systemctl is-active --quiet nginx; then
        systemctl reload nginx
    else
        systemctl restart nginx
    fi
}

# O site do domínio pode já existir com outro nome (instalação feita à mão): atualiza esse arquivo em vez de criar um segundo
nginx_site_file() {
    local existing=""
    if compgen -G "/etc/nginx/sites-enabled/*" >/dev/null; then
        existing=$(grep -lE "server_name[^;]*[[:space:]]${DOMAIN//./\\.}" /etc/nginx/sites-enabled/* 2>/dev/null | head -1 || true)
    fi
    if [[ -n "$existing" ]]; then
        readlink -f "$existing"
    else
        echo "/etc/nginx/sites-available/$DOMAIN"
    fi
}

# Troca a configuração do site e recarrega; se o nginx recusar, devolve a anterior
install_site_conf() {
    local source=$1 target=$2 saved=""
    if [[ -f "$target" ]]; then
        saved=$(mktemp)
        cp -p "$target" "$saved"
    fi
    install -m 644 "$source" "$target"
    if reload_nginx; then
        [[ -z "$saved" ]] || rm -f "$saved"
        return 0
    fi

    warn "o nginx recusou a configuração nova: voltando a anterior"
    if [[ -n "$saved" ]]; then
        cp -p "$saved" "$target"
        rm -f "$saved"
        reload_nginx || true
    else
        rm -f "$target"
    fi
    return 1
}

# Site só em HTTP: serve o desafio do certbot e já repassa para o app, para testar antes de existir certificado
http_only_conf() {
    cat <<CONF
# Provisório: só HTTP, até o certificado existir (deploy.sh troca por infra/vps/nginx.conf)
server {
    listen 80 default_server;
    listen [::]:80 default_server;
    server_name $DOMAIN www.$DOMAIN;

    location /.well-known/acme-challenge/ {
        root /var/www/certbot;
    }

    location / {
        proxy_pass http://127.0.0.1:$APP_PORT;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$remote_addr;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_buffering off;
    }
}
CONF
}

certificate_ready() {
    [[ -s "/etc/letsencrypt/live/$DOMAIN/fullchain.pem" && -f /etc/letsencrypt/options-ssl-nginx.conf && -f /etc/letsencrypt/ssl-dhparams.pem ]]
}

configure_nginx() {
    log "nginx"
    local site tmp
    site=$(nginx_site_file)
    mkdir -p /var/www/certbot
    rm -f /etc/nginx/sites-enabled/default

    # Guarda uma cópia do que existia, uma vez, se for diferente do que vai entrar
    if [[ -f "$site" ]] && ! cmp -s "$site" "$INSTALL_DIR/nginx.conf"; then
        cp -p "$site" "$site.bak-$(date +%Y%m%d-%H%M%S)"
    fi
    ln -sf "$site" "/etc/nginx/sites-enabled/$(basename "$site")"

    if ! certificate_ready; then
        tmp=$(mktemp)
        http_only_conf > "$tmp"
        install_site_conf "$tmp" "$site" || { rm -f "$tmp"; fail "o nginx não aceitou nem o site provisório em HTTP"; }
        rm -f "$tmp"

        if [[ $SKIP_TLS -eq 1 ]]; then
            warn "sem certificado (--skip-tls): o site fica em HTTP até você rodar o deploy sem essa opção"
            TLS_PENDING=1
            return 0
        fi

        local email_args=(--register-unsafely-without-email)
        if [[ -n "$EMAIL" ]]; then
            email_args=(-m "$EMAIL")
        else
            warn "sem --email: o Let's Encrypt não vai avisar quando o certificado estiver para vencer"
        fi

        log "Certificado TLS (certbot)"
        # O plugin do nginx acrescenta o 443 ao site e cria options-ssl-nginx.conf e ssl-dhparams.pem, que o nginx.conf usa
        if ! certbot --nginx -d "$DOMAIN" -d "www.$DOMAIN" --non-interactive --agree-tos "${email_args[@]}"; then
            warn "o certbot não conseguiu o certificado. O DNS de $DOMAIN e de www.$DOMAIN aponta para este servidor?"
            warn "o site segue em HTTP; resolva o DNS e rode o deploy de novo"
            TLS_PENDING=1
            return 0
        fi
        if ! certificate_ready; then
            warn "o certbot terminou, mas faltam arquivos em /etc/letsencrypt; o site segue em HTTP"
            TLS_PENDING=1
            return 0
        fi
    fi

    install_site_conf "$INSTALL_DIR/nginx.conf" "$site" || fail "o nginx não aceitou o nginx.conf (a configuração anterior foi mantida)"
    echo "    nginx.conf instalado em $site"
}

main() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --domain) DOMAIN="${2:?informe o domínio}"; shift 2 ;;
            --email) EMAIL="${2:?informe o e-mail}"; shift 2 ;;
            --admin-email) ADMIN_EMAIL="${2:?informe o e-mail do administrador}"; shift 2 ;;
            --skip-tls) SKIP_TLS=1; shift ;;
            --no-backup) NO_BACKUP=1; shift ;;
            --skip-pull) SKIP_PULL=1; shift ;;
            -h|--help) usage; exit 0 ;;
            *) echo "Opção desconhecida: $1" >&2; usage; exit 2 ;;
        esac
    done

    if [[ ${EUID:-$(id -u)} -ne 0 ]]; then
        fail "rode como root (sudo bash deploy.sh)"
    fi

    require_tools
    [[ -d "$INSTALL_DIR" ]] || fail "falta a pasta $INSTALL_DIR (copie infra/vps para lá)"
    [[ -f "$INSTALL_DIR/nginx.conf" ]] || fail "falta $INSTALL_DIR/nginx.conf"
    fix_ownership

    log ".env e compose"
    ensure_env
    ensure_compose
    start_stack
    configure_nginx

    echo
    echo "==> Deploy concluído."
    echo "    App:      http://127.0.0.1:$APP_PORT (interno)"
    if [[ $TLS_PENDING -eq 0 ]]; then
        echo "    Público:  https://$DOMAIN"
    else
        echo "    Público:  http://$DOMAIN (sem TLS ainda)"
    fi
    echo "    Logs:     docker logs -f monburan-app"
    echo "    Conferir: bash $INSTALL_DIR/validate.sh"
    [[ $TLS_PENDING -eq 0 ]] || exit 1
}

# Permite carregar as funções nos testes (DEPLOY_SOURCE_ONLY=1) sem executar nada
if [[ "${DEPLOY_SOURCE_ONLY:-0}" != "1" ]]; then
    main "$@"
fi
