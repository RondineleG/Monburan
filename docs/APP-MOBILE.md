# Aplicativo Android (MAUI Blazor Hybrid)

O aplicativo fica em `src/Monburan.Mobile`, com a camada compartilhada em `src/Monburan.Mobile.Shared`. O pacote é `com.monburan.app`. A versão no código é a 1.6.0 (build 6), publicada quando a `dev` for promovida para a `main`; a última release publicada é a [v1.5.0](https://github.com/RondineleG/Monburan/releases/tag/v1.5.0).

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

## Exclusão de conta

A tela **Perfil** tem a seção "Excluir conta": pede a senha, confirma numa caixa de diálogo e chama
`POST /api/v1/account/delete`. O servidor segue a mesma regra da página `/conta/excluir-dados` do site: apaga o usuário e
anonimiza perfil e currículos; as candidaturas ficam anônimas no histórico das empresas. Depois a sessão é limpa e o app
volta ao início. É exigência do Google Play (e da App Store) para apps com cadastro.

## Pronto para a loja (checklist técnico)

| Item | Situação |
|---|---|
| Exclusão de conta dentro do app | Feito (acima). |
| Sessão expirada | O `TokenService` renova em 401; se o refresh for recusado, `SessionExpired` limpa o estado, avisa "Sua sessão expirou" e leva ao login, que volta à mesma tela depois de entrar. Falha temporária no refresh (offline) **não** derruba a sessão. |
| Sem internet | Erros de rede viram `ApiException.IsNetworkError`; as telas mostram "Sem conexão" com "Tentar novamente". |
| Política de privacidade | `https://monburan.com.br/privacidade` (URL para a ficha da loja). |
| Ícone 512 px | `src/Monburan.Web/wwwroot/icon-512.png` serve de base; a ficha do Play pede PNG 512×512 sem transparência. |
| Data Safety (Play) | Dados coletados: nome, e-mail, telefone, currículo (profissional), candidaturas. Transmitidos com criptografia (HTTPS). Exclusão pelo app e pelo site. Sem anúncios e sem venda de dados. Conteúdo do currículo vai a provedor de IA **só** quando a pessoa pede as sugestões. |
| Conta Google Play, ficha, classificação de conteúdo | Pendente (decisão de canal; veja `docs/pre-prod.md` §11.1). |
