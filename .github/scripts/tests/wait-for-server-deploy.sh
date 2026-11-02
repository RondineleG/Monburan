#!/usr/bin/env bash
set -euo pipefail

script="$(cd "$(dirname "$0")/.." && pwd)/wait-for-server-deploy.sh"
fixture="$(mktemp -d)"
trap 'rm -rf "$fixture"' EXIT

cat > "$fixture/gh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
if [[ "$*" == *"--event"* || "$*" == *"--event="* ]]; then
  echo "A consulta não pode ignorar deploys manuais" >&2
  exit 90
fi
if [[ "$1 $2" == "run list" ]]; then
  [[ "$*" == *"--commit review-sha"* && "$*" == *"--branch main"* ]] || exit 91
  [[ "$REVIEW_CASE" == missing ]] || echo 123
elif [[ "$*" == *"--json status"* ]]; then
  if [[ "$REVIEW_CASE" == timeout ]]; then echo in_progress; else echo completed; fi
elif [[ "$*" == *"--json jobs"* ]]; then
  case "$REVIEW_CASE" in
    success) echo success ;;
    failed) echo failure ;;
    skipped) echo skipped ;;
    no-job) : ;;
  esac
else
  exit 92
fi
EOF
printf '#!/usr/bin/env bash\nexit 0\n' > "$fixture/sleep"
chmod +x "$fixture/gh" "$fixture/sleep"

for scenario in success failed skipped no-job missing timeout; do
  result=0
  PATH="$fixture:$PATH" REVIEW_CASE="$scenario" GITHUB_SHA=review-sha \
    bash "$script" > "$fixture/output" 2>&1 || result=$?
  if [[ "$scenario" == success && "$result" == 0 ]] || [[ "$scenario" != success && "$result" == 1 ]]; then
    echo "PASS: $scenario"
  else
    cat "$fixture/output"
    echo "FAIL: $scenario (exit $result)" >&2
    exit 1
  fi
done
