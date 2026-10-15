#!/usr/bin/env bash
# Deploy da app Monburan no VPS.
# Uso (como root, dentro do diretorio onde o pacote foi extraido):
#   chmod +x deploy.sh && ./deploy.sh
# Variaveis opcionais:
#   PG_CONN     - connection string completa do Postgres
#   PGPASSWORD  - senha do postgres (default: postgres)
#   PGUSER      - usuario do postgres (default: postgres)
#   APP_PORT    - porta HTTP da app (default: 80)
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

echo "==> Instalando systemd unit (porta $APP_PORT)"
sed -e "s|__PG_CONN__|$PG_CONN|" -e "s|http://+:80|http://+:$APP_PORT|" monburan.service \
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
