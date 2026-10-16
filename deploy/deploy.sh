#!/usr/bin/env bash
# Deploy da app Monburan no VPS.
# Uso (como root, dentro do diretorio onde o pacote foi extraido):
#   chmod +x deploy.sh && ./deploy.sh
# Variaveis opcionais:
#   PG_CONN         - connection string completa do Postgres
#   PGPASSWORD      - senha do postgres (default: postgres)
#   PGUSER          - usuario do postgres (default: postgres)
#   APP_PORT        - porta HTTP da app (default: 80)
#   APP_PUBLIC_BASE_URL - URL pública p/ links de e-mail (ex.: https://monburan.com)
#   JWT_SIGNING_KEY - chave de assinatura JWT; se ausente, reusa a de
#                   /opt/monburan/.jwt_signing_key ou gera uma nova
set -euo pipefail

APP_DIR=/opt/monburan
APP_PORT="${APP_PORT:-80}"
PG_CONN="${PG_CONN:-Host=127.0.0.1;Port=5432;Database=monburan;Username=${PGUSER:-postgres};Password=${PGPASSWORD:-postgres}}"

echo "==> Instalando app em $APP_DIR"
mkdir -p "$APP_DIR"
cp -r app/. "$APP_DIR/"
chmod +x "$APP_DIR/Monburan.Web"

echo "==> Garantindo database 'monburan' no Postgres"
if docker ps >/dev/null 2>&1; then
  PG_CTR="$(docker ps --format '{{.Names}} {{.Image}}' | grep -iE 'postgres|pgvector' | awk '{print $1}' | head -1 || true)"
fi
if [ -n "${PG_CTR:-}" ]; then
  echo "    via container docker: $PG_CTR"
  docker exec -i "$PG_CTR" psql -U "${PGUSER:-postgres}" -tc \
    "SELECT 1 FROM pg_database WHERE datname='monburan'" | grep -q 1 || \
  docker exec -i "$PG_CTR" psql -U "${PGUSER:-postgres}" -c "CREATE DATABASE monburan"
elif command -v psql >/dev/null 2>&1; then
  echo "    via psql local"
  PGPASSWORD="${PGPASSWORD:-postgres}" psql -h 127.0.0.1 -U "${PGUSER:-postgres}" -tc \
    "SELECT 1 FROM pg_database WHERE datname='monburan'" | grep -q 1 || \
  PGPASSWORD="${PGPASSWORD:-postgres}" psql -h 127.0.0.1 -U "${PGUSER:-postgres}" -c "CREATE DATABASE monburan"
else
  echo "    AVISO: nenhum psql/docker encontrado - crie o database 'monburan' manualmente"
fi

echo "==> Resolvendo chave de assinatura JWT"
KEY_FILE="$APP_DIR/.jwt_signing_key"
if [ -z "${JWT_SIGNING_KEY:-}" ]; then
  if [ -f "$KEY_FILE" ]; then
    JWT_SIGNING_KEY="$(cat "$KEY_FILE")"
    echo "    reusando chave persistida em $KEY_FILE"
  else
    JWT_SIGNING_KEY="$(openssl rand -base64 48 | tr -d '\n')"
    echo "    gerada nova chave (openssl rand -base64 48)"
  fi
fi
umask 077  # arquivos de segredo nascem 600 — sem janela world-readable
printf '%s' "$JWT_SIGNING_KEY" > "$KEY_FILE"
chmod 600 "$KEY_FILE"

echo "==> Escrevendo $APP_DIR/monburan.env (segredos fora da unit, chmod 600)"
cat > "$APP_DIR/monburan.env" <<EOF
ConnectionStrings__DefaultConnection=$PG_CONN
Jwt__SigningKey=$JWT_SIGNING_KEY
App__PublicBaseUrl=${APP_PUBLIC_BASE_URL:-}
EOF
chmod 600 "$APP_DIR/monburan.env"

echo "==> Instalando systemd unit (porta $APP_PORT)"
sed -e "s|__APP_PORT__|$APP_PORT|" monburan.service \
  > /etc/systemd/system/monburan.service
systemctl daemon-reload
systemctl enable monburan
systemctl restart monburan

sleep 4
systemctl --no-pager --full status monburan | head -12 || true
echo "==> Health check:"
curl -s -o /dev/null -w "  http://127.0.0.1:$APP_PORT/health -> %{http_code}\n" \
  "http://127.0.0.1:$APP_PORT/health" || true
echo "==> Pronto. Logs: journalctl -u monburan -f"
