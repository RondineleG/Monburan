#!/usr/bin/env bash
# ============================================================
# Monburan — Teste de fumaça do backup e da restauração
#
# Sobe um PostgreSQL de mentira com dados de mentira, roda o backup.sh de verdade (age + rclone, com uma pasta local no lugar do
# armazenamento remoto) e depois o restore-test.sh. Confere que:
#   - o backup sai criptografado (nada legível no arquivo) e chega ao "remoto";
#   - a restauração devolve os dados (acentos inclusive) com a chave certa e falha com a chave errada ou o arquivo corrompido;
#   - falha do backup (banco parado, pg_dump ausente) não deixa arquivo pela metade nem mexe no remoto;
#   - a retenção apaga só o que passou do prazo, e nada quando KEEP_REMOTE_DAYS=0.
# Roda dentro de um contêiner auxiliar (Alpine com age, rclone e a CLI do Docker) que fala com o Docker de quem executa.
#
#   bash infra/vps/tests/backup-restore-smoke.sh
# ============================================================
set -uo pipefail

if [[ "${INSIDE_CONTAINER:-0}" != "1" ]]; then
    repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
    exec docker run --rm -e INSIDE_CONTAINER=1 \
        -v /var/run/docker.sock:/var/run/docker.sock -v "$repo":/repo:ro \
        alpine:3.21 sh -c 'apk add --no-cache bash docker-cli age rclone coreutils findutils curl >/dev/null && bash /repo/infra/vps/tests/backup-restore-smoke.sh'
fi

BACKUP=/repo/infra/vps/backup.sh
RESTORE=/repo/infra/vps/restore-test.sh
WORK=$(mktemp -d)
OUT=$WORK/out.log
PG=monburan-smoke-pg
APP=monburan-smoke-app
NOPG=monburan-smoke-nopg
PG_IMAGE=postgres:16-alpine
passed=0
failed=0

cleanup() {
    docker rm -f "$PG" "$APP" "$NOPG" monburan-restore-test >/dev/null 2>&1
    rm -rf "$WORK"
}
trap cleanup EXIT

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

wait_for_pg() {
    local name=$1 ready=0 _
    for _ in $(seq 1 60); do
        if docker exec "$name" pg_isready -U monburan -d monburan >/dev/null 2>&1; then
            # a imagem sobe o servidor uma vez para inicializar e reinicia: confirma de novo
            sleep 2
            docker exec "$name" pg_isready -U monburan -d monburan >/dev/null 2>&1 && ready=1 && break
        fi
        sleep 1
    done
    [[ $ready -eq 1 ]]
}

write_config() {
    cat >"$WORK/backup.env" <<CONFIG
BACKUP_AGE_RECIPIENT=$RECIPIENT
BACKUP_REMOTE=$WORK/remote
BACKUP_DIR=$WORK/local
POSTGRES_CONTAINER=$PG
APP_CONTAINER=$APP
KEEP_LOCAL_DAYS=7
KEEP_REMOTE_DAYS=${1:-30}
CONFIG
    chmod 600 "$WORK/backup.env"
}

backup() {
    sleep 1 # o nome do arquivo tem a hora em segundos
    BACKUP_CONFIG="$WORK/backup.env" bash "$BACKUP" >"$OUT" 2>&1
    status=$?
}

restore() {
    BACKUP_CONFIG="$WORK/backup.env" bash "$RESTORE" "$@" >"$OUT" 2>&1
    status=$?
}

remote_count() { find "$WORK/remote" -type f 2>/dev/null | wc -l; }
db_file() { find "$WORK/local" -name 'monburan-db-*.dump.age' ! -name '*2020*' | sort | tail -1; }

echo "Preparando o PostgreSQL e o app de mentira..."
docker rm -f "$PG" "$APP" "$NOPG" >/dev/null 2>&1
docker run -d --name "$PG" -e POSTGRES_USER=monburan -e POSTGRES_PASSWORD=smoke -e POSTGRES_DB=monburan "$PG_IMAGE" >/dev/null
docker run -d --name "$APP" alpine:3.21 sh -c 'mkdir -p /app/wwwroot/uploads && echo "curriculo do Joao" > /app/wwwroot/uploads/joao.txt && sleep 3600' >/dev/null
docker run -d --name "$NOPG" alpine:3.21 sleep 3600 >/dev/null
wait_for_pg "$PG" || { echo "O PostgreSQL de teste não ficou pronto"; exit 1; }
for _ in $(seq 1 30); do
    docker exec "$APP" test -f /app/wwwroot/uploads/joao.txt 2>/dev/null && break
    sleep 0.5
done

docker exec -i "$PG" psql -U monburan -d monburan -v ON_ERROR_STOP=1 -q >/dev/null <<'SQL'
CREATE SCHEMA "Identity";
CREATE TABLE "Identity"."Users" ("Id" text PRIMARY KEY, "Email" text NOT NULL);
CREATE TABLE "Jobs" ("Id" serial PRIMARY KEY, "Title" text NOT NULL, "Description" text);
CREATE TABLE "Advertisers" ("Id" serial PRIMARY KEY, "Name" text);
CREATE TABLE "Candidates" ("Id" serial PRIMARY KEY, "Name" text);
CREATE TABLE "__EFMigrationsHistory" ("MigrationId" text PRIMARY KEY, "ProductVersion" text);
INSERT INTO "Identity"."Users" VALUES ('1', 'ana@example.com'), ('2', 'bruno@example.com');
INSERT INTO "Jobs" ("Title", "Description") VALUES ('Médico', 'Descrição com acentuação: ação, coração, ônibus');
INSERT INTO "Jobs" ("Title", "Description") SELECT 'Vaga ' || g, repeat('texto ', 50) FROM generate_series(1, 200) g;
INSERT INTO "Advertisers" ("Name") VALUES ('Empresa Ação Ltda');
INSERT INTO "Candidates" ("Name") VALUES ('João'), ('Maria'), ('José');
INSERT INTO "__EFMigrationsHistory" VALUES ('20260101000000_Initial', '10.0.0');
SQL

age-keygen -o "$WORK/key.txt" 2>/dev/null
age-keygen -o "$WORK/other-key.txt" 2>/dev/null
RECIPIENT=$(age-keygen -y "$WORK/key.txt")
write_config 30

echo
echo "1. Backup completo"
backup
expect "termina sem erro" test "$status" -eq 0
expect "o remoto recebeu o banco" bash -c "ls $WORK/remote/monburan-db-*.dump.age"
expect "o remoto recebeu os uploads" bash -c "ls $WORK/remote/monburan-uploads-*.tar.gz.age"
expect "o disco local também guarda os dois" test "$(find "$WORK/local" -name 'monburan-*.age' | wc -l)" -eq 2
expect "nenhum arquivo .partial sobrou" test -z "$(find "$WORK" -name '*.partial')"
expect "a pasta local é 700" test "$(stat -c %a "$WORK/local")" = 700
expect "os arquivos são 600" test "$(stat -c %a "$(db_file)")" = 600
expect "o arquivo é age (criptografado)" bash -c "head -c 21 '$(db_file)' | grep -q 'age-encryption.org/v1'"
expect "e não é um dump do PostgreSQL em claro" bash -c "! head -c 5 '$(db_file)' | grep -aq PGDMP"
expect "com a chave certa sai um dump do PostgreSQL" bash -c "age -d -i $WORK/key.txt '$(db_file)' | head -c 5 | grep -aq PGDMP"

echo
echo "2. Restauração"
restore "$(db_file)" --identity "$WORK/key.txt"
expect "com a chave certa termina sem erro" test "$status" -eq 0
expect "devolve as contas, vagas, empresas, candidatos e migrations" \
    grep -q 'Contas: 2 | Vagas: 201 | Empresas: 1 | Candidatos: 3 | Migrations aplicadas: 1' "$OUT"
expect "confere que o backup dos uploads abre" grep -q 'Uploads: o arquivo abre e tem 1 arquivo' "$OUT"
expect "apagou o contêiner descartável" test -z "$(docker ps -aq -f name=monburan-restore-test)"
expect "os dados voltam com acentuação intacta" bash -c \
    "age -d -i $WORK/key.txt '$(db_file)' | docker run -i --rm $PG_IMAGE pg_restore -f - 2>/dev/null | grep -q 'Descrição com acentuação: ação, coração, ônibus'"

restore --latest-remote --identity "$WORK/key.txt"
expect "--latest-remote também restaura" grep -q 'Restauração OK' "$OUT"
expect "--latest-remote confere os uploads do mesmo dia" grep -q 'Uploads: o arquivo abre' "$OUT"

restore "$(db_file)" --identity "$WORK/other-key.txt"
expect "chave errada: falha" test "$status" -ne 0
expect "explica que não descriptografou" grep -q 'não consegui descriptografar' "$OUT"
expect "e também apaga o contêiner e os temporários" test -z "$(docker ps -aq -f name=monburan-restore-test)"

head -c 600 "$(db_file)" >"$WORK/truncated.dump.age"
restore "$WORK/truncated.dump.age" --identity "$WORK/key.txt"
expect "arquivo cortado: falha" test "$status" -ne 0

echo
echo "3. Falhas do backup não deixam rastro"
before=$(remote_count)
docker stop "$PG" >/dev/null
backup
expect "banco parado: falha" test "$status" -ne 0
expect "explica que o contêiner não está rodando" grep -q 'não está rodando' "$OUT"
expect "o remoto continua igual" test "$(remote_count)" -eq "$before"

sed -i "s/^POSTGRES_CONTAINER=.*/POSTGRES_CONTAINER=$NOPG/" "$WORK/backup.env"
backup
expect "pg_dump ausente (falha no meio do pipe): falha" test "$status" -ne 0
expect "registra o erro" grep -q 'ERRO: o backup falhou' "$OUT"
expect "nenhum arquivo .partial sobrou" test -z "$(find "$WORK" -name '*.partial')"
expect "o remoto continua igual" test "$(remote_count)" -eq "$before"
write_config 30
docker start "$PG" >/dev/null
wait_for_pg "$PG" || { echo "O PostgreSQL de teste não voltou"; exit 1; }

echo
echo "4. Retenção"
touch -d '40 days ago' "$WORK/remote/outro-arquivo.txt"
for old in "$WORK/remote" "$WORK/local"; do
    : >"$old/monburan-db-20200101T000000Z.dump.age"
    touch -d '40 days ago' "$old/monburan-db-20200101T000000Z.dump.age"
done
backup
expect "termina sem erro" test "$status" -eq 0
expect "apaga do remoto o que passou de 30 dias" test ! -e "$WORK/remote/monburan-db-20200101T000000Z.dump.age"
expect "apaga do disco o que passou de 7 dias" test ! -e "$WORK/local/monburan-db-20200101T000000Z.dump.age"
expect "não apaga arquivo que não é backup" test -e "$WORK/remote/outro-arquivo.txt"
expect "mantém os backups recentes" test "$(find "$WORK/remote" -name 'monburan-db-*' ! -name '*20200101*' | wc -l)" -ge 2

: >"$WORK/remote/monburan-db-20200101T000000Z.dump.age"
touch -d '40 days ago' "$WORK/remote/monburan-db-20200101T000000Z.dump.age"
write_config 0
backup
expect "KEEP_REMOTE_DAYS=0 termina sem erro" test "$status" -eq 0
expect "KEEP_REMOTE_DAYS=0 não apaga nada do remoto" test -e "$WORK/remote/monburan-db-20200101T000000Z.dump.age"
write_config 30

echo
echo "5. Configuração"
chmod 666 "$WORK/backup.env"
backup
expect "arquivo de configuração gravável por outros é recusado" grep -q 'sem escrita para grupo/outros' "$OUT"
chmod 600 "$WORK/backup.env"

BACKUP_CONFIG=/nao-existe bash "$BACKUP" >"$OUT" 2>&1
status=$?
expect "sem BACKUP_AGE_RECIPIENT: falha pedindo a chave" bash -c "test $status -ne 0 && grep -q BACKUP_AGE_RECIPIENT $OUT"

sed -i 's/^BACKUP_AGE_RECIPIENT=.*/BACKUP_AGE_RECIPIENT=chave-qualquer/' "$WORK/backup.env"
backup
expect "chave que não é age1... é recusada" grep -q 'precisa ser uma chave pública age' "$OUT"

echo
echo "$passed ok, $failed falhas"
[[ $failed -eq 0 ]]
