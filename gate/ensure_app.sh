#!/usr/bin/env bash
# Garante o Monburan no ar em http://localhost:5095 para a suite E2E.
set -e
if curl -sf -o /dev/null -m 5 http://localhost:5095/health; then
  echo "app ja no ar"
  exit 0
fi
export PATH="/c/Users/Administrator/dotnet:$PATH"
export DOTNET_ROOT="C:\\Users\\Administrator\\dotnet"
export ASPNETCORE_ENVIRONMENT=Development
cd "$(dirname "$0")/.."
mkdir -p reports
nohup dotnet run --project src/Monburan.Web --urls http://localhost:5095 > reports/app.log 2>&1 &
echo "subindo app (pid $!)..."
for i in $(seq 1 60); do
  sleep 2
  if curl -sf -o /dev/null -m 3 http://localhost:5095/health; then
    echo "app no ar"
    exit 0
  fi
done
echo "app nao respondeu em 120s" >&2
exit 1
