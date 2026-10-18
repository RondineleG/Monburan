#!/usr/bin/env bash
# ============================================================
# Monburan — Teste de fumaça do harden.sh
#
# Roda o harden.sh de verdade (--apply) em um Ubuntu descartável (contêiner) e confere o que ele escreve e se as travas contra
# ficar sem acesso funcionam (sem chave, configuração do sshd que vence a nossa, configuração inválida...). apt-get,
# systemctl e ufw são trocados por registradores de chamadas; sshd, fail2ban e ss são os de verdade.
# Não altera a máquina de quem roda: precisa só de Docker.
#
#   bash infra/vps/tests/harden-smoke.sh
# ============================================================
set -uo pipefail

if [[ "${INSIDE_CONTAINER:-0}" != "1" ]]; then
    repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
    exec docker run --rm -e INSIDE_CONTAINER=1 -v "$repo":/repo:ro "${TEST_IMAGE:-ubuntu:24.04}" \
        bash /repo/infra/vps/tests/harden-smoke.sh
fi

HARDEN=/repo/infra/vps/harden.sh
OUT=/tmp/harden.out
CALLS=/tmp/calls.log
DROPIN=/etc/ssh/sshd_config.d/00-monburan-hardening.conf
JAIL=/etc/fail2ban/jail.d/monburan.local
passed=0
failed=0

ok() { passed=$((passed + 1)); printf '  ok     %s\n' "$1"; }
bad() { failed=$((failed + 1)); printf '  FALHOU %s\n' "$1"; }

# expect <descrição> <comando...>: o comando precisa terminar com sucesso
expect() {
    local name=$1
    shift
    if "$@" >/dev/null 2>&1; then ok "$name"; else bad "$name"; fi
}

# O harden.sh roda com a saída guardada em $OUT; o status fica em $status
harden() {
    bash "$HARDEN" "$@" >"$OUT" 2>&1
    status=$?
}

reset_state() {
    rm -f /etc/ssh/sshd_config.d/*.conf "$JAIL" /etc/apt/apt.conf.d/20auto-upgrades /etc/apt/apt.conf.d/52monburan-unattended-upgrades
    cp /tmp/sshd_config.orig /etc/ssh/sshd_config
    rm -f /root/.ssh/authorized_keys
    : >"$CALLS"
}

with_key() {
    printf '%s\n' "$PUBKEY" >/root/.ssh/authorized_keys
}

effective() {
    sshd -T 2>/dev/null | grep -x "$1"
}

# Carrega as funções do harden.sh em um subshell e pergunta quais portas ele detecta
detect_ports() {
    (
        export HARDEN_SOURCE_ONLY=1
        # shellcheck disable=SC1090
        source "$HARDEN"
        detect_ssh_ports
    )
}

export DEBIAN_FRONTEND=noninteractive
echo "Preparando o Ubuntu de teste (apt)..."
apt-get update -qq >/dev/null 2>&1
if ! apt-get install -y -qq openssh-server fail2ban unattended-upgrades python3-systemd ufw iproute2 curl >/dev/null 2>&1; then
    echo "Não consegui instalar os pacotes de teste"
    exit 1
fi
ssh-keygen -A >/dev/null
mkdir -p /run/sshd /root/.ssh
cp /etc/ssh/sshd_config /tmp/sshd_config.orig
ssh-keygen -q -t ed25519 -N '' -f /tmp/test_key
PUBKEY=$(cat /tmp/test_key.pub)

# Registradores de chamadas no lugar de apt-get, systemctl e ufw (o contêiner não tem systemd nem permissão de firewall)
mkdir -p /stubs
for tool in apt-get systemctl ufw; do
    printf '#!/bin/sh\necho "%s $*" >> /tmp/calls.log\nexit 0\n' "$tool" >"/stubs/$tool"
    chmod +x "/stubs/$tool"
done
export PATH="/stubs:$PATH"

echo
echo "1. Plano (sem --apply) não altera nada"
reset_state
harden
expect "termina sem erro" test "$status" -eq 0
expect "mostra o plano do firewall" grep -q '\[plano\] ufw limit 22/tcp' "$OUT"
expect "não cria o arquivo do sshd" test ! -e "$DROPIN"
expect "não cria o jail do fail2ban" test ! -e "$JAIL"
expect "não chama apt-get, systemctl nem ufw" test ! -s "$CALLS"

echo
echo "2. Travas do SSH: sem chave autorizada a senha não é desligada"
reset_state
harden --apply --skip-ufw --yes
expect "falha" test "$status" -ne 0
expect "explica o motivo" grep -q 'nenhuma chave SSH autorizada' "$OUT"
expect "não cria o arquivo do sshd" test ! -e "$DROPIN"

reset_state
printf '# %s\n' "$PUBKEY" >/root/.ssh/authorized_keys
harden --apply --skip-ufw --yes
expect "chave só comentada também não vale" test "$status" -ne 0
expect "e o arquivo do sshd não existe" test ! -e "$DROPIN"

reset_state
with_key
harden --apply --skip-ufw </dev/null
expect "sem terminal e sem --yes, falha" test "$status" -ne 0
expect "pede --yes" grep -q 'sem terminal' "$OUT"
expect "e o arquivo do sshd não existe" test ! -e "$DROPIN"

reset_state
with_key
printf 'nao\n' | script -qec "bash $HARDEN --apply --skip-ufw" /dev/null >"$OUT" 2>&1
status=$?
expect "resposta 'nao' aborta" test "$status" -ne 0
expect "e o arquivo do sshd não existe" test ! -e "$DROPIN"

reset_state
with_key
printf 'sim\n' | script -qec "bash $HARDEN --apply --skip-ufw" /dev/null >"$OUT" 2>&1
status=$?
expect "resposta 'sim' segue" test "$status" -eq 0
expect "e o arquivo do sshd existe" test -f "$DROPIN"

echo
echo "3. Aplicação completa do SSH, do fail2ban e das atualizações"
reset_state
printf 'PasswordAuthentication yes\n' >/etc/ssh/sshd_config.d/50-cloud-init.conf
printf 'command="/bin/true",no-pty %s\n' "$PUBKEY" >/root/.ssh/authorized_keys
mkdir -p /tmp/inst
printf 'X=1\n' >/tmp/inst/.env
chmod 644 /tmp/inst/.env
INSTALL_DIR=/tmp/inst harden --apply --skip-ufw --yes
expect "termina sem erro (chave com opções command=... vale)" test "$status" -eq 0
expect "senha desligada, mesmo com 50-cloud-init.conf pedindo senha" effective 'passwordauthentication no'
expect "teclado-interativo desligado" effective 'kbdinteractiveauthentication no'
expect "root só por chave" effective 'permitrootlogin without-password'
expect "pubkey ligada" effective 'pubkeyauthentication yes'
expect "o jail do fail2ban usa a porta 22" grep -q '^port    = 22$' "$JAIL"
expect "o jail não força de onde ler o registro (vale o padrão da distribuição)" bash -c "! grep -q '^backend' $JAIL"
expect "o fail2ban aceita a configuração" fail2ban-client -t
expect "ligou as atualizações automáticas" grep -q 'Unattended-Upgrade "1"' /etc/apt/apt.conf.d/20auto-upgrades
expect "sem reinício automático" grep -q 'Automatic-Reboot "false"' /etc/apt/apt.conf.d/52monburan-unattended-upgrades
expect "instalou os pacotes" grep -q 'apt-get install -y ufw fail2ban unattended-upgrades python3-systemd' "$CALLS"
expect "recarregou o ssh" grep -qE 'systemctl (reload|restart) ssh' "$CALLS"
expect "ajustou a permissão do .env" test "$(stat -c %a /tmp/inst/.env)" = 600
expect "lembra de abrir outra sessão" grep -q 'NÃO feche esta sessão' "$OUT"

INSTALL_DIR=/tmp/inst harden --apply --skip-ufw --yes
expect "rodar de novo também funciona (idempotente)" test "$status" -eq 0
expect "e a senha continua desligada" effective 'passwordauthentication no'

echo
echo "4. Travas do SSH: configuração que o script não consegue garantir"
reset_state
with_key
printf 'PasswordAuthentication yes\n' | cat - /tmp/sshd_config.orig >/etc/ssh/sshd_config
harden --apply --skip-ufw --yes
expect "outra configuração vence: falha" test "$status" -ne 0
expect "explica que a senha continuaria aceita" grep -q 'a senha continuaria aceita' "$OUT"
expect "remove o arquivo e não recarrega o ssh" bash -c "test ! -e $DROPIN && ! grep -q 'systemctl .* ssh' $CALLS"

reset_state
with_key
printf 'BogusDirective yes\n' >/etc/ssh/sshd_config.d/10-bad.conf
harden --apply --skip-ufw --yes
expect "configuração inválida: falha" test "$status" -ne 0
expect "sshd -t recusou" grep -q 'sshd -t recusou' "$OUT"
expect "remove o arquivo e não recarrega o ssh" bash -c "test ! -e $DROPIN && ! grep -q 'systemctl .* ssh' $CALLS"

reset_state
with_key
grep -v '^Include' /tmp/sshd_config.orig >/etc/ssh/sshd_config
harden --apply --skip-ufw --yes
expect "sshd_config sem Include: falha" test "$status" -ne 0
expect "avisa que o drop-in não seria lido" grep -q 'não inclui sshd_config.d' "$OUT"

echo
echo "5. Detecção das portas do SSH"
reset_state
/usr/sbin/sshd -p 22 -p 2222
sleep 1
expect "lê as portas do sshd em execução (22 e 2222)" test "$(detect_ports)" = "22 2222"
expect "inclui a porta da sessão atual (SSH_CONNECTION)" \
    test "$(SSH_CONNECTION='198.51.100.7 51234 10.0.0.5 2200' detect_ports)" = "22 2200 2222"
expect "SSH_PORT aceita várias portas" test "$(SSH_PORT=2201,2202 detect_ports)" = "2201 2202"
pkill sshd
sleep 1
expect "sem sshd ouvindo, usa a configurada (22)" test "$(detect_ports)" = "22"

echo
echo "6. Firewall"
reset_state
with_key
SSH_PORT=2200,2201 FAIL2BAN_IGNOREIP="203.0.113.9" harden --apply --skip-ssh --yes
expect "termina sem erro" test "$status" -eq 0
expect "libera cada porta do SSH com limite" bash -c "grep -q '^ufw limit 2200/tcp' $CALLS && grep -q '^ufw limit 2201/tcp' $CALLS"
expect "libera 80 e 443" bash -c "grep -q '^ufw allow 80/tcp' $CALLS && grep -q '^ufw allow 443/tcp' $CALLS"
expect "nega o resto por padrão" grep -q '^ufw default deny incoming' "$CALLS"
expect "liga o firewall só depois de todas as regras (as do SSH inclusive)" \
    bash -c "test \$(grep -nE '^ufw (limit|allow|default)' $CALLS | tail -1 | cut -d: -f1) -lt \$(grep -n 'force enable' $CALLS | cut -d: -f1)"
expect "o fail2ban vigia as mesmas portas" grep -q '^port    = 2200,2201$' "$JAIL"
expect "o fail2ban respeita os endereços ignorados" grep -q '^ignoreip = 127.0.0.1/8 ::1 203.0.113.9$' "$JAIL"
expect "com --skip-ssh não mexe no SSH" test ! -e "$DROPIN"

echo
echo "$passed ok, $failed falhas"
[[ $failed -eq 0 ]]
