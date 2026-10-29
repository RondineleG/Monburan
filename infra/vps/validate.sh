#!/usr/bin/env bash
# ============================================================
# Monburan — Validação do servidor (só leitura)
#
# Confere se o servidor está de pé e como está a segurança dele. Duas partes, que dispensam senha:
#   - DE FORA (qualquer máquina): DNS, portas, /health, redirecionamento para HTTPS, certificado, cabeçalhos de segurança
#     (CSP, HSTS, versão do nginx), cookies com Secure e se o nginx descarta quem chega pelo IP;
#   - POR DENTRO (por SSH com chave, ou rodando no próprio servidor): disco gravável e sem erros no kernel, carga, espaço, SSH só
#     por chave, firewall, portas abertas, fail2ban, atualizações, contêineres, .env, nginx e backup.
# Nada é alterado. Cada linha sai como [ok], [AVISO] ou [FALHA]; o código de saída é 1 se houver [FALHA].
#
# Uso:
#   bash validate.sh                                          # de fora: https://monburan.com.br
#   bash validate.sh --ip 203.0.113.10                        # de fora, mandando as chamadas para esse IP (servidor novo, antes do DNS)
#   bash validate.sh --ssh root@203.0.113.10 -i ~/.ssh/chave  # de fora e por dentro
#   bash validate.sh --only-inside                            # no próprio servidor (como root)
#
# Opções:
#   --host NOME      domínio do site (padrão: monburan.com.br)
#   --ip IP          envia as chamadas HTTP(S) para este IP, como se o DNS já apontasse para ele
#   --ssh ALVO       usuario@servidor para a parte de dentro; -i CHAVE informa a chave
#   --only-outside   só a parte de fora         --only-inside   só a parte de dentro (com --ssh, ou aqui)
#   --insecure       aceita certificado que não é de uma autoridade (testes)
#   --http-port N    --https-port N    portas do site (padrão 80 e 443)
# ============================================================
set -uo pipefail

HOST="monburan.com.br"
IP=""
SSH_TARGET=""
SSH_KEY=""
INSECURE=0
RUN_OUTSIDE=1
RUN_INSIDE=1
HTTP_PORT=80
HTTPS_PORT=443

usage() {
    sed -n '2,/^# =====/p' "$0" | sed 's/^# \{0,1\}//' | sed '$d'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --host) HOST="${2:?informe o domínio}"; shift 2 ;;
        --ip) IP="${2:?informe o IP}"; shift 2 ;;
        --ssh) SSH_TARGET="${2:?informe usuario@servidor}"; shift 2 ;;
        -i) SSH_KEY="${2:?informe a chave}"; shift 2 ;;
        --http-port) HTTP_PORT="${2:?informe a porta}"; shift 2 ;;
        --https-port) HTTPS_PORT="${2:?informe a porta}"; shift 2 ;;
        --insecure) INSECURE=1; shift ;;
        --only-outside) RUN_INSIDE=0; shift ;;
        --only-inside) RUN_OUTSIDE=0; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Opção desconhecida: $1" >&2; usage; exit 2 ;;
    esac
done

ok_count=0
warn_count=0
fail_count=0

say() {
    case "$1" in
        ok) ok_count=$((ok_count + 1)); printf '  [ok]     %s\n' "$2" ;;
        aviso) warn_count=$((warn_count + 1)); printf '  [AVISO]  %s\n' "$2" ;;
        falha) fail_count=$((fail_count + 1)); printf '  [FALHA]  %s\n' "$2" ;;
        *) printf '  [info]   %s\n' "$2" ;;
    esac
}

section() { printf '\n%s\n' "$*"; }

# ---------------------------------------------------------------------------------------------------------------------
# Parte de dentro: um script que imprime "nível<TAB>mensagem". Roda aqui ou por SSH, sempre só lendo.
# ---------------------------------------------------------------------------------------------------------------------
read -r -d '' INSIDE_SCRIPT <<'INSIDE' || true
emit() { printf '%s\t%s\n' "$1" "$2"; }
PATH="$PATH:/usr/sbin:/sbin"

root_options=$(awk '$2 == "/" { print $4 }' /proc/mounts 2>/dev/null | tail -1)
case ",${root_options}," in
    *,ro,*) emit falha "o sistema de arquivos raiz está SOMENTE LEITURA (sinal de erro de disco)" ;;
    *) emit ok "sistema de arquivos raiz gravável" ;;
esac

disk_errors=$(dmesg 2>/dev/null | grep -ciE 'i/o error|ext4-fs error|remounting filesystem read-only|blk_update_request' || true)
if [ "${disk_errors:-0}" -gt 0 ]; then
    emit falha "o kernel registrou ${disk_errors} linha(s) de erro de disco neste boot (dmesg | grep -i 'i/o error')"
else
    emit ok "nenhum erro de disco no log do kernel deste boot"
fi

read -r load1 _ < /proc/loadavg 2>/dev/null || load1=0
cpus=$(nproc 2>/dev/null || echo 1)
if awk -v load="$load1" -v cpus="$cpus" 'BEGIN { exit !(load > 2 * cpus) }'; then
    emit aviso "carga alta: ${load1} em ${cpus} CPU(s)"
else
    emit ok "carga normal (${load1} em ${cpus} CPU)"
fi

used=$(df -P / 2>/dev/null | awk 'NR == 2 { gsub("%", "", $5); print $5 }')
if [ -n "$used" ]; then
    if [ "$used" -ge 95 ]; then emit falha "disco com ${used}% de uso";
    elif [ "$used" -ge 85 ]; then emit aviso "disco com ${used}% de uso";
    else emit ok "disco com ${used}% de uso"; fi
fi

available_kb=$(awk '/MemAvailable/ { print $2 }' /proc/meminfo 2>/dev/null)
if [ -n "$available_kb" ] && [ "$available_kb" -lt 100000 ]; then emit aviso "pouca memória livre: $((available_kb / 1024)) MB"; else emit ok "memória livre: $((${available_kb:-0} / 1024)) MB"; fi

if grep -rqs 'archive.debian.org' /etc/apt/sources.list /etc/apt/sources.list.d/ 2>/dev/null; then
    emit aviso "sistema sem suporte de segurança (pacotes de archive.debian.org): troque por uma versão com suporte"
else
    emit ok "repositórios do sistema com suporte"
fi
[ -f /var/run/reboot-required ] && emit aviso "há atualização que pede reinício (/var/run/reboot-required)"

if command -v sshd >/dev/null 2>&1 && [ "$(id -u)" -eq 0 ]; then
    effective=$(sshd -T 2>/dev/null)
    if grep -qx 'passwordauthentication no' <<<"$effective"; then emit ok "SSH só por chave (senha desligada)"; else emit aviso "o SSH aceita senha: rode harden.sh"; fi
    root_login=$(awk '/^permitrootlogin / { print $2 }' <<<"$effective")
    case "$root_login" in
        no|prohibit-password|without-password) emit ok "root só entra por chave (${root_login})" ;;
        *) emit aviso "root pode entrar com senha (permitrootlogin ${root_login:-?})" ;;
    esac
fi

if command -v ufw >/dev/null 2>&1; then
    if ufw status 2>/dev/null | grep -q '^Status: active'; then
        emit ok "firewall (ufw) ligado"
        extra=$(ufw status 2>/dev/null | awk '/ALLOW|LIMIT/ && !/\(v6\)/ { split($1, p, "/"); if (p[1] !~ /^(22|80|443)$/) print $1 }' | sort -u | tr '\n' ' ')
        if [ -n "$extra" ]; then emit aviso "o ufw libera além de 22, 80 e 443: ${extra}"; fi
    else
        emit aviso "firewall (ufw) desligado: rode harden.sh"
    fi
else
    emit aviso "ufw não instalado"
fi

exposed=$(ss -ltnH 2>/dev/null | awk '{ n = split($4, a, ":"); port = a[n]; addr = substr($4, 1, length($4) - length(port) - 1); if (addr !~ /^(127\.|\[::1\])/ && port !~ /^(22|80|443)$/) print port }' | sort -un | tr '\n' ' ')
if [ -n "$exposed" ]; then emit aviso "portas abertas ao mundo além de 22, 80 e 443: ${exposed}"; else emit ok "só 22, 80 e 443 escutam fora do loopback"; fi

if systemctl is-active fail2ban >/dev/null 2>&1; then
    if fail2ban-client status sshd >/dev/null 2>&1; then emit ok "fail2ban ativo, com a jail do SSH"; else emit aviso "fail2ban ativo, mas sem a jail do SSH"; fi
else
    emit aviso "fail2ban não está ativo"
fi

if dpkg -s unattended-upgrades >/dev/null 2>&1 && systemctl is-enabled apt-daily-upgrade.timer >/dev/null 2>&1; then
    emit ok "atualizações automáticas de segurança ligadas"
else
    emit aviso "atualizações automáticas desligadas"
fi

if command -v docker >/dev/null 2>&1; then
    for name in monburan-app; do
        state=$(docker ps --filter "name=^${name}$" --format '{{.Status}}' 2>/dev/null)
        case "$state" in
            "") emit falha "contêiner ${name} não está rodando" ;;
            *unhealthy*|*Restarting*) emit falha "contêiner ${name}: ${state}" ;;
            *) emit ok "contêiner ${name}: ${state}" ;;
        esac
    done
    code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 http://127.0.0.1:8080/health 2>/dev/null)
    if [ "$code" = "200" ]; then emit ok "o app responde /health localmente"; else emit falha "o app não responde /health localmente (${code:-sem resposta})"; fi
    # O banco fica na nuvem: a contagem confere de quebra que a VPS alcança o banco (BACKUP_DB_URL do backup.env)
    if [ -f /opt/monburan/backup.env ]; then
        dburl=$(awk -F= '/^BACKUP_DB_URL=/ { print substr($0, index($0, "=") + 1) }' /opt/monburan/backup.env)
        if [ -n "$dburl" ]; then
            users=$(docker run --rm postgres:18-alpine psql "$dburl" -tAc 'select count(*) from "Identity"."Users"' 2>/dev/null | head -1)
            if [ -n "$users" ]; then emit ok "banco na nuvem alcançado (${users} contas)"; else emit aviso "não consegui consultar o banco na nuvem (BACKUP_DB_URL)"; fi
        fi
    fi
fi

if [ -d /opt/monburan ]; then
    # deploy.sh e backup.sh rodam como root: quem puder editá-los vira root
    foreign=$(find /opt/monburan -xdev \( ! -user 0 -o ! -group 0 -o -perm /022 \) -print 2>/dev/null | head -3 | tr '\n' ' ')
    if [ -z "$foreign" ]; then emit ok "arquivos de /opt/monburan só do root"; else emit falha "arquivos de /opt/monburan com outro dono ou com escrita para grupo/outros: ${foreign}(o deploy.sh corrige)"; fi
fi

if [ -f /opt/monburan/.env ]; then
    mode=$(stat -c %a /opt/monburan/.env 2>/dev/null)
    if [ "$mode" = "600" ]; then emit ok ".env com permissão 600"; else emit aviso ".env com permissão ${mode} (use chmod 600)"; fi
    hosts=$(awk -F= '/^ALLOWED_HOSTS=/ { print $2 }' /opt/monburan/.env)
    if [ -z "$hosts" ] || [ "$hosts" = "*" ]; then emit aviso "ALLOWED_HOSTS aberto (*): restrinja ao domínio"; else emit ok "ALLOWED_HOSTS restrito ao domínio"; fi
    grep -q '^ADMIN_EMAILS=.' /opt/monburan/.env && emit ok "ADMIN_EMAILS definido" || emit aviso "ADMIN_EMAILS vazio: ninguém vira administrador"
    grep -qi '^REQUIRE_ADMIN_2FA=false' /opt/monburan/.env && emit aviso "REQUIRE_ADMIN_2FA=false: administradores entram sem 2FA"
fi

if command -v nginx >/dev/null 2>&1; then
    if nginx -t >/dev/null 2>&1; then emit ok "nginx -t aceita a configuração"; else emit falha "nginx -t recusa a configuração"; fi
    systemctl is-active nginx >/dev/null 2>&1 && emit ok "nginx ativo" || emit falha "nginx não está ativo"
fi

if [ -d /opt/monburan/backups ]; then
    if systemctl is-active monburan-backup.timer >/dev/null 2>&1; then emit ok "timer do backup ligado"; else emit aviso "timer do backup desligado"; fi
    newest=$(find /opt/monburan/backups -maxdepth 1 -name 'monburan-db-*.age' -printf '%T@\n' 2>/dev/null | sort -n | tail -1)
    if [ -n "$newest" ]; then
        age_hours=$(( ($(date +%s) - ${newest%.*}) / 3600 ))
        if [ "$age_hours" -le 26 ]; then emit ok "último backup do banco tem ${age_hours} h"; else emit aviso "último backup do banco tem ${age_hours} h"; fi
    else
        emit aviso "nenhum backup do banco em /opt/monburan/backups"
    fi
    if [ -f /opt/monburan/backup.env ]; then
        grep -q '^BACKUP_REMOTE=.' /opt/monburan/backup.env && emit ok "backup com destino fora do servidor" || emit aviso "backup SÓ no disco da VPS (BACKUP_REMOTE vazio)"
    fi
fi
INSIDE

run_inside_checks() {
    local output
    if [[ -n "$SSH_TARGET" ]]; then
        local ssh_args=(-o BatchMode=yes -o ConnectTimeout=15 -o StrictHostKeyChecking=yes)
        [[ -n "$SSH_KEY" ]] && ssh_args+=(-i "$SSH_KEY" -o IdentitiesOnly=yes)
        if ! output=$(ssh "${ssh_args[@]}" "$SSH_TARGET" 'bash -s' <<<"$INSIDE_SCRIPT" 2>&1); then
            say falha "não consegui conferir por dentro: o SSH de ${SSH_TARGET} não respondeu ou recusou a chave"
            return
        fi
    else
        output=$(bash -c "$INSIDE_SCRIPT" 2>&1)
    fi

    local level message
    while IFS=$'\t' read -r level message; do
        [[ -n "$level" ]] || continue
        say "$level" "$message"
    done <<<"$output"
}

# ---------------------------------------------------------------------------------------------------------------------
# Parte de fora
# ---------------------------------------------------------------------------------------------------------------------
header_of() { grep -i "^$1:" <<<"$headers" | head -1 | tr -d '\r'; }

run_outside_checks() {
    local curl_args=(-sS --max-time 15)
    [[ $INSECURE -eq 1 ]] && curl_args+=(-k)
    if [[ -n "$IP" ]]; then
        curl_args+=(--resolve "$HOST:$HTTPS_PORT:$IP" --resolve "$HOST:$HTTP_PORT:$IP")
    fi
    local site_https="https://$HOST:$HTTPS_PORT"
    local site_http="http://$HOST:$HTTP_PORT"

    section "De fora ($HOST${IP:+ via $IP})"

    local resolved target
    resolved=$( { getent ahostsv4 "$HOST" || getent hosts "$HOST"; } 2>/dev/null | awk 'NR == 1 { print $1 }')
    if [[ -z "$resolved" ]]; then
        say aviso "DNS: $HOST não resolve nesta máquina"
    elif [[ -n "$IP" && "$resolved" != "$IP" ]]; then
        say aviso "DNS: $HOST aponta para $resolved, e não para $IP (troque o DNS quando o servidor novo estiver pronto)"
    else
        say ok "DNS: $HOST -> $resolved"
    fi
    target="${IP:-$resolved}"

    local open_ports=0
    if [[ -n "$target" ]]; then
        local port
        for port in "$HTTP_PORT" "$HTTPS_PORT"; do
            if timeout 6 bash -c "exec 3<>/dev/tcp/$target/$port" 2>/dev/null; then
                open_ports=$((open_ports + 1))
                say ok "porta $port aberta em $target"
            else
                say falha "porta $port não abre em $target"
            fi
        done
        if timeout 6 bash -c "exec 3<>/dev/tcp/$target/22" 2>/dev/null; then
            local banner
            banner=$(timeout 8 bash -c "exec 3<>/dev/tcp/$target/22; read -t 6 -r line <&3; echo \"\$line\"" 2>/dev/null)
            if [[ -n "$banner" ]]; then say ok "SSH responde ($banner)"; else say falha "porta 22 abre, mas o SSH não dá o banner (servidor travado ou sobrecarregado)"; fi
        else
            say info "porta 22 fechada ou filtrada para esta máquina"
        fi
    fi

    if [[ $open_ports -eq 0 ]]; then
        say falha "o servidor não responde nas portas do site: pulei as conferências de HTTP, certificado, cabeçalhos e cookies"
        return
    fi

    local code
    code=$(curl "${curl_args[@]}" -o /dev/null -w '%{http_code}' "$site_https/health" 2>/dev/null)
    if [[ "$code" == "200" ]]; then say ok "$site_https/health responde 200"; else say falha "$site_https/health respondeu ${code:-sem resposta} (esperado 200)"; fi

    local redirect
    redirect=$(curl "${curl_args[@]}" -o /dev/null -w '%{http_code} %{redirect_url}' "$site_http/" 2>/dev/null)
    case "$redirect" in
        "301 https://"*) say ok "HTTP redireciona para HTTPS (301)" ;;
        *) say falha "HTTP não redireciona para HTTPS (resposta: ${redirect:-nenhuma})" ;;
    esac

    local cert_end days
    cert_end=$(echo | timeout 10 openssl s_client -servername "$HOST" -connect "${target:-$HOST}:$HTTPS_PORT" 2>/dev/null | openssl x509 -noout -enddate 2>/dev/null | cut -d= -f2)
    if [[ -n "$cert_end" ]]; then
        days=$(( ($(date -d "$cert_end" +%s) - $(date +%s)) / 86400 ))
        if [[ $days -lt 0 ]]; then say falha "certificado vencido há $((-days)) dia(s)";
        elif [[ $days -lt 14 ]]; then say aviso "certificado vence em $days dia(s)";
        else say ok "certificado válido por mais $days dias"; fi
    else
        say aviso "não consegui ler o certificado"
    fi

    headers=$(curl "${curl_args[@]}" -D - -o /dev/null "$site_https/" 2>/dev/null)
    if [[ -z "$headers" ]]; then
        say falha "sem resposta em $site_https/: pulei os cabeçalhos e os cookies"
    else
        local value
        value=$(header_of server)
        if [[ "$value" =~ /[0-9] ]]; then say aviso "o cabeçalho Server revela a versão (${value#*: }): use o nginx.conf novo (server_tokens off)"; else say ok "cabeçalho Server sem versão"; fi

        value=$(header_of content-security-policy)
        if [[ -z "$value" ]]; then
            say falha "sem Content-Security-Policy"
        else
            local script_src
            script_src=$(grep -oiE "script-src[^;]*" <<<"$value" | head -1)
            if [[ "$script_src" == *"'unsafe-inline'"* || "$script_src" == *"'unsafe-eval'"* ]]; then
                say falha "a CSP ainda permite script inline ou eval (versão antiga do app no ar?)"
            else
                say ok "CSP sem unsafe-inline nem unsafe-eval nos scripts"
            fi
        fi

        if [[ -n "$(header_of strict-transport-security)" ]]; then say ok "HSTS ligado"; else say aviso "sem Strict-Transport-Security"; fi
        if [[ "$(header_of x-content-type-options)" == *nosniff* ]]; then say ok "X-Content-Type-Options: nosniff"; else say aviso "sem X-Content-Type-Options: nosniff"; fi
        if [[ -n "$(header_of x-frame-options)" || "$(header_of content-security-policy)" == *frame-ancestors* ]]; then say ok "proteção contra frame (clickjacking)"; else say aviso "sem proteção contra frame"; fi
    fi

    local cookies insecure_cookies
    cookies=$(curl "${curl_args[@]}" -D - -o /dev/null "$site_https/Account/Login" 2>/dev/null | tr -d '\r' | grep -i '^set-cookie:')
    if [[ -z "$cookies" ]]; then
        say info "a página de login não definiu cookie (nada a conferir)"
    else
        insecure_cookies=$(grep -vi ';[[:space:]]*secure' <<<"$cookies" | sed -E 's/^[Ss]et-[Cc]ookie:[[:space:]]*([^=]*)=.*/\1/' | tr '\n' ' ')
        if [[ -z "$insecure_cookies" ]]; then say ok "cookies da página de login com Secure"; else say falha "cookie(s) sem Secure: ${insecure_cookies}"; fi
    fi

    if [[ -n "$IP" ]]; then
        local ip_code ip_status
        ip_code=$(curl -sS --max-time 8 -o /dev/null -w '%{http_code}' "http://$IP:$HTTP_PORT/" 2>/dev/null)
        ip_status=$?
        if [[ $ip_status -eq 52 || $ip_status -eq 56 || "$ip_code" == "000" ]]; then
            say ok "quem chega pelo IP, sem o nome do site, é descartado"
        else
            say aviso "o nginx responde ${ip_code} a quem chega pelo IP: use o nginx.conf novo (servidor padrão com 444)"
        fi
    fi
}

echo "Validação do servidor (somente leitura): $(date '+%Y-%m-%d %H:%M:%S')"

if [[ $RUN_OUTSIDE -eq 1 ]]; then
    run_outside_checks
fi

if [[ $RUN_INSIDE -eq 1 && ( -n "$SSH_TARGET" || $RUN_OUTSIDE -eq 0 ) ]]; then
    section "Por dentro (${SSH_TARGET:-esta máquina})"
    run_inside_checks
fi

printf '\n%d ok, %d aviso(s), %d falha(s)\n' "$ok_count" "$warn_count" "$fail_count"
[[ $fail_count -eq 0 ]]
