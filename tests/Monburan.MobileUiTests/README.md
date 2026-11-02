# Testes de tela do app Android (Appium)

Testes de interface do app MAUI Blazor Hybrid (`com.monburan.app`) rodando em emulador ou aparelho, com
**Appium 3 + UiAutomator2 + xUnit**. A tela do app é uma WebView: o teste entra no contexto `WEBVIEW_com.monburan.app`
e usa seletores CSS (como o Playwright da web).

| Teste | O que cobre |
|---|---|
| `AuthFlowTests` | Boas-vindas sem sessão, login com senha errada, login válido e barra inferior, sessão que sobrevive a fechar e abrir o app, sair da conta |
| `JobsFlowTests` | Lista de vagas, abrir o detalhe, busca sem resultado |

Sem `MOBILE_UI=1` os testes são **pulados**; `dotnet test` da solução continua verde em máquinas sem Android.

## Rodar no CI

Workflow `mobile-ui.yml`: sobe PostgreSQL, API em Development (com seed), APK Debug, Appium e um emulador Android 14.
Roda em PR para a `main` e por disparo manual (*Actions → Mobile UI (Appium) → Run workflow*).

## Rodar local

Requisitos: JDK 17, Android SDK com um emulador (ou aparelho com depuração USB), Node 20+, workload `maui-android`.

```bash
# 1. Appium
npm install --global appium@3.9.0
appium driver install uiautomator2@8.7.3
appium --allow-insecure=uiautomator2:chromedriver_autodownload      # deixe rodando

# 2. API local (Postgres via docker compose; Development popula a massa de demonstração)
docker compose up -d
APPLY_MIGRATIONS=true dotnet run --project src/Monburan.Web      # http://127.0.0.1:5095

# 3. APK Debug autocontido (aponta para http://127.0.0.1:5095)
dotnet build src/Monburan.Mobile -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true

# 4. Emulador ou aparelho ligado, e a porta da API para dentro dele
adb reverse tcp:5095 tcp:5095

# 5. Testes
MOBILE_UI=1 MOBILE_APK=src/Monburan.Mobile/bin/Debug/net10.0-android/com.monburan.app-Signed.apk \
  dotnet test tests/Monburan.MobileUiTests
```

PowerShell: `$env:MOBILE_UI = "1"; $env:MOBILE_APK = "..."; dotnet test tests/Monburan.MobileUiTests`.

## Variáveis

| Variável | Padrão | Uso |
|---|---|---|
| `MOBILE_UI` | (vazio) | `1` liga os testes; senão são pulados |
| `MOBILE_APK` | (vazio) | APK Debug a instalar; vazio usa o app já instalado |
| `APPIUM_URL` | `http://127.0.0.1:4723/` | Servidor Appium |
| `MOBILE_API_URL` | `http://127.0.0.1:5095/` | API, para o teste criar a conta de candidato |
| `MOBILE_DEVICE` | (vazio) | Nome do aparelho, se houver mais de um |

## Observações

- **Só Debug:** o Appium só enxerga a WebView se ela for depurável, o que o app só habilita em Debug
  (`AddBlazorWebViewDeveloperTools`). O APK Release não serve para estes testes.
- Cada teste apaga os dados do app (`mobile: clearApp`) e cria uma conta nova pela API: não há ordem nem massa fixa.
- Não use a conta de teste de produção aqui: o teste cria contas e precisa de uma API de desenvolvimento.
