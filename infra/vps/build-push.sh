#!/usr/bin/env bash
# ============================================================
# Monburan — Build + Push da imagem Docker para o GHCR (na VPS)
#
# Motivo: o GitHub Actions está travado por billing; então a VPS
# mesma constrói a imagem e publica direto no GHCR.
#
# Pré-requisitos (na VPS):
#   1. Instalar gh (opcional) e git
#   2. Exportar um PAT do GitHub com escopos write:packages e read:packages:
#        export GHCR_TOKEN=ghp_xxx
#
# Uso:
#   cd /opt/monburan
#   export GHCR_TOKEN=ghp_xxxxxxxx
#   bash build-push.sh
# ============================================================
set -euo pipefail

REPO="https://github.com/RondineleG/Monburan.git"
SRC_DIR="/opt/monburan/src"
IMAGE="ghcr.io/ronelineg/monburan"
TAG="${TAG:-latest}"

if [[ -z "${GHCR_TOKEN:-}" ]]; then
    echo "ERRO: defina GHCR_TOKEN com um PAT do GitHub (write:packages, read:packages)." >&2
    echo "  export GHCR_TOKEN=ghp_xxxxxxxx" >&2
    exit 1
fi

echo "==> [1/4] Login no GHCR"
echo "$GHCR_TOKEN" | docker login ghcr.io -u "RondineleG" --password-stdin

echo "==> [2/4] Sincronizando código"
mkdir -p "$SRC_DIR"
if [[ ! -d "$SRC_DIR/.git" ]]; then
    git clone "$REPO" "$SRC_DIR"
else
    git -C "$SRC_DIR" pull --ff-only
fi

echo "==> [3/4] Build + push da imagem ($IMAGE:$TAG)"
cd "$SRC_DIR"
docker buildx bake --file infra/vps/docker-bake.hcl --push

echo "==> [4/4] Concluído"
docker images "$IMAGE" --format "table {{.Repository}}:{{.Tag}}\t{{.Size}}"
