# Aplicativo Android (MAUI Blazor Hybrid)

O aplicativo fica em `src/Monburan.Mobile`, com a camada compartilhada em `src/Monburan.Mobile.Shared`. O pacote é `com.monburan.app`. A versão atual é a 1.5.0; o APK está na release [v1.5.0](https://github.com/RondineleG/Monburan/releases/tag/v1.5.0).

## Estrutura

- `src/Monburan.Mobile.Shared`: `MonburanApiClient`, `AuthApiClient`, `TokenService` (refresh transparente em 401), `AuthHeaderHandler`, `AuthStateService` e `ITokenStore`.
- `src/Monburan.Mobile`: app MAUI Blazor Hybrid. `SecureStorageTokenStore` persiste a sessão. `network_security_config.xml` libera cleartext só para localhost e emulador.

## Como rodar

Pré-requisito: o workload MAUI Android é necessário, inclusive para `dotnet restore` e `dotnet build` da solução.

```bash
dotnet workload install maui-android
```

```bash
# 1. API de pé (Postgres via docker compose + migrations/seed)
docker compose up -d
APPLY_MIGRATIONS=true dotnet run --project src/Monburan.Web

# 2. Emulador ou dispositivo físico (USB): expor a API local ao app
adb reverse tcp:5095 tcp:5095   # o app usa http://127.0.0.1:5095 em Debug
                                # o network_security_config libera cleartext só para
                                # 127.0.0.1, localhost e 10.0.2.2; então
                                # http://<IP-da-LAN>:5095 NÃO funciona no dispositivo físico.
                                # Use adb reverse (ou ajuste MobileApiConfig e a configuração).

# 3. Build e execução
dotnet build src/Monburan.Mobile -f net10.0-android -c Debug -t:Run
# ou gerar um APK autocontido:
dotnet build src/Monburan.Mobile -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true

# 4. APK release assinado (a esteira faz isso na main; ver CI-CD.md)
dotnet publish src/Monburan.Mobile -f net10.0-android -c Release -p:AndroidPackageFormat=apk \
  -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=<keystore> -p:AndroidSigningKeyAlias=<alias> \
  -p:AndroidSigningStorePass=env:KEYSTORE_PASSWORD -p:AndroidSigningKeyPass=env:KEY_PASSWORD
```

## Testes

Os testes de API ficam em `tests/Monburan.ApiTests` (WebApplicationFactory com EF InMemory). A pré-visualização das telas do app para revisão de interface fica em `tests/ux-mobile-preview`.
