# Evidência — testes de tela do app Android (Etapa 2)

Registro da validação do aplicativo Monburan por **testes automatizados de tela em emulador Android**, executados no
GitHub Actions. Os artefatos do GitHub expiram em 7 dias, por isso o resultado está guardado aqui.

## Resultado

**7 de 7 testes aprovados, 0 falhas**, na execução 8 do workflow `Mobile UI (Appium)`.

| Item | Valor |
|---|---|
| Execução | <https://github.com/RondineleG/Monburan/actions/runs/41364600639> |
| Commit testado | `be0d425` (branch `fix/mobile-ui-appium-versoes`) |
| Data | 01/11/2026 (workflow iniciado às 20:57, horário de Brasília) |
| Duração da suíte de testes | 3 min 18 s (das 21:02:56 às 21:06:14, horário de Brasília) |
| Relatório bruto (TRX) | [`mobile-ui-etapa2.trx`](mobile-ui-etapa2.trx) |
| Pull request com as correções | <https://github.com/RondineleG/Monburan/pull/87> |

### Ambiente

| Componente | Versão |
|---|---|
| Emulador | Android 14 (API 34, `google_apis`, x86_64, perfil Pixel 6), no runner `ubuntu-latest` |
| Aplicativo | APK **Debug** `com.monburan.app` 1.6.0 (build 6), gerado no próprio job a partir do commit testado |
| Automação | Appium 3.9.0, driver UiAutomator2 8.7.3 (servidor UiAutomator2 v10.7.6), cliente Appium.WebDriver 9.0.0 (C#) |
| Framework de testes | xUnit 2.9.3, .NET 10 |
| API usada pelo app | API do próprio repositório em modo Development (massa de demonstração), PostgreSQL 16 do serviço do job, ligada ao emulador por `adb reverse` |

### Testes e duração

| Teste | Resultado | Duração |
|---|---|---|
| `FirstLaunch_WithoutSession_ShowsTheWelcomeScreen` — primeira abertura sem sessão mostra as boas-vindas | Aprovado | 45 s |
| `Login_WithAWrongPassword_ShowsTheError_AndStaysOnLogin` — senha errada mostra o erro e permanece no login | Aprovado | 15 s |
| `Login_WithValidCredentials_OpensTheDashboard_WithTheBottomNavigation` — login válido abre o painel com a barra de navegação | Aprovado | 20 s |
| `Session_SurvivesClosingAndReopeningTheApp` — a sessão sobrevive a fechar e reabrir o app | Aprovado | 43 s |
| `Logout_GoesToTheLoginScreen_AndTheSessionIsGone` — sair da conta leva ao login e a sessão some ao reabrir | Aprovado | 35 s |
| `Jobs_ListsTheOpenJobs_AndOpensTheDetails` — lista as vagas e abre o detalhe | Aprovado | 18 s |
| `Jobs_SearchWithNoMatch_ShowsTheEmptyState` — busca sem resultado mostra o estado vazio | Aprovado | 20 s |

Cada teste apaga os dados do app, cria uma conta de candidato nova pela API e interage com o app instalado: toca em
botões, digita nos campos e lê o que aparece na tela (a tela do app é uma WebView do Blazor Hybrid).

## Histórico das execuções

O workflow foi disparado manualmente. A primeira execução aconteceu depois do merge do PR #86, que trouxe os testes
**sem que eles tivessem rodado em emulador** (a máquina de desenvolvimento não tem emulador). As falhas abaixo eram da
infraestrutura de teste, e não do aplicativo.

| # | Execução | Commit | Resultado | Causa e correção |
|---|---|---|---|---|
| 1 | 41359021036 | `683c60c` (`dev`) | Falhou | A instalação do driver UiAutomator2 exigia o Appium 4 (beta), e o `appium@2` trazia o servidor 2.x. Fixados Appium 3.9.0 e driver 8.7.3. |
| 2 | 41359474062 | `74080ab` | Falhou | O Appium 3 não inicia com `--allow-insecure` sem escopo de driver. Passou a `--allow-insecure=uiautomator2:chromedriver_autodownload` (testado antes em máquina local). |
| 3 | 41360056082 | `7d45e1a` | Falhou | A instalação do servidor UiAutomator2 no emulador recém-iniciado estourou o limite padrão de 20 s. Timeouts aumentados. |
| 4 | 41360855793 | `0ef65df` | Falhou (4 de 7) | Testes em paralelo sobre um único emulador foram desligados; ainda falharam a primeira abertura a frio (45 s), a WebView após reabrir o app e um clique coberto pela barra inferior. |
| 5 | 41361761623 | `e5565e4` | Falhou (4 de 7) | Reentrada na WebView ao reabrir, rolagem antes do toque e espera maior na primeira abertura. Ainda instável. |
| 6 | 41362801913 | `84ccdc6` | Falhou (6 de 7) | Passou a registrar contexto, URL, texto da tela e screenshot na falha. O teste de sair da conta esperava a tela de boas-vindas, mas o app vai ao login (`Profile.LogoutAsync`): expectativa corrigida. |
| 7 | 41363679411 | `7d94fec` | Falhou (6 de 7) | Só o primeiro teste falhou: na partida a frio a WebView não subiu em 90 s. O teste passou a reiniciar o app uma vez nesse caso. |
| 8 | 41364600639 | `be0d425` | **Aprovado (7 de 7)** | — |

## O que isso valida e o que não valida

- **Valida:** o APK instala e abre num Android 14; boas-vindas, login (com erro e com sucesso), sessão guardada,
  sair da conta, lista de vagas, detalhe da vaga e busca sem resultado funcionam de ponta a ponta contra a API.
- **Não valida:** candidatura, currículo e exclusão de conta pelo app (sem teste em dispositivo); aparelhos físicos;
  outras versões do Android; desempenho; o APK **Release** assinado (os testes usam o Debug, único com WebView
  depurável).
- **Instabilidade conhecida:** a primeira abertura do app num emulador recém-iniciado é lenta e já falhou em
  execuções anteriores. A nova tentativa foi validada em uma execução verde, não em várias.
