#!/usr/bin/env bash
set -euo pipefail

workflow=docker-ghcr.yml

# O run do servidor pode demorar alguns minutos para aparecer
run_id=""
for _ in $(seq 1 30); do
  run_id="$(gh run list --workflow "$workflow" --commit "$GITHUB_SHA" --branch main --limit 1 --json databaseId --jq '.[0].databaseId // empty')"
  [ -n "$run_id" ] && break
  sleep 10
done
if [ -z "$run_id" ]; then
  echo "::error::Nenhum deploy de $workflow para $GITHUB_SHA. Execute o workflow do servidor na main para este commit e tente publicar o APK novamente."
  exit 1
fi
echo "Aguardando o run $run_id de $workflow"

status=""
for _ in $(seq 1 105); do
  status="$(gh run view "$run_id" --json status --jq .status)"
  [ "$status" = "completed" ] && break
  sleep 20
done
if [ "$status" != "completed" ]; then
  echo "::error::O deploy do servidor não terminou em 35 minutos; a release não foi publicada. Quando o deploy terminar, rode este workflow de novo (workflow_dispatch)."
  exit 1
fi

deploy="$(gh run view "$run_id" --json jobs --jq '.jobs[] | select(.name == "deploy-vps") | .conclusion')"
if [ "$deploy" != "success" ]; then
  echo "::error::O deploy do servidor terminou como '${deploy:-sem job deploy-vps}'; a release não foi publicada para não apontar o app novo para um servidor antigo. Corrija o deploy e rode este workflow de novo (workflow_dispatch)."
  exit 1
fi
echo "Servidor atualizado: a release pode sair."
