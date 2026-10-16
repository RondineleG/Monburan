# ============================================================
# Monburan — Buildx Bake (build da imagem Docker para o GHCR)
# Uso:
#   docker buildx bake --file docker-bake.hcl            # build local
#   docker buildx bake --file docker-bake.hcl --push     # build + push no GHCR
#
# O push exige docker login ghcr.io com um PAT que tenha
# os escopos write:packages e read:packages.
# ============================================================
variable "IMAGE" {
  default = "ghcr.io/rondineleg/monburan"
}

variable "TAG" {
  default = "latest"
}

group "default" {
  targets = ["app"]
}

target "app" {
  context    = "."
  dockerfile = "src/Monburan.Web/Dockerfile"
  tags       = ["${IMAGE}:${TAG}"]
  cache-from = ["type=gha"]
  cache-to   = ["type=gha,mode=max"]
}
