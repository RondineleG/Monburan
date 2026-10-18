#!/usr/bin/env bash
# ============================================================
# Monburan — Teste de fumaça do nginx.conf
#
# Sobe um nginx descartável (contêiner) com o nginx.conf do repositório, certificados de mentira no lugar dos do certbot e um
# "app" de mentira em 127.0.0.1:8080 que devolve os cabeçalhos que recebeu. Confere que o nginx.conf é válido e que:
#   - nomes desconhecidos (IP, domínios de terceiros) são descartados sem resposta, em HTTP e em HTTPS;
#   - o nome certo redireciona para HTTPS, serve o desafio do certbot e faz proxy (HTTP/2);
#   - um X-Forwarded-For mandado pelo cliente não chega ao app;
#   - o cabeçalho Server não revela a versão e o limite de upload vale.
#
#   bash infra/vps/tests/nginx-smoke.sh
# ============================================================
set -uo pipefail

if [[ "${INSIDE_CONTAINER:-0}" != "1" ]]; then
    repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
    exec docker run --rm -e INSIDE_CONTAINER=1 -v "$repo":/repo:ro --entrypoint sh "${TEST_IMAGE:-nginx:stable-alpine}" \
        -c 'apk add --no-cache bash curl openssl >/dev/null && bash /repo/infra/vps/tests/nginx-smoke.sh'
fi

OUT=/tmp/nginx-smoke.out
HOST=monburan.com.br
passed=0
failed=0

ok() { passed=$((passed + 1)); printf '  ok     %s\n' "$1"; }
bad() {
    failed=$((failed + 1))
    printf '  FALHOU %s\n' "$1"
    tail -8 "$OUT" 2>/dev/null | sed 's/^/           | /'
    tail -5 /var/log/nginx/error.log 2>/dev/null | sed 's/^/           | /'
}

# expect <descrição> <comando...>: o comando precisa terminar com sucesso
expect() {
    local name=$1
    shift
    if "$@" >/dev/null 2>&1; then ok "$name"; else bad "$name"; fi
}

# Certificados e arquivos do certbot de mentira
live=/etc/letsencrypt/live/$HOST
mkdir -p "$live" /var/www/certbot/.well-known/acme-challenge
openssl req -x509 -newkey rsa:2048 -nodes -days 1 -subj "/CN=$HOST" \
    -keyout "$live/privkey.pem" -out "$live/fullchain.pem" >/dev/null 2>&1
openssl dhparam -dsaparam -out /etc/letsencrypt/ssl-dhparams.pem 2048 >/dev/null 2>&1
cat >/etc/letsencrypt/options-ssl-nginx.conf <<'CONF'
ssl_session_cache shared:le_nginx_SSL:10m;
ssl_session_timeout 1440m;
ssl_session_tickets off;
ssl_protocols TLSv1.2 TLSv1.3;
ssl_prefer_server_ciphers off;
CONF
echo "token-do-certbot" >/var/www/certbot/.well-known/acme-challenge/token

# O app de mentira devolve o que recebeu. Os registros do nginx vão para arquivos (na imagem eles apontam para a saída padrão)
rm -f /etc/nginx/conf.d/default.conf /var/log/nginx/access.log /var/log/nginx/error.log
cp /repo/infra/vps/nginx.conf /etc/nginx/conf.d/monburan.conf
cat >/etc/nginx/conf.d/backend.conf <<'CONF'
server {
    listen 127.0.0.1:8080;
    client_max_body_size 50m;
    location / {
        default_type text/plain;
        return 200 "xff=$http_x_forwarded_for|proto=$http_x_forwarded_proto|host=$http_host|realip=$http_x_real_ip\n";
    }
}
CONF

echo "1. Configuração"
nginx -t >"$OUT" 2>&1
expect "nginx -t aceita o nginx.conf" test $? -eq 0
nginx
sleep 1

# Atalhos: o código HTTP (000 = conexão fechada sem resposta) e o corpo
code() { curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$@"; }
body() { curl -s --max-time 5 "$@"; }
secure() { curl -sk --max-time 5 --resolve "$HOST:443:127.0.0.1" "$@"; }

echo
echo "2. Nomes desconhecidos são descartados"
expect "HTTP pelo IP: sem resposta" test "$(code http://127.0.0.1/)" = 000
expect "HTTP com Host de terceiros: sem resposta" test "$(code -H 'Host: evil.example' http://127.0.0.1/)" = 000
expect "HTTPS pelo IP: sem resposta" test "$(code -k https://127.0.0.1/)" = 000
expect "HTTPS com SNI de terceiros: sem resposta" test "$(code -k --resolve evil.example:443:127.0.0.1 https://evil.example/)" = 000

echo
echo "3. O nome certo funciona"
expect "HTTP redireciona para HTTPS (301)" test "$(code -H "Host: $HOST" http://127.0.0.1/pagina?x=1)" = 301
expect "com o endereço certo no Location" bash -c "curl -sI -H 'Host: $HOST' 'http://127.0.0.1/pagina?x=1' | grep -qi '^location: https://$HOST/pagina?x=1'"
expect "www também redireciona" test "$(code -H 'Host: www.monburan.com.br' http://127.0.0.1/)" = 301
expect "o desafio do certbot é servido em HTTP" test "$(body -H "Host: $HOST" http://127.0.0.1/.well-known/acme-challenge/token)" = token-do-certbot
expect "HTTPS faz proxy para o app" bash -c "curl -sk --resolve $HOST:443:127.0.0.1 https://$HOST/ | grep -q 'host=$HOST'"
expect "HTTPS negocia HTTP/2" bash -c "curl -skI --http2 --resolve $HOST:443:127.0.0.1 https://$HOST/ | head -1 | grep -q '^HTTP/2 200'"
expect "o app recebe o protocolo https e o IP do cliente" bash -c "curl -sk --resolve $HOST:443:127.0.0.1 https://$HOST/ | grep -q 'xff=127.0.0.1|proto=https|host=$HOST|realip=127.0.0.1'"

echo
echo "4. Cabeçalhos e limites"
secure -H 'X-Forwarded-For: 6.6.6.6, 7.7.7.7' https://$HOST/ >"$OUT"
expect "X-Forwarded-For do cliente não chega ao app" bash -c "grep -q 'xff=127.0.0.1|' $OUT && ! grep -q '6.6.6.6' $OUT"
secure -I https://$HOST/ >"$OUT"
expect "Server diz só nginx, sem versão" bash -c "grep -qi '^server: nginx\r\?\$' $OUT"
head -c 1000000 /dev/zero | secure -X POST --data-binary @- -o /dev/null -w '%{http_code}' https://$HOST/ >"$OUT"
expect "upload de 1 MB passa" test "$(cat "$OUT")" = 200
head -c 21000000 /dev/zero | secure -X POST --data-binary @- -o /dev/null -w '%{http_code}' https://$HOST/ >"$OUT"
expect "upload de 21 MB é recusado (413)" test "$(cat "$OUT")" = 413

echo
echo "$passed ok, $failed falhas"
[[ $failed -eq 0 ]]
