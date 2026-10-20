# Esteira de CI/CD

## Workflows

| Workflow | Quando roda | O que faz |
|---|---|---|
| `ci-cd.yml` | push e PR em `main`/`dev`, manual | build Release da solução (inclui o app Android), testes unitários, de integração e de API com coverage, **E2E** (Playwright contra o app em Development + Postgres de serviço, com 1 retentativa), scan de segredos, auditoria de pacotes NuGet vulneráveis e `dotnet publish` da web como artefato |
| `docker-ghcr.yml` | push na `main` com mudança em `src/**` ou `infra/vps/**`, rebuild semanal, manual | constrói a imagem Docker (baixando as imagens base de novo) e, na `main`, publica em `ghcr.io/rondineleg/monburan` (`latest` + tag com o sha do commit) e faz o deploy na VPS (job `deploy-vps`, environment `prod`, que só aceita a `main`); em disparo manual de outra branch só constrói, sem enviar |
| `rebuild-semanal.yml` | toda segunda às 03:00 de Brasília, manual | dispara o `docker-ghcr.yml` na `main` para a imagem receber os patches das imagens base (.NET e Postgres) mesmo sem mudança no código. Roda na branch padrão (`dev`), mas não constrói nada a partir dela |
| `android-release.yml` | push na `main` com mudança no app/Core, PR para a `main`, manual | compila, assina e confere o APK; na `main`, publica a release `v<versão>` com o APK |
| `ios-build.yml` | push na `main` com mudança no app/Core, PR para a `main`, manual | em macOS: compila o app para o simulador, abre e tira print; gera o `.ipa` sem assinatura (anexado à release na `main`) |
| `codeql.yml` | push e PR em `main`/`dev`, toda segunda-feira, manual | análise estática de segurança (C#, JavaScript e os próprios workflows); alertas em *Security → Code scanning*. Não publica nada |
| `main-only-from-dev.yml` | PR para a `main` | reprova o PR que não vem da `dev` (a `main` só recebe a promoção); o nome da branch chega por variável de ambiente, nunca interpolado no script. Não publica nada |
| `infra-scripts.yml` | push e PR em `main`/`dev` que mudam `infra/vps/**`, manual | ShellCheck e testes de verdade, em contêineres, do endurecimento da VPS, do backup com restauração e do `nginx.conf` (`infra/vps/tests`). Não publica nada |

Os jobs `deploy-staging` e `deploy-production` (Azure Web App) só rodam quando existe a variável de repositório
`AZURE_RESOURCE_GROUP` (mais os secrets do Azure). A produção atual é a VPS (abaixo), então eles ficam ignorados.

## Lançar uma nova versão do app (APK)

1. Num PR para a `dev`, suba `ApplicationDisplayVersion` (nome, ex.: `1.5.0`) e `ApplicationVersion` (versionCode,
   inteiro que só cresce) em `src/Monburan.Mobile/Monburan.Mobile.csproj`.
2. O PR de promoção `dev` → `main` roda o `android-release.yml` em modo ensaio (os PRs para a `dev` não rodam o
   workflow): compila, assina, confere versão e assinatura, mostra as notas no resumo do run e deixa o APK como
   artefato. Nada é publicado.
3. Ao mesclar na `main`, o workflow repete o processo e cria a release `v<versão>` com `Monburan-v<versão>.apk`, o
   SHA-256 do APK e o certificado de assinatura. Se a tag já existir, só gera o artefato (não republica). Enquanto a
   `dev` não é promovida, a versão publicada continua a antiga.

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

## iOS (sem conta Apple Developer)

O iOS só compila em macOS, então o `ios-build.yml` usa runner `macos-latest` (grátis em repositório público). O projeto
declara o alvo `net10.0-ios26.0` apenas quando o build roda em macOS; no Linux, no Windows e no APK nada muda.

O alvo é o SDK do iOS 26 do .NET (pacote 26.0): o workload do .NET 10 traz também o do iOS 27, que exige o Xcode 27,
ainda ausente dos runners do GitHub. Para mirar o 27, troque `IosSdkVersion` no csproj e `IOS_SDK` no `ios-build.yml`.
A esteira usa `-p:IosOnly=true` para o app declarar só o alvo iOS e não exigir o workload do Android no macOS.

**Xcode do runner.** O compilador de assets do Xcode (`actool`) precisa de um runtime de simulador da mesma versão do
iOS que o SDK do Xcode em uso, e o runner só guarda runtimes do 26.2 em diante. O passo "Selecionar o Xcode" escolhe o
Xcode mais antigo, a partir do SDK alvo, que tenha esse runtime (hoje o 26.2). Como o pacote 26.0 do .NET pede o
Xcode 26.0, os builds passam `-p:ValidateXcodeVersion=false`, que desliga só essa checagem de versão.

**Keychain.** `Platforms/iOS/Entitlements.plist` (grupo de Keychain do SecureStorage) só entra no build com
`-p:IosKeychain=true`, porque exige uma identidade de assinatura, que a esteira não tem. Fica para quando houver conta
Apple.

Sem conta Apple Developer não há assinatura, TestFlight nem App Store. O que a esteira entrega:

- **Job `simulator`:** compila em Release para o simulador, dá ao app uma assinatura ad hoc (o simulador em Mac arm64
  só executa binário assinado; ela não usa certificado nem identifica ninguém), abre no iPhone do simulador, espera
  25 s, confirma que o app continua em execução e guarda o print (`ios-simulador-print`) como artefato. O app de
  Release lê a lista de vagas de `https://monburan.com.br/` (leitura pública). Se o app cair, o log traz o resumo do
  relatório de crash e o diagnóstico vai como artefato (`ios-simulador-diagnostico`).
- **Job `ipa`:** compila para aparelho (`ios-arm64`) sem assinar, monta o `.ipa` (um zip com o `.app` em `Payload/`) e
  confere identificador (`com.monburan.app`), versão e build contra o csproj e a arquitetura (só `arm64`), e avisa se o
  pacote vier assinado.
  Em PR vira artefato do run; na `main` é anexado à release `v<versão>` (criada pelo `android-release.yml`, que o job
  espera existir), com as instruções de instalação nas notas. Como no Android, o que já foi anexado a uma versão não
  é trocado: para publicar outro `.ipa`, suba a versão no csproj.

Cada execução leva cerca de 20 minutos (os dois jobs rodam em paralelo; só o build do simulador gasta cerca de 14).

**Instalar o `.ipa` sem assinatura:** o arquivo não instala direto. Use AltStore ou Sideloadly com um Apple ID gratuito,
que assinam na hora. O app exige iOS 16.4 ou superior (requisito do Blazor Hybrid do MAUI; o job `ipa` falha se o
mínimo do pacote ficar abaixo disso). Limites do Apple ID gratuito: a assinatura vale 7 dias (é preciso renovar), no
máximo 3 apps ativos e o iPhone precisa estar com o Modo Desenvolvedor ligado. Serve para você e poucos testadores, não
para o público.

**Quando houver conta Apple Developer** (US$ 99/ano), a próxima fase é assinar na esteira e enviar ao TestFlight. Seriam
necessários: certificado de distribuição (`.p12` e senha), perfil de provisionamento do `com.monburan.app` e a chave da
API do App Store Connect (Issuer ID, Key ID e `.p8`), além de um número de build que cresça a cada envio (a Apple
recusa repetidos). Para a App Store, faltam ainda a exclusão de conta dentro do app, o manifesto de privacidade
(`PrivacyInfo.xcprivacy`), ícones e capturas de tela.

## Publicar a web (VPS)

A imagem `latest` é construída e publicada pelo `docker-ghcr.yml` a cada push na `main`.

> **Pré-requisito (uma vez):** o pacote `monburan` do GHCR foi criado manualmente, então o `GITHUB_TOKEN` do workflow
> só consegue publicar nele se o repositório tiver acesso de escrita. Abra
> <https://github.com/users/RondineleG/packages/container/monburan/settings>, em *Manage Actions access* use
> *Add Repository*, escolha `Monburan` e o papel **Write**. Sem isso o passo "Build and push" falha com
> `denied: permission_denied: write_package` (visto em 17/10/2026; o build da imagem em si passa no runner).
> Para conferir depois de ajustar, rode o workflow na `main` (`gh workflow run docker-ghcr.yml --ref main`): fora da
> `main` o disparo manual só constrói a imagem e não envia nada.

Servidor novo: `provision.sh` (Docker, nginx, certbot...), `harden.sh` e `deploy.sh`, nessa ordem; o passo a passo está em
[SEGURANCA.md](SEGURANCA.md#servidor-novo-do-zero). O `deploy.sh` também serve para atualizar (backup, tag de rollback, pull, reinício e
espera do `/health`).

Na `main`, o workflow `docker-ghcr.yml` publica a imagem e, na sequência, o job `deploy-vps` entra por SSH na VPS, envia
`infra/vps` para `/opt/monburan` e roda o `deploy.sh` — o site é atualizado logo depois do push. O job usa os secrets do
environment `prod` (`VPS_HOST`, `VPS_USER`, `VPS_SSH_KEY` e `VPS_KNOWN_HOSTS`) e uma chave dedicada ao GitHub Actions,
separada da chave da sua máquina. O deploy é serializado e mantém a tag de rollback da imagem anterior.

À mão, o equivalente é:

```bash
cd /opt/monburan
docker tag ghcr.io/rondineleg/monburan:latest ghcr.io/rondineleg/monburan:rollback-$(date +%Y%m%d-%H%M)   # imagem para voltar atrás
bash backup.sh                                          # backup criptografado do banco e dos uploads, fora da VPS (docs/SEGURANCA.md)
docker compose pull app && docker compose up -d app    # as migrations rodam no boot (APPLY_MIGRATIONS=true)
curl -s http://127.0.0.1:8080/health
```

Para ativar as sugestões de currículo com IA depois do deploy (detalhes em [IA-PROVEDORES.md](IA-PROVEDORES.md)):

1. O `docker-compose.yml` da VPS é uma cópia de `infra/vps/docker-compose.prod.yml`: copie a versão nova do repositório
   (ela carrega o `ai-keys.env` e passa `ADMIN_EMAILS` ao app). Exige Docker Compose 2.24+ (`docker compose version`).
2. Coloque `ADMIN_EMAILS=<seu e-mail>` no `.env` e as chaves, uma linha `AI_KEY_<NOME>=<chave>` por provedor, no
   `ai-keys.env` (modelo em `infra/vps/ai-keys.env.example`). Os dois arquivos com permissão `600`.
3. `docker compose up -d --force-recreate app`. Cadastre-se no site com o e-mail de `ADMIN_EMAILS` e confirme o e-mail
   pelo link recebido: a conta vira administradora.
4. Em **Administração → Provedores de IA** confira que a chave aparece como "definida no servidor", use **Testar** e, se
   quiser outro provedor como padrão, **Tornar padrão**. O Xiaomi MiMo já vem cadastrado como padrão na primeira execução.

Para voltar à versão anterior: `docker tag ghcr.io/rondineleg/monburan:rollback-<data-hora> ghcr.io/rondineleg/monburan:latest && docker compose up -d app`.

Voltar a imagem não desfaz migrations (elas rodam no boot da imagem nova): se a versão nova alterou o esquema, restaure antes
o backup feito pelo `backup.sh` acima (o passo a passo da restauração está em [SEGURANCA.md](SEGURANCA.md#4-backup-criptografado-fora-do-servidor)).

Firewall, SSH só por chave, fail2ban, atualizações automáticas, backup criptografado fora do servidor, as telas de
administração e os ajustes de segurança do app: [SEGURANCA.md](SEGURANCA.md).

## Testes

- **Unitários, integração e API** rodam no job `build-and-test` (EF InMemory, sem dependências externas).
- **E2E** (`tests/Monburan.E2ETests`) precisa do app no ar com Postgres e dos usuários do seed de Development. Local:

  ```bash
  ASPNETCORE_ENVIRONMENT=Development APPLY_MIGRATIONS=true dotnet run --project src/Monburan.Web --urls http://localhost:5095
  dotnet test tests/Monburan.E2ETests          # E2E_BASE_URL muda a URL do app
  ```

  As varreduras (`FullSweepTests`) já falharam de forma intermitente por navegarem enquanto a página de chegada ainda
  negociava o circuito Blazor, e hoje esperam esse circuito antes da primeira rota. O job ainda repete a execução uma
  vez antes de reprovar, como proteção até a esteira acumular execuções verdes sem a retentativa.
- **Segurança**: o job `security-scan` procura formatos conhecidos de chave/token nos arquivos versionados e reprova se
  `dotnet list package --vulnerable --include-transitive` apontar algum pacote com vulnerabilidade.
