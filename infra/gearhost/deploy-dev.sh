#!/usr/bin/env bash
# ============================================================
# Monburan — deploy local na homologação (GearHost)
#
# Faz da sua máquina o mesmo que o workflow deploy-dev.yml: publica, gera o web.config, confere o limite de 100 MB, envia
# por FTP (uma conexão só, com curl) e espera o /health/live. Útil quando o GitHub não alcança o FTP do GearHost.
#
# Credenciais, nunca no Git:
#   - FTP: o .publishsettings baixado do painel do GearHost (padrão: app-dev-monburan.gearhost.io.publishsettings na raiz)
#   - banco, chave JWT e senha do seed: infra/gearhost/dev.env (modelo em dev.env.example)
#
# Uso (Git Bash, na raiz do repositório):
#   bash infra/gearhost/deploy-dev.sh [--dry-run] [arquivo.publishsettings]
#   --dry-run: publica, gera o web.config e confere o tamanho, sem tocar no FTP
# ============================================================
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
here="$root/infra/gearhost"
dry_run=0
if [[ "${1:-}" == "--dry-run" ]]; then dry_run=1; shift; fi
settings="${1:-$root/app-dev-monburan.gearhost.io.publishsettings}"
env_file="$here/dev.env"
max_mb=100

[[ -f "$settings" ]] || { echo "ERRO: não achei $settings (baixe no painel do GearHost)" >&2; exit 1; }
[[ -f "$env_file" ]] || { echo "ERRO: crie $env_file a partir de dev.env.example" >&2; exit 1; }
set -a
# shellcheck disable=SC1090
source "$env_file"
set +a

attr() { grep -o "$1=\"[^\"]*\"" "$settings" | head -1 | sed -e "s/^$1=\"//" -e 's/"$//' -e 's/&amp;/\&/g'; }
ftp_url="$(attr publishUrl)"          # ftp://servidor/wwwroot
ftp_user="$(attr userName)"
ftp_pass="$(attr userPWD)"
APP_URL="${APP_URL:-$(attr destinationAppUrl)}"
export APP_URL

out="$(mktemp -d)"
trap 'rm -rf "$out"' EXIT

if (( dry_run == 0 )); then
    echo "==> Conferindo o FTP ($ftp_url)"
    curl -sS --max-time 30 --list-only -u "$ftp_user:$ftp_pass" "$ftp_url/" >/dev/null \
        || { echo "ERRO: o FTP não responde. Confira no painel do GearHost se o site está ativo e se o servidor mudou." >&2; exit 1; }
fi

echo "==> Publicando"
dotnet publish "$root/src/Monburan.Web/Monburan.Web.csproj" -c Release -o "$out" --nologo -v q \
    --self-contained false -p:DebugType=none -p:SatelliteResourceLanguages="pt-BR%3Ben"
rm -rf "$out/BlazorDebugProxy"
bash "$here/make-webconfig.sh" "$out"

size=$(du -sm "$out" | cut -f1)
echo "==> Publicação: ${size} MB (limite ${max_mb} MB)"
(( size <= max_mb )) || { echo "ERRO: passa do limite" >&2; exit 1; }
if (( dry_run == 1 )); then
    echo "==> Simulação: $(find "$out" -type f | wc -l) arquivos seriam enviados para $ftp_url"
    exit 0
fi

# Uma conexão FTP para todos os arquivos: o curl lê os pares arquivo/destino de um arquivo de configuração. As senhas
# vão no próprio arquivo (fora da linha de comando, que outros processos enxergam).
config="$out.curl"
trap 'rm -rf "$out" "$config"' EXIT
{
    printf 'user = "%s:%s"\n' "$ftp_user" "$ftp_pass"
    echo 'ftp-create-dirs'
    echo 'ftp-pasv'
    echo 'silent'
    echo 'show-error'
    (cd "$out" && find . -type f ! -name app_offline.htm | sed 's|^\./||') | while read -r file; do
        printf 'upload-file = "%s"\nurl = "%s/%s"\n' "$out/$file" "$ftp_url" "$file"
    done
} > "$config"
chmod 600 "$config"

echo "==> Tirando o site do ar durante a troca (app_offline.htm)"
printf '<!doctype html><meta charset="utf-8"><title>Atualizando</title><p>O Monburan está sendo atualizado. Volte em um minuto.</p>' > "$out/app_offline.htm"
curl -sS -u "$ftp_user:$ftp_pass" -T "$out/app_offline.htm" "$ftp_url/app_offline.htm"
sleep 10

echo "==> Enviando $(grep -c '^upload-file' "$config") arquivos"
curl -K "$config"

echo "==> Voltando o site"
curl -sS -u "$ftp_user:$ftp_pass" -Q "DELE app_offline.htm" "$ftp_url/" -o /dev/null

echo "==> Esperando $APP_URL/health/live (a primeira subida aplica migrations e o seed)"
for i in $(seq 1 30); do
    code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 20 "$APP_URL/health/live" || true)
    echo "    tentativa $i: HTTP $code"
    [[ "$code" == "200" ]] && { echo "Pronto: $APP_URL"; exit 0; }
    sleep 10
done
echo "ERRO: o site não respondeu 200. HTTP 500.31 indica que falta o runtime ASP.NET Core 10 no servidor." >&2
exit 1
