#!/usr/bin/env bash
# ============================================================
# Monburan — Endurecimento da VPS (Debian/Ubuntu)
#
# Firewall (ufw), SSH só por chave, fail2ban e atualizações automáticas de segurança.
#
# NADA é alterado sem --apply: sem argumentos o script só mostra o plano e as verificações.
# As partes que podem trancar você para fora do servidor (firewall e SSH) são as últimas a rodar e têm travas:
#   - o firewall só liga com as portas do SSH liberadas (lidas do sshd em execução e da sua sessão atual);
#   - o SSH só deixa de aceitar senha se a conta de administração já tem chave autorizada, se você confirma que entra por
#     chave e se a configuração efetiva (sshd -T) realmente ficou sem senha; a configuração é validada (sshd -t) antes de
#     ser recarregada.
# MESMO ASSIM: depois de aplicar, NÃO feche a sessão atual. Abra outra e confirme que entra.
#
# Uso (como root, na VPS):
#   bash harden.sh                        # plano (não altera nada)
#   bash harden.sh --apply                # aplica tudo
#   bash harden.sh --apply --skip-ssh     # aplica sem mexer no SSH
#   bash harden.sh --apply --skip-ufw     # aplica sem mexer no firewall
#   bash harden.sh --apply --yes          # sem a pergunta de confirmação do SSH (automação)
#
# Variáveis opcionais:
#   SSH_PORT           porta(s) do SSH, separadas por vírgula (padrão: as que o sshd escuta agora)
#   ADMIN_USER         conta cuja chave autorizada é conferida (padrão: $SUDO_USER, ou root)
#   INSTALL_DIR        pasta do Monburan (padrão: /opt/monburan), para ajustar as permissões dos segredos
#   FAIL2BAN_IGNOREIP  endereços que o fail2ban nunca bloqueia, além do loopback (ex.: o IP fixo do escritório)
# ============================================================
set -euo pipefail

# sshd, ufw e afins ficam em /usr/sbin, que não está no PATH de quem não é root (o plano roda sem root)
PATH="$PATH:/usr/local/sbin:/usr/sbin:/sbin"

APPLY=0
SKIP_SSH=0
SKIP_UFW=0
ASSUME_YES=0
INSTALL_DIR="${INSTALL_DIR:-/opt/monburan}"

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

# Recarrega um serviço; separado para os testes poderem trocá-lo (contêiner sem systemd)
reload_service() {
    systemctl reload "$1" 2>/dev/null || systemctl restart "$1"
}

# Portas em que o SSH atende: as informadas em SSH_PORT, as que o sshd escuta agora (ss e sshd -T) e a desta sessão
# (SSH_CONNECTION). Liberar todas evita se trancar fora quando o SSH está fora da 22 ou em mais de uma porta.
detect_ssh_ports() {
    local ports=""
    if [[ -n "${SSH_PORT:-}" ]]; then
        ports="${SSH_PORT//,/ }"
    else
        if command -v ss >/dev/null 2>&1; then
            ports+=" $(ss -H -ltnp 2>/dev/null | awk '/"sshd"/ { n = split($4, a, ":"); print a[n] }' || true)"
        fi
        if command -v sshd >/dev/null 2>&1; then
            ports+=" $(sshd -T 2>/dev/null | awk '/^port /{print $2}' || true)"
        fi
    fi
    if [[ -n "${SSH_CONNECTION:-}" ]]; then
        ports+=" ${SSH_CONNECTION##* }"
    fi

    local list=()
    local port
    for port in $ports; do
        if [[ "$port" =~ ^[0-9]+$ ]]; then
            list+=("$port")
        fi
    done
    if [[ ${#list[@]} -eq 0 ]]; then
        echo 22
        return
    fi
    printf '%s\n' "${list[@]}" | sort -un | tr '\n' ' ' | sed 's/ $//'
}

admin_home() {
    local user=${ADMIN_USER:-${SUDO_USER:-root}}
    getent passwd "$user" | cut -d: -f6
}

install_packages() {
    log "Pacotes: ufw, fail2ban, unattended-upgrades"
    if [[ $APPLY -eq 1 ]]; then
        export DEBIAN_FRONTEND=noninteractive
        apt-get update -y
        # python3-systemd: o fail2ban lê os registros do SSH direto do journal
        apt-get install -y ufw fail2ban unattended-upgrades python3-systemd
    else
        echo "    [plano] apt-get install -y ufw fail2ban unattended-upgrades python3-systemd"
    fi
}

configure_fail2ban() {
    local ports=${1// /,}
    local ignore_ips="127.0.0.1/8 ::1${FAIL2BAN_IGNOREIP:+ $FAIL2BAN_IGNOREIP}"
    local config=/etc/fail2ban/jail.d/monburan.local
    log "fail2ban: bloqueia por 1 hora quem erra o SSH 4 vezes em 10 minutos"
    write_file "$config" 644 <<EOF
# Gerado por infra/vps/harden.sh (Monburan)
[DEFAULT]
bantime  = 1h
findtime = 10m
maxretry = 4
ignoreip = ${ignore_ips}

[sshd]
enabled = true
port    = ${ports}
backend = systemd
EOF

    if [[ $APPLY -eq 1 ]]; then
        if ! fail2ban-client -t >/dev/null 2>&1; then
            rm -f "$config"
            fail "fail2ban-client -t recusou a configuração; o arquivo foi removido e nada foi recarregado"
        fi
        systemctl enable fail2ban >/dev/null 2>&1 || true
        reload_service fail2ban
    else
        echo "    [plano] fail2ban-client -t && systemctl enable fail2ban && systemctl reload fail2ban"
    fi
}

configure_unattended_upgrades() {
    log "Atualizações automáticas de segurança (sem reinício automático)"
    write_file /etc/apt/apt.conf.d/20auto-upgrades 644 <<'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
APT::Periodic::AutocleanInterval "7";
EOF
    write_file /etc/apt/apt.conf.d/52monburan-unattended-upgrades 644 <<'EOF'
// Gerado por infra/vps/harden.sh (Monburan). As origens de segurança já vêm no padrão da distribuição.
Unattended-Upgrade::Remove-Unused-Dependencies "true";
// Sem reinício automático: o reinício derruba o site. Atualizações de kernel pedem reinício manual (veja /var/run/reboot-required).
Unattended-Upgrade::Automatic-Reboot "false";
EOF
}

fix_secret_permissions() {
    log "Permissões dos segredos em ${INSTALL_DIR}"
    local file
    for file in .env ai-keys.env backup.env; do
        if [[ -e "$INSTALL_DIR/$file" ]]; then
            run chmod 600 "$INSTALL_DIR/$file"
        fi
    done
    if [[ -d "$INSTALL_DIR/backups" ]]; then
        run chmod 700 "$INSTALL_DIR/backups"
    fi
}

configure_ufw() {
    local ports=$1
    local port_list=()
    read -r -a port_list <<<"$ports"
    log "Firewall (ufw): só ${ports// /, }/tcp (SSH), 80/tcp e 443/tcp"
    [[ ${#port_list[@]} -gt 0 ]] || fail "porta do SSH não identificada; defina SSH_PORT"

    # Sem IPv6 no ufw, o tráfego IPv6 ficaria sem filtro nenhum
    if [[ -f /etc/default/ufw ]]; then
        run sed -i 's/^IPV6=no/IPV6=yes/' /etc/default/ufw
    fi
    run ufw default deny incoming
    run ufw default allow outgoing
    local port
    for port in "${port_list[@]}"; do
        # "limit" já barra quem abre conexões demais por minuto no SSH
        run ufw limit "${port}/tcp" comment 'SSH'
    done
    run ufw allow 80/tcp comment 'HTTP (nginx, redireciona para HTTPS)'
    run ufw allow 443/tcp comment 'HTTPS (nginx)'
    run ufw --force enable

    echo "    Lembrete: o Docker publica portas pelo iptables e IGNORA o ufw. O compose do Monburan publica o app só em"
    echo "    127.0.0.1:8080 e não publica o Postgres; qualquer porta nova ligada a 0.0.0.0 ficaria exposta. A verificação"
    echo "    final lista o que escuta fora do loopback."
}

check_ssh_key_for_admin() {
    local home keys
    home=$(admin_home)
    [[ -n "$home" ]] || fail "conta ${ADMIN_USER:-${SUDO_USER:-root}} não encontrada"
    keys="$home/.ssh/authorized_keys"

    # Ignora comentários e aceita opções antes do tipo da chave (command="...", from="...")
    if [[ ! -s "$keys" ]] \
        || ! grep -Ev '^[[:space:]]*#' "$keys" \
        | grep -Eq '(^|[[:space:]])(ssh-(ed25519|rsa)|ecdsa-sha2-[a-z0-9-]+|sk-[a-z0-9@.-]+)[[:space:]]+[A-Za-z0-9+/=]{20,}'; then
        fail "nenhuma chave SSH autorizada em $keys. Desligar a senha agora trancaria você para fora. Instale sua chave (ssh-copy-id) e rode de novo, ou use --skip-ssh."
    fi
}

# Ter a chave em authorized_keys não prova que você a tem em mãos: pergunta antes de desligar a senha
confirm_key_login() {
    if [[ $ASSUME_YES -eq 1 ]]; then
        return 0
    fi
    if [[ ! -t 0 ]]; then
        fail "sem terminal para confirmar. Teste a entrada por chave e rode de novo com --yes."
    fi
    local answer
    read -r -p "    Você já entrou nesta VPS usando a chave SSH (sem digitar senha)? Digite 'sim' para continuar: " answer
    [[ "$answer" == "sim" ]] || fail "abortado. Teste a entrada por chave (ssh -o PreferredAuthentications=publickey ...) e rode de novo."
}

configure_ssh() {
    log "SSH: só por chave, sem senha"
    if ! command -v sshd >/dev/null 2>&1; then
        if [[ $APPLY -eq 1 ]]; then
            fail "sshd não está instalado"
        fi
        warn "sshd não está instalado nesta máquina; o plano abaixo vale para a VPS."
    fi

    if [[ $APPLY -eq 1 ]]; then
        check_ssh_key_for_admin
        confirm_key_login
    else
        echo "    [plano] conferiria que ${ADMIN_USER:-${SUDO_USER:-root}} tem chave em ~/.ssh/authorized_keys e pediria a sua confirmação"
    fi

    if ! grep -Eqs '^\s*Include\s+/etc/ssh/sshd_config\.d/' /etc/ssh/sshd_config; then
        warn "/etc/ssh/sshd_config não inclui sshd_config.d: a configuração abaixo não seria lida. Adicione 'Include /etc/ssh/sshd_config.d/*.conf' no topo e rode de novo."
        if [[ $APPLY -eq 1 ]]; then
            fail "abortado para não deixar a configuração pela metade"
        fi
    fi

    # Prefixo 00: o sshd vale o PRIMEIRO valor que lê, em ordem alfabética, e imagens de nuvem trazem um
    # 50-cloud-init.conf com "PasswordAuthentication yes": um arquivo 99-... seria ignorado
    local dropin=/etc/ssh/sshd_config.d/00-monburan-hardening.conf
    write_file "$dropin" 644 <<'EOF'
# Gerado por infra/vps/harden.sh (Monburan). Para desfazer: apague este arquivo e recarregue o sshd.
PasswordAuthentication no
KbdInteractiveAuthentication no
PermitEmptyPasswords no
PubkeyAuthentication yes
PermitRootLogin prohibit-password
LoginGraceTime 30
X11Forwarding no
ClientAliveInterval 300
ClientAliveCountMax 2
EOF

    if [[ $APPLY -eq 1 ]]; then
        if ! sshd -t; then
            rm -f "$dropin"
            fail "sshd -t recusou a configuração; o arquivo foi removido e nada foi recarregado"
        fi
        local effective
        effective=$(sshd -T 2>/dev/null) || effective=""
        if ! grep -qx 'passwordauthentication no' <<<"$effective"; then
            rm -f "$dropin"
            fail "outra configuração vence a deste script e a senha continuaria aceita (veja: grep -rniE 'passwordauthentication' /etc/ssh/sshd_config /etc/ssh/sshd_config.d/). O arquivo foi removido e nada foi recarregado."
        fi
        reload_service ssh || reload_service sshd
    else
        echo "    [plano] sshd -t && sshd -T | grep 'passwordauthentication no' && systemctl reload ssh"
    fi
}

verify() {
    log "Verificação"
    echo "    Portas que escutam fora do loopback (devem ser só SSH, 80 e 443):"
    if command -v ss >/dev/null 2>&1; then
        ss -ltnH 2>/dev/null | awk '{print $4}' | grep -Ev '^(127\.|\[::1\]|::1)' | sort -u | sed 's/^/        /' || true
    fi

    if [[ $APPLY -eq 1 ]]; then
        echo
        if command -v ufw >/dev/null 2>&1; then
            ufw status verbose 2>&1 | sed 's/^/    /' || true
        fi
        if command -v fail2ban-client >/dev/null 2>&1; then
            fail2ban-client status sshd 2>&1 | sed 's/^/    /' || true
        fi
        if command -v sshd >/dev/null 2>&1; then
            sshd -T 2>/dev/null | grep -E '^(passwordauthentication|kbdinteractiveauthentication|permitrootlogin) ' | sed 's/^/    sshd: /' || true
        fi
        if [[ -f /var/run/reboot-required ]]; then
            warn "há atualização pendente que pede reinício (/var/run/reboot-required)"
        fi
        if command -v curl >/dev/null 2>&1; then
            if curl -fsS -m 5 -o /dev/null http://127.0.0.1:8080/health 2>/dev/null; then
                echo "    app: responde em http://127.0.0.1:8080/health"
            else
                warn "o app não respondeu em http://127.0.0.1:8080/health (confira: docker ps; docker logs monburan-app)"
            fi
        fi
    else
        cat <<'EOF'

    Confira depois de aplicar:
      ufw status verbose
      fail2ban-client status sshd
      sshd -T | grep -E '^(passwordauthentication|permitrootlogin)'
      systemctl status unattended-upgrades --no-pager
      test -f /var/run/reboot-required && echo "reinício pendente"
EOF
    fi
}

main() {
    for arg in "$@"; do
        case "$arg" in
            --apply) APPLY=1 ;;
            --dry-run) APPLY=0 ;;
            --skip-ssh) SKIP_SSH=1 ;;
            --skip-ufw) SKIP_UFW=1 ;;
            --yes) ASSUME_YES=1 ;;
            -h|--help) usage; exit 0 ;;
            *) echo "Opção desconhecida: $arg" >&2; usage; exit 2 ;;
        esac
    done

    if [[ $APPLY -eq 1 && ${EUID:-$(id -u)} -ne 0 ]]; then
        fail "rode como root (sudo bash harden.sh --apply)"
    fi

    if [[ $APPLY -eq 1 ]]; then
        echo "Aplicando o endurecimento. Mantenha esta sessão aberta até confirmar o acesso por outra."
    else
        echo "Modo plano: nada será alterado. Use --apply para aplicar."
    fi

    local ssh_ports
    ssh_ports=$(detect_ssh_ports)
    echo "Porta(s) do SSH detectada(s): ${ssh_ports}"

    # O que pode trancar você para fora (firewall e SSH) fica por último
    install_packages
    configure_fail2ban "$ssh_ports"
    configure_unattended_upgrades
    fix_secret_permissions
    [[ $SKIP_UFW -eq 1 ]] || configure_ufw "$ssh_ports"
    [[ $SKIP_SSH -eq 1 ]] || configure_ssh
    verify

    if [[ $APPLY -eq 1 && $SKIP_SSH -eq 0 ]]; then
        echo
        echo "    >>> NÃO feche esta sessão. Abra OUTRA e confirme que entra por chave antes de sair. <<<"
    fi
}

# Permite carregar as funções nos testes (HARDEN_SOURCE_ONLY=1) sem executar nada
if [[ "${HARDEN_SOURCE_ONLY:-0}" != "1" ]]; then
    main "$@"
fi
