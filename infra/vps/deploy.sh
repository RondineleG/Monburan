#!/usr/bin/env bash
# ============================================================
# Monburan — Deploy na VPS
# Instala dependências, publica imagem do GHCR, sobe stack Docker
# e configura nginx + TLS (certbot) para monburan.com.br.
#
# Uso (como root, na VPS):
#   cd /opt/monburan
#   bash deploy.sh
# ============================================================
set -euo pipefail

DOMAIN="monburan.com.br"
APP_PORT=8080
INSTALL_DIR="/opt/monburan"

echo "==> [1/7] Dependências do sistema"
apt-get update -y
apt-get install -y nginx certbot python3-certbot-nginx curl

echo "==> [2/7] Parando app antigo (Recruiva) para liberar porta 80"
systemctl stop recruiva 2>/dev/null && echo "    recruiva parado" || echo "    recruiva não estava ativo"
systemctl disable recruiva 2>/dev/null || true

echo "==> [3/7] Preparando diretório e .env"
mkdir -p "$INSTALL_DIR"
if [[ ! -f "$INSTALL_DIR/.env" ]]; then
    cp "$INSTALL_DIR/env.example" "$INSTALL_DIR/.env"
    chmod 600 "$INSTALL_DIR/.env"
    echo "    ATENÇÃO: edite $INSTALL_DIR/.env e preencha DB_PASSWORD e JWT_SIGNING_KEY."
fi

echo "==> [4/7] Logando no GHCR (usa o token do gh na VPS)"
if command -v gh >/dev/null 2>&1; then
    gh auth token | docker login ghcr.io -u "$(gh api user --jq .login)" --password-stdin
else
    echo "    gh não encontrado — o pull pode falhar se a imagem for privada."
fi

echo "==> [5/7] Subindo stack (app + postgres)"
cd "$INSTALL_DIR"
docker compose -f docker-compose.prod.yml pull app || echo "    pull falhou — usando imagem local se existir"
docker compose -f docker-compose.prod.yml up -d --remove-orphans

echo "==> [6/7] Configurando nginx"
cp "$INSTALL_DIR/nginx.conf" "/etc/nginx/sites-available/$DOMAIN"
ln -sf "/etc/nginx/sites-available/$DOMAIN" "/etc/nginx/sites-enabled/$DOMAIN"
# Remove o site default para evitar conflito de server_name
rm -f /etc/nginx/sites-enabled/default
mkdir -p /var/www/certbot
nginx -t && systemctl reload nginx

echo "==> [7/7] Certificado TLS (certbot)"
# Só é possível quando o DNS de $DOMAIN aponta para este servidor.
if curl -fsS "http://$DOMAIN/.well-known/acme-challenge/healthcheck" >/dev/null 2>&1 || \
   dig +short "$DOMAIN" A | grep -q "$(curl -s ifconfig.me)"; then
    certbot --nginx -d "$DOMAIN" -d "www.$DOMAIN" --non-interactive --agree-tos --redirect
    systemctl reload nginx
else
    echo "    DNS de $DOMAIN ainda não aponta para este servidor — TLS adiado."
    echo "    Depois que o DNS propagar, rode:"
    echo "      certbot --nginx -d $DOMAIN -d www.$DOMAIN --non-interactive --agree-tos --redirect"
fi

echo ""
echo "==> Deploy concluído."
echo "    App:      http://127.0.0.1:$APP_PORT (interno)"
echo "    Público:  https://$DOMAIN (após TLS)"
echo "    Logs:     docker logs -f monburan-app"
