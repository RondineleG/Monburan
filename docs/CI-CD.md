# Esteira de CI/CD

## Workflows

| Workflow | Quando roda | O que faz |
|---|---|---|
| `ci-cd.yml` | push e PR em `main`/`develop`, manual | build Release da solução (inclui o app Android), testes unitários, de integração e de API com coverage, **E2E** (Playwright contra o app em Development + Postgres de serviço, com 1 retentativa), scan de segredos, auditoria de pacotes NuGet vulneráveis e `dotnet publish` da web como artefato |
| `docker-ghcr.yml` | push na `main` com mudança em `src/**`, manual | constrói a imagem Docker e publica em `ghcr.io/rondineleg/monburan` (`latest` + tag com o sha do commit) |
| `android-release.yml` | push na `main` com mudança no app/Core, PR para a `main`, manual | compila, assina e confere o APK; na `main`, publica a release `v<versão>` com o APK |

Os jobs `deploy-staging` e `deploy-production` (Azure Web App) só rodam quando existe a variável de repositório
`AZURE_RESOURCE_GROUP` (mais os secrets do Azure). A produção atual é a VPS (abaixo), então eles ficam ignorados.

## Lançar uma nova versão do app (APK)

1. No PR, suba `ApplicationDisplayVersion` (nome, ex.: `1.4.0`) e `ApplicationVersion` (versionCode, inteiro que só
   cresce) em `src/Monburan.Mobile/Monburan.Mobile.csproj`.
2. O PR roda o `android-release.yml` em modo ensaio: compila, assina, confere versão e assinatura, mostra as notas no
   resumo do run e deixa o APK como artefato. Nada é publicado.
3. Ao mesclar na `main`, o workflow repete o processo e cria a release `v<versão>` com `Monburan-v<versão>.apk`, o
   SHA-256 do APK e o certificado de assinatura. Se a tag já existir, só gera o artefato (não republica).

O APK de release usa `https://monburan.com.br/` como API. Por isso, **publique a web antes de distribuir o APK**.

**Secrets necessários** (Settings → Secrets and variables → Actions): `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEY_ALIAS`,
`ANDROID_KEYSTORE_PASSWORD` e `ANDROID_KEY_PASSWORD`. Sem eles a publicação na `main` falha de propósito: um APK
assinado com a chave de debug do runner muda a cada execução e ninguém conseguiria atualizar por cima. Para criar a
keystore:

```bash
keytool -genkeypair -v -keystore monburan.keystore -alias monburan -keyalg RSA -keysize 2048 -validity 10000
base64 -w0 monburan.keystore    # valor de ANDROID_KEYSTORE_BASE64
```

Guarde a keystore e as senhas fora do repositório: se forem perdidas, não é mais possível lançar atualizações que
instalem por cima do app já instalado. As primeiras releases (até a v1.3.0 manual) foram assinadas com a chave de
debug de um computador; se a keystore da esteira for outra, o workflow detecta e avisa nas notas da release que é
preciso desinstalar a versão anterior uma vez.

## Publicar a web (VPS)

A imagem `latest` é construída e publicada pelo `docker-ghcr.yml` a cada push na `main`.

> **Pré-requisito (uma vez):** o pacote `monburan` do GHCR foi criado manualmente, então o `GITHUB_TOKEN` do workflow
> só consegue publicar nele se o repositório tiver acesso de escrita. Abra
> <https://github.com/users/RondineleG/packages/container/monburan/settings>, em *Manage Actions access* use
> *Add Repository*, escolha `Monburan` e o papel **Write**. Sem isso o passo "Build and push" falha com
> `denied: permission_denied: write_package` (visto em 17/10/2026; o build da imagem em si passa no runner).
> Para conferir depois de ajustar: `gh workflow run docker-ghcr.yml --ref develop` publica só a tag do sha, porque o
> `latest` só é movido na `main`.

A VPS baixa a imagem manualmente (não há deploy automático por SSH, que exigiria secrets de acesso ao servidor):

```bash
cd /opt/monburan
docker tag ghcr.io/rondineleg/monburan:latest ghcr.io/rondineleg/monburan:rollback-$(date +%Y%m%d)   # imagem para voltar atrás
docker exec monburan-postgres pg_dump -U "$(docker exec monburan-postgres printenv POSTGRES_USER)" \
  -d "$(docker exec monburan-postgres printenv POSTGRES_DB)" | gzip > backups/monburan-$(date +%Y%m%d-%H%M).sql.gz
docker compose pull app && docker compose up -d app    # as migrations rodam no boot (APPLY_MIGRATIONS=true)
curl -s http://127.0.0.1:8080/health
```

Para voltar à versão anterior: `docker tag ghcr.io/rondineleg/monburan:rollback-<data> ghcr.io/rondineleg/monburan:latest && docker compose up -d app`.

Atenção: o `infra/vps/build-push.sh` constrói a imagem a partir da `main` da VPS; rodá-lo antes de mesclar um PR
republica o `latest` com o código antigo.

## Testes

- **Unitários, integração e API** rodam no job `build-and-test` (EF InMemory, sem dependências externas).
- **E2E** (`tests/Monburan.E2ETests`) precisa do app no ar com Postgres e dos usuários do seed de Development. Local:

  ```bash
  ASPNETCORE_ENVIRONMENT=Development APPLY_MIGRATIONS=true dotnet run --project src/Monburan.Web --urls http://localhost:5095
  dotnet test tests/Monburan.E2ETests          # E2E_BASE_URL muda a URL do app
  ```

  A suíte tem uma falha intermitente conhecida (`FullSweepTests.Sweep_Advertiser_AllRoutes`, cerca de 1 em 6 execuções);
  por isso o job repete a execução uma vez antes de reprovar.
- **Segurança**: o job `security-scan` procura formatos conhecidos de chave/token nos arquivos versionados e reprova se
  `dotnet list package --vulnerable --include-transitive` apontar algum pacote com vulnerabilidade.
