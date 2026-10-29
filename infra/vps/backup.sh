#!/usr/bin/env bash
# ============================================================
# Monburan — Backup criptografado e enviado para fora da VPS
#
# O que vai: o banco PostgreSQL (pg_dump de um contêiner descartável apontando para o banco na nuvem, BACKUP_DB_URL) e a pasta de
# uploads do app. Tudo é criptografado com age ANTES de tocar o disco (o dump nunca existe em texto claro na VPS) e enviado ao
# armazenamento remoto com rclone. A chave privada do age fica FORA da VPS: quem invadir o servidor, ou o armazenamento remoto,
# só encontra arquivos que não consegue abrir. Os snapshots do próprio provedor do banco são uma camada a mais: este backup
# continua restaurável por você, em qualquer PostgreSQL, sem depender do provedor.
# Sem BACKUP_REMOTE o backup fica só no disco da VPS (protege de erro e de dado apagado, não da perda do servidor) e o script avisa.
#
# Configuração: /opt/monburan/backup.env (modelo em backup.env.example; permissão 600). Uso:
#   bash backup.sh            # um backup agora
#   systemctl enable --now monburan-backup.timer    # todo dia (veja infra/vps/systemd)
#
# Para conferir que o backup serve de verdade, restaure-o em um banco descartável: restore-test.sh.
# ============================================================
set -euo pipefail

CONFIG="${BACKUP_CONFIG:-/opt/monburan/backup.env}"
if [[ -f "$CONFIG" ]]; then
    # O arquivo é executado como shell: só aceita se for do dono e ninguém mais puder escrever nele
    if [[ "$(stat -c %u "$CONFIG")" != "$(id -u)" ]] || (( 8#$(stat -c %a "$CONFIG") & 8#022 )); then
        echo "ERRO: $CONFIG precisa ser do usuário que roda o backup e sem escrita para grupo/outros (chmod 600)" >&2
        exit 1
    fi
    # -a: as variáveis do arquivo (inclusive as RCLONE_CONFIG_*) chegam ao rclone
    set -a
    # shellcheck disable=SC1090
    source "$CONFIG"
    set +a
fi

: "${BACKUP_AGE_RECIPIENT:?Defina BACKUP_AGE_RECIPIENT (chave PÚBLICA age, começa com age1) em $CONFIG}"
: "${BACKUP_DB_URL:?Defina BACKUP_DB_URL (URI do banco na nuvem: postgresql://usuario:senha@host/banco?sslmode=require) em $CONFIG}"
BACKUP_REMOTE="${BACKUP_REMOTE:-}"
BACKUP_DIR="${BACKUP_DIR:-/opt/monburan/backups}"
KEEP_LOCAL_DAYS="${KEEP_LOCAL_DAYS:-7}"
KEEP_REMOTE_DAYS="${KEEP_REMOTE_DAYS:-30}"
# O pg_dump não lê servidores mais novos que ele: a imagem fica configurável para acompanhar a versão do provedor
BACKUP_PG_IMAGE="${BACKUP_PG_IMAGE:-postgres:18-alpine}"
# Rede Docker para o contêiner do pg_dump (só testes; em produção fica vazio e o banco é alcançado pela internet)
BACKUP_DOCKER_NETWORK="${BACKUP_DOCKER_NETWORK:-}"
APP_CONTAINER="${APP_CONTAINER:-monburan-app}"
BACKUP_UPLOADS="${BACKUP_UPLOADS:-true}"
HEALTHCHECK_URL="${HEALTHCHECK_URL:-}"

log() { printf '%s %s\n' "$(date -u +%FT%TZ)" "$*"; }

ping_healthcheck() {
    local suffix=${1:-}
    [[ -n "$HEALTHCHECK_URL" ]] || return 0
    command -v curl >/dev/null 2>&1 || { log "aviso: curl não está instalado; o monitor não foi avisado"; return 0; }
    curl -fsS -m 10 --retry 3 -o /dev/null "${HEALTHCHECK_URL}${suffix}" || log "aviso: não consegui avisar o monitor (${suffix:-ok})"
}

on_error() {
    local code=$?
    trap - ERR
    # Arquivo pela metade nunca fica no disco nem vai para o remoto
    rm -f "$BACKUP_DIR"/*.partial 2>/dev/null || true
    log "ERRO: o backup falhou (código ${code}). Nada foi apagado do armazenamento remoto."
    ping_healthcheck "/fail"
    exit "$code"
}
trap on_error ERR

require() {
    command -v "$1" >/dev/null 2>&1 || { log "ERRO: '$1' não está instalado (veja docs/SEGURANCA.md)"; exit 1; }
}

require docker
require age
if [[ -n "$BACKUP_REMOTE" ]]; then
    require rclone
fi

[[ "$BACKUP_AGE_RECIPIENT" == age1* ]] || { log "ERRO: BACKUP_AGE_RECIPIENT precisa ser uma chave pública age (age1...)"; exit 1; }
[[ "$BACKUP_DB_URL" == postgresql://* || "$BACKUP_DB_URL" == postgres://* ]] \
    || { log "ERRO: BACKUP_DB_URL precisa ser uma URI postgres:// ou postgresql://"; exit 1; }

umask 077
mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
database_file="$BACKUP_DIR/monburan-db-${stamp}.dump.age"
uploads_file="$BACKUP_DIR/monburan-uploads-${stamp}.tar.gz.age"
evidence_file="$BACKUP_DIR/monburan-evidencias-${stamp}.tar.gz.age"
min_size=1024

log "Banco: pg_dump do banco na nuvem, criptografado na saída"
# pg_dump -Fc é compactado e permite restaurar só uma parte; o texto claro segue direto para o age. Escreve em .partial e só
# vira backup depois de terminar (um pg_dump que falha no meio não deixa arquivo com cara de bom)
network_args=()
[[ -n "$BACKUP_DOCKER_NETWORK" ]] && network_args=(--network "$BACKUP_DOCKER_NETWORK")
docker run --rm "${network_args[@]}" "$BACKUP_PG_IMAGE" pg_dump "$BACKUP_DB_URL" --format=custom --no-owner \
    | age --recipient "$BACKUP_AGE_RECIPIENT" --output "${database_file}.partial"
[[ $(stat -c %s "${database_file}.partial") -gt $min_size ]] || { log "ERRO: o dump do banco saiu pequeno demais"; exit 1; }
mv "${database_file}.partial" "$database_file"
log "Banco: $(basename "$database_file") ($(stat -c %s "$database_file") bytes)"

files=("$database_file")
if [[ "$BACKUP_UPLOADS" == "true" ]]; then
    if docker inspect -f '{{.State.Running}}' "$APP_CONTAINER" 2>/dev/null | grep -q true; then
        log "Uploads: pasta de $APP_CONTAINER, criptografada na saída"
        docker exec "$APP_CONTAINER" tar -czf - -C /app/wwwroot uploads \
            | age --recipient "$BACKUP_AGE_RECIPIENT" --output "${uploads_file}.partial"
        mv "${uploads_file}.partial" "$uploads_file"
        files+=("$uploads_file")
        log "Uploads: $(basename "$uploads_file") ($(stat -c %s "$uploads_file") bytes)"

        # Imagens das sugestões de vaga: pasta privada, fora do wwwroot, em arquivo próprio
        if docker exec "$APP_CONTAINER" test -d /app/App_Data/evidencias; then
            log "Evidências: pasta de $APP_CONTAINER, criptografada na saída"
            docker exec "$APP_CONTAINER" tar -czf - -C /app/App_Data evidencias \
                | age --recipient "$BACKUP_AGE_RECIPIENT" --output "${evidence_file}.partial"
            mv "${evidence_file}.partial" "$evidence_file"
            files+=("$evidence_file")
            log "Evidências: $(basename "$evidence_file") ($(stat -c %s "$evidence_file") bytes)"
        else
            log "aviso: $APP_CONTAINER não tem a pasta de evidências; ficou de fora deste backup"
        fi
    else
        log "aviso: $APP_CONTAINER não está rodando; uploads e evidências ficaram de fora deste backup"
    fi
fi

if [[ -n "$BACKUP_REMOTE" ]]; then
    log "Enviando para $BACKUP_REMOTE"
    for file in "${files[@]}"; do
        rclone copy "$file" "$BACKUP_REMOTE" --checksum --retries 3
    done
else
    log "AVISO: BACKUP_REMOTE não está configurado: o backup ficou só no disco da VPS (não protege contra a perda do servidor)"
fi

# Só apaga o antigo depois de um envio bem-sucedido
log "Retenção local: ${KEEP_LOCAL_DAYS} dias"
find "$BACKUP_DIR" -maxdepth 1 -type f -name 'monburan-*.age' -mtime +"$KEEP_LOCAL_DAYS" -print -delete
if [[ -n "$BACKUP_REMOTE" ]]; then
    if [[ "$KEEP_REMOTE_DAYS" -gt 0 ]]; then
        log "Retenção remota: ${KEEP_REMOTE_DAYS} dias"
        # A falha aqui não invalida o backup que acabou de subir, mas fica registrada
        if ! rclone delete "$BACKUP_REMOTE" --include 'monburan-*.age' --min-age "${KEEP_REMOTE_DAYS}d" -v 2>&1 | sed 's/^/    /'; then
            log "aviso: não consegui limpar os backups antigos do remoto"
        fi
    else
        log "Retenção remota: desligada (KEEP_REMOTE_DAYS=0); a limpeza fica por conta do armazenamento"
    fi
fi

log "Backup concluído"
ping_healthcheck
