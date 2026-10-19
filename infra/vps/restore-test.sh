#!/usr/bin/env bash
# ============================================================
# Monburan — Teste de restauração do backup
#
# Restaura o backup do banco em um PostgreSQL DESCARTÁVEL (contêiner novo, sem portas publicadas, dados em memória) e confere
# que os dados estão lá. Não toca no banco de produção. Um backup que nunca foi restaurado é só uma esperança: rode isto
# todo mês e depois de qualquer mudança no backup. Se o backup dos uploads (monburan-uploads-*.tar.gz.age) do mesmo dia
# estiver ao lado do arquivo, ou no remoto, o script também confere que ele abre.
#
# Precisa da chave PRIVADA do age (a que fica fora da VPS): copie-a para um lugar temporário, rode e apague.
#
# Uso:
#   bash restore-test.sh /caminho/monburan-db-AAAAMMDDTHHMMSSZ.dump.age --identity /caminho/chave-privada.txt
#   bash restore-test.sh --latest-remote --identity /caminho/chave-privada.txt     # o mais recente do BACKUP_REMOTE
#
# Variáveis opcionais: POSTGRES_IMAGE (padrão: a imagem do contêiner de produção, ou postgres:16-alpine).
# ============================================================
set -euo pipefail

CONFIG="${BACKUP_CONFIG:-/opt/monburan/backup.env}"
if [[ -f "$CONFIG" ]]; then
    # O arquivo é executado como shell: só aceita se for do dono e ninguém mais puder escrever nele
    if [[ "$(stat -c %u "$CONFIG")" != "$(id -u)" ]] || (( 8#$(stat -c %a "$CONFIG") & 8#022 )); then
        echo "ERRO: $CONFIG precisa ser do usuário que roda o teste e sem escrita para grupo/outros (chmod 600)" >&2
        exit 1
    fi
    set -a
    # shellcheck disable=SC1090
    source "$CONFIG"
    set +a
fi

SOURCE=""
IDENTITY=""
TEST_CONTAINER="monburan-restore-test"
POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-monburan-postgres}"

log() { printf '%s %s\n' "$(date -u +%FT%TZ)" "$*"; }
fail() { log "ERRO: $*"; exit 1; }

while [[ $# -gt 0 ]]; do
    case "$1" in
        --identity) IDENTITY="${2:?informe o caminho da chave privada}"; shift 2 ;;
        --latest-remote) SOURCE="--latest-remote"; shift ;;
        -h|--help) sed -n '2,/^# =====/p' "$0" | sed 's/^# \{0,1\}//' | sed '$d'; exit 0 ;;
        *) SOURCE="$1"; shift ;;
    esac
done

[[ -n "$SOURCE" ]] || fail "informe o arquivo de backup ou --latest-remote (veja --help)"
[[ -n "$IDENTITY" && -f "$IDENTITY" ]] || fail "informe --identity com o caminho da chave privada do age"
command -v age >/dev/null || fail "age não está instalado"
command -v docker >/dev/null || fail "docker não está instalado"

# Mesma versão do PostgreSQL de produção: o pg_restore não lê arquivo feito por um pg_dump mais novo que ele
POSTGRES_IMAGE="${POSTGRES_IMAGE:-$(docker inspect -f '{{.Config.Image}}' "$POSTGRES_CONTAINER" 2>/dev/null || true)}"
POSTGRES_IMAGE="${POSTGRES_IMAGE:-postgres:16-alpine}"

umask 077
workdir="$(mktemp -d)"
cleanup() {
    docker rm -f "$TEST_CONTAINER" >/dev/null 2>&1 || true
    rm -rf "$workdir"
}
trap cleanup EXIT

# monburan-db-<data>.dump.age  ->  monburan-uploads-<data>.tar.gz.age
uploads_name_for() {
    local name
    name="$(basename "$1")"
    name="${name/monburan-db-/monburan-uploads-}"
    echo "${name/.dump.age/.tar.gz.age}"
}

has_uploads=0
if [[ "$SOURCE" == "--latest-remote" ]]; then
    command -v rclone >/dev/null || fail "rclone não está instalado"
    [[ -n "${BACKUP_REMOTE:-}" ]] || fail "defina BACKUP_REMOTE em $CONFIG"
    remote_files="$(rclone lsf --files-only "$BACKUP_REMOTE")"
    latest="$(grep -E '^monburan-db-.*\.dump\.age$' <<<"$remote_files" | sort | tail -1 || true)"
    [[ -n "$latest" ]] || fail "nenhum backup do banco encontrado em $BACKUP_REMOTE"
    log "Baixando $latest"
    rclone copyto "$BACKUP_REMOTE/$latest" "$workdir/backup.dump.age"
    uploads_name="$(uploads_name_for "$latest")"
    if grep -qx "$uploads_name" <<<"$remote_files"; then
        rclone copyto "$BACKUP_REMOTE/$uploads_name" "$workdir/uploads.tar.gz.age"
        has_uploads=1
    fi
else
    [[ -f "$SOURCE" ]] || fail "arquivo não encontrado: $SOURCE"
    cp "$SOURCE" "$workdir/backup.dump.age"
    uploads_path="$(dirname "$SOURCE")/$(uploads_name_for "$SOURCE")"
    if [[ -f "$uploads_path" ]]; then
        cp "$uploads_path" "$workdir/uploads.tar.gz.age"
        has_uploads=1
    fi
fi

log "Descriptografando"
age --decrypt --identity "$IDENTITY" --output "$workdir/backup.dump" "$workdir/backup.dump.age" \
    || fail "não consegui descriptografar: a chave privada não é a do backup, ou o arquivo está corrompido"

if [[ $has_uploads -eq 1 ]]; then
    uploaded_files="$(age --decrypt --identity "$IDENTITY" "$workdir/uploads.tar.gz.age" | tar -tz | grep -vc '/$' || true)"
    [[ "${uploaded_files:-0}" -gt 0 ]] || fail "o backup dos uploads não abre ou está vazio"
    log "Uploads: o arquivo abre e tem ${uploaded_files} arquivo(s)"
else
    log "Uploads: nenhum backup de uploads do mesmo dia encontrado (conferência pulada)"
fi

log "Subindo um PostgreSQL descartável ($POSTGRES_IMAGE)"
docker rm -f "$TEST_CONTAINER" >/dev/null 2>&1 || true
docker run -d --name "$TEST_CONTAINER" \
    -e POSTGRES_PASSWORD=restore-test -e POSTGRES_DB=restore \
    --tmpfs /var/lib/postgresql/data \
    "$POSTGRES_IMAGE" >/dev/null

ready=0
for _ in $(seq 1 60); do
    if docker exec "$TEST_CONTAINER" pg_isready -U postgres -d restore >/dev/null 2>&1; then
        # a imagem sobe o servidor uma vez para inicializar e reinicia: confirma de novo
        sleep 2
        docker exec "$TEST_CONTAINER" pg_isready -U postgres -d restore >/dev/null 2>&1 && ready=1 && break
    fi
    sleep 1
done
[[ $ready -eq 1 ]] || fail "o PostgreSQL de teste não ficou pronto"

log "Restaurando"
if ! docker exec -i "$TEST_CONTAINER" pg_restore -U postgres -d restore --no-owner --exit-on-error < "$workdir/backup.dump"; then
    fail "o pg_restore falhou: este backup NÃO serve"
fi

count() {
    docker exec "$TEST_CONTAINER" psql -U postgres -d restore -tAc "SELECT count(*) FROM $1" 2>/dev/null || echo "-1"
}

users="$(count '"Identity"."Users"')"
jobs="$(count '"Jobs"')"
advertisers="$(count '"Advertisers"')"
candidates="$(count '"Candidates"')"
applied="$(count '"__EFMigrationsHistory"')"

log "Contas: $users | Vagas: $jobs | Empresas: $advertisers | Candidatos: $candidates | Migrations aplicadas: $applied"
[[ "$users" =~ ^[0-9]+$ && "$users" -gt 0 ]] || fail "o banco restaurado não tem contas: confira o backup"
[[ "$applied" =~ ^[0-9]+$ && "$applied" -gt 0 ]] || fail "o banco restaurado não tem o histórico de migrations"

log "Restauração OK. O contêiner de teste e o arquivo descriptografado foram apagados."
