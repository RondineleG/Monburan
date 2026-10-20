#!/usr/bin/env bash
# ============================================================
# Monburan — Preparação de um servidor novo (Debian 12/13 ou Ubuntu 22.04/24.04)
#
# Instala o que o Monburan usa: Docker (repositório oficial, com a chave conferida), nginx, certbot, age e rclone; limita o tamanho
# dos registros do Docker e do journal do sistema (o disco da VPS é pequeno), cria swap quando a máquina tem pouca memória e deixa
# /opt/monburan pronto.
# NÃO liga o firewall nem mexe no SSH: isso é o harden.sh, que vem a seguir.
#
# NADA é alterado sem --apply: sem argumentos o script só mostra o plano. Pode rodar de novo: cada passo confere antes de agir.
#
# Ordem de um servidor novo (como root):
#   1. bash provision.sh --apply
#   2. bash harden.sh --apply            (com uma segunda sessão SSH aberta)
#   3. copiar infra/vps para /opt/monburan e rodar: bash deploy.sh --email voce@exemplo.com
#   4. backup (docs/SEGURANCA.md) e bash validate.sh --ssh ...
#
# Opções: --apply   --skip-docker (o Docker já está instalado)   --skip-swap
# ============================================================
set -euo pipefail

# Impressão digital oficial da chave do repositório do Docker (https://docs.docker.com/engine/install/debian/)
DOCKER_KEY_FPR="9DC858229FC7DD38854AE2D88D81803C0EBFCD88"

APPLY=0
SKIP_DOCKER=0
SKIP_SWAP=0
INSTALL_DIR="${INSTALL_DIR:-/opt/monburan}"
OS_ID=""
CODENAME=""

usage() {
    sed -n '2,/^# =====/p' "$0" | sed 's/^# \{0,1\}//' | sed '$d'
}

log() { printf '\n==> %s\n' "$*"; }
warn() { printf '    ATENÇÃO: %s\n' "$*" >&2; }
fail() { printf 'ERRO: %s\n' "$*" >&2; exit 1; }

# Executa (com --apply) ou só mostra o que executaria
run() {
    if [[ $APPLY -eq 1 ]]; then
        echo "    + $*"
        "$@"
    else
        echo "    [plano] $*"
    fi
}

# Escreve um arquivo com o conteúdo vindo da entrada padrão: write_file <caminho> <modo>
write_file() {
    local path=$1 mode=$2 content
    content=$(cat)
    if [[ $APPLY -eq 1 ]]; then
        mkdir -p "$(dirname "$path")"
        printf '%s\n' "$content" > "$path"
        chmod "$mode" "$path"
        echo "    + escrito $path"
    else
        echo "    [plano] escreveria $path:"
        printf '%s\n' "$content" | sed 's/^/        | /'
    fi
}

# Troca o serviço de lugar nos testes (contêiner sem systemd)
enable_service() {
    systemctl enable --now "$1" >/dev/null 2>&1 || systemctl restart "$1"
}

detect_os() {
    [[ -r /etc/os-release ]] || fail "não achei /etc/os-release: este script é para Debian ou Ubuntu"
    # shellcheck disable=SC1091
    . /etc/os-release
    case "${ID:-}" in
        debian|ubuntu) OS_ID="$ID" ;;
        *)
            case " ${ID_LIKE:-} " in
                *" ubuntu "*) OS_ID=ubuntu ;;
                *" debian "*) OS_ID=debian ;;
                *) fail "sistema não suportado (${PRETTY_NAME:-desconhecido}): use Debian 12/13 ou Ubuntu 22.04/24.04" ;;
            esac
            ;;
    esac
    CODENAME="${UBUNTU_CODENAME:-${VERSION_CODENAME:-}}"
    [[ -n "$CODENAME" ]] || fail "não consegui descobrir o nome da versão do sistema (VERSION_CODENAME)"
    echo "Sistema: ${PRETTY_NAME:-$OS_ID $CODENAME} (${OS_ID}/${CODENAME}), $(dpkg --print-architecture)"
}

install_base_packages() {
    log "Pacotes: atualização do sistema, nginx, certbot, age, rclone"
    if [[ $APPLY -eq 1 ]]; then
        export DEBIAN_FRONTEND=noninteractive NEEDRESTART_MODE=a
        apt-get update -y
        # A imagem de instalação do provedor pode ser antiga: traz tudo para a versão atual antes de instalar o resto
        apt-get -y -o Dpkg::Options::=--force-confold full-upgrade
        apt-get install -y ca-certificates curl gnupg openssl nginx certbot python3-certbot-nginx age rclone
    else
        echo "    [plano] apt-get update && apt-get -y full-upgrade"
        echo "    [plano] apt-get install -y ca-certificates curl gnupg openssl nginx certbot python3-certbot-nginx age rclone"
    fi
}

add_docker_repo() {
    local keyring=/etc/apt/keyrings/docker.asc list=/etc/apt/sources.list.d/docker.list
    log "Repositório oficial do Docker (${OS_ID} ${CODENAME})"
    if [[ $APPLY -eq 0 ]]; then
        echo "    [plano] baixar https://download.docker.com/linux/${OS_ID}/gpg e conferir a impressão digital ${DOCKER_KEY_FPR}"
        echo "    [plano] escrever ${list} apontando para https://download.docker.com/linux/${OS_ID} ${CODENAME} stable"
        return
    fi

    install -m 0755 -d /etc/apt/keyrings
    local tmp fingerprint
    tmp=$(mktemp)
    curl -fsSL "https://download.docker.com/linux/${OS_ID}/gpg" -o "$tmp" || { rm -f "$tmp"; fail "não consegui baixar a chave do repositório do Docker"; }
    fingerprint=$(gpg --show-keys --with-colons "$tmp" 2>/dev/null | awk -F: '$1 == "fpr" { print $10; exit }')
    if [[ "$fingerprint" != "$DOCKER_KEY_FPR" ]]; then
        rm -f "$tmp"
        fail "a impressão digital da chave do Docker não confere (veio '${fingerprint:-nenhuma}'): não vou instalar de uma origem que não reconheço"
    fi
    install -m 0644 "$tmp" "$keyring"
    rm -f "$tmp"

    echo "deb [arch=$(dpkg --print-architecture) signed-by=${keyring}] https://download.docker.com/linux/${OS_ID} ${CODENAME} stable" > "$list"
    echo "    + escrito $list (chave conferida)"
    apt-get update -y
}

install_docker() {
    if [[ $SKIP_DOCKER -eq 1 ]]; then
        log "Docker: pulado (--skip-docker)"
        return
    fi
    if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
        log "Docker: já instalado ($(docker --version 2>/dev/null))"
        return
    fi

    add_docker_repo
    log "Docker: instalação"
    if [[ $APPLY -eq 1 ]]; then
        export DEBIAN_FRONTEND=noninteractive
        apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
        enable_service docker
    else
        echo "    [plano] apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin"
    fi
}

configure_docker_logs() {
    local config=/etc/docker/daemon.json
    log "Docker: registros limitados (10 MB x 3 por contêiner)"
    if [[ -f "$config" ]]; then
        echo "    $config já existe: não mexo"
        return
    fi
    write_file "$config" 644 <<'EOF'
{
  "log-driver": "json-file",
  "log-opts": { "max-size": "10m", "max-file": "3" }
}
EOF
    if [[ $APPLY -eq 1 ]] && systemctl is-active docker >/dev/null 2>&1; then
        systemctl restart docker
    fi
}

limit_journal() {
    log "Journal do sistema: no máximo 300 MB em disco"
    # O padrão do journald é 10% do disco: 1,5 GB numa VPS de 15 GB
    write_file /etc/systemd/journald.conf.d/monburan.conf 644 <<'EOF'
[Journal]
SystemMaxUse=300M
EOF
    if [[ $APPLY -eq 1 ]]; then
        systemctl restart systemd-journald >/dev/null 2>&1 || true
    fi
}

ensure_swap() {
    if [[ $SKIP_SWAP -eq 1 ]]; then
        log "Swap: pulado (--skip-swap)"
        return
    fi
    local mem_kb
    mem_kb=$(awk '/MemTotal/ { print $2 }' /proc/meminfo)
    if [[ -n "$(swapon --noheadings --show 2>/dev/null)" ]]; then
        log "Swap: já existe ($(swapon --noheadings --show=NAME,SIZE 2>/dev/null | tr '\n' ' '))"
        return
    fi
    if [[ "${mem_kb:-0}" -ge 3000000 ]]; then
        log "Swap: a máquina tem memória de sobra ($((mem_kb / 1024)) MB), não crio"
        return
    fi

    log "Swap: 2 GB em /swapfile (a máquina tem $((mem_kb / 1024)) MB de memória)"
    if [[ $APPLY -eq 1 ]]; then
        fallocate -l 2G /swapfile 2>/dev/null || dd if=/dev/zero of=/swapfile bs=1M count=2048 status=none
        chmod 600 /swapfile
        mkswap /swapfile >/dev/null
        swapon /swapfile
        grep -q '^/swapfile ' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
        echo "    + swap ligado"
    else
        echo "    [plano] fallocate -l 2G /swapfile && chmod 600 && mkswap && swapon && /etc/fstab"
    fi
    write_file /etc/sysctl.d/99-monburan.conf 644 <<'EOF'
# Usa o swap só quando precisa: a memória é do app e do Postgres
vm.swappiness = 10
EOF
}

prepare_directories() {
    log "Pastas: ${INSTALL_DIR} (backups só do dono), /var/www/certbot"
    run mkdir -p "$INSTALL_DIR" "$INSTALL_DIR/backups" /var/www/certbot
    run chmod 755 "$INSTALL_DIR"
    run chmod 700 "$INSTALL_DIR/backups"
    # O site padrão do nginx tem default_server e entraria em conflito com o do Monburan
    if [[ -e /etc/nginx/sites-enabled/default ]]; then
        run rm -f /etc/nginx/sites-enabled/default
    fi
}

summary() {
    log "Próximos passos"
    if [[ $APPLY -eq 1 ]]; then
        echo "    Se o kernel foi atualizado (uname -r diferente do mais novo em /boot), reinicie antes de seguir."
    fi
    cat <<'EOF'
    1. bash harden.sh --apply          (firewall, SSH só por chave, fail2ban; mantenha uma segunda sessão SSH aberta)
    2. copie infra/vps para /opt/monburan
    3. bash deploy.sh --email voce@exemplo.com --admin-email adm@exemplo.com
    4. backup (docs/SEGURANCA.md) e bash validate.sh --ssh root@IP -i CHAVE
EOF
}

main() {
    local arg
    for arg in "$@"; do
        case "$arg" in
            --apply) APPLY=1 ;;
            --skip-docker) SKIP_DOCKER=1 ;;
            --skip-swap) SKIP_SWAP=1 ;;
            -h|--help) usage; exit 0 ;;
            *) echo "Opção desconhecida: $arg" >&2; usage; exit 2 ;;
        esac
    done

    if [[ $APPLY -eq 1 && ${EUID:-$(id -u)} -ne 0 ]]; then
        fail "rode como root (sudo bash provision.sh --apply)"
    fi

    if [[ $APPLY -eq 1 ]]; then
        echo "Preparando o servidor."
    else
        echo "Modo plano: nada será alterado. Use --apply para aplicar."
    fi

    detect_os
    install_base_packages
    install_docker
    configure_docker_logs
    limit_journal
    ensure_swap
    prepare_directories
    summary
}

# Permite carregar as funções nos testes (PROVISION_SOURCE_ONLY=1) sem executar nada
if [[ "${PROVISION_SOURCE_ONLY:-0}" != "1" ]]; then
    main "$@"
fi
