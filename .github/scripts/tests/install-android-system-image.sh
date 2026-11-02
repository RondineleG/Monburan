#!/usr/bin/env bash
set -euo pipefail

script="$(cd "$(dirname "$0")/.." && pwd)/install-android-system-image.sh"
fixture="$(mktemp -d)"
trap 'rm -rf "$fixture"' EXIT
mkdir -p "$fixture/bin" "$fixture/sdk"

cat > "$fixture/bin/sdkmanager" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
[[ "$*" == '--install system-images;android-34;google_apis;x86_64 --channel=0' ]] || exit 90
count=0
[[ ! -f "$ANDROID_HOME/count" ]] || read -r count < "$ANDROID_HOME/count"
count=$((count + 1))
echo "$count" > "$ANDROID_HOME/count"
if [[ "$count" -gt 1 && -e "$ANDROID_HOME/.temp/partial.zip" ]]; then
  echo 'Download parcial não foi removido' >&2
  exit 91
fi
if [[ "$count" -le "$FAILURES" ]]; then
  mkdir -p "$ANDROID_HOME/.temp"
  touch "$ANDROID_HOME/.temp/partial.zip"
  exit 1
fi
EOF
cat > "$fixture/bin/timeout" <<'EOF'
#!/usr/bin/env bash
[[ "$1" == 12m ]] || exit 92
shift
exec "$@"
EOF
printf '#!/usr/bin/env bash\nexit 0\n' > "$fixture/bin/sleep"
chmod +x "$fixture/bin/"*

for failures in 0 1 3; do
  rm -f "$fixture/sdk/count" "$fixture/sdk/.temp/partial.zip"
  result=0
  PATH="$fixture/bin:$PATH" ANDROID_HOME="$fixture/sdk" FAILURES="$failures" \
    bash "$script" > "$fixture/output" 2>&1 || result=$?
  expected_attempts=$((failures + 1))
  expected_result=0
  if [[ "$failures" == 3 ]]; then
    expected_attempts=3
    expected_result=1
  fi
  read -r attempts < "$fixture/sdk/count"
  if [[ "$result" != "$expected_result" || "$attempts" != "$expected_attempts" ]]; then
    cat "$fixture/output"
    echo "FAIL: failures=$failures (exit=$result, attempts=$attempts)" >&2
    exit 1
  fi
  echo "PASS: failures=$failures, attempts=$attempts, exit=$result"
done
