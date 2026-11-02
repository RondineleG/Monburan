using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace Monburan.MobileUiTests;

/// <summary>
/// Sessão Appium (UiAutomator2) com o app. Cada teste cria a sua e começa com os dados do app apagados
/// (sem sessão guardada), para um não depender do outro. A tela do app é uma WebView do Blazor Hybrid:
/// os testes entram no contexto WEBVIEW e usam seletores CSS, como no Playwright da web.
/// </summary>
public sealed class AppSession : IDisposable
{
    // A primeira abertura do app num emulador recém-iniciado (JIT do .NET + WebView) passa dos 45 s
    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(90);

    public AndroidDriver Driver { get; }

    public AppSession()
    {
        var options = new AppiumOptions
        {
            PlatformName = "Android",
            AutomationName = "UiAutomator2"
        };
        options.AddAdditionalAppiumOption("appPackage", MobileEnvironment.AppPackage);
        options.AddAdditionalAppiumOption("appWaitActivity", "*");
        options.AddAdditionalAppiumOption("autoGrantPermissions", true);
        options.AddAdditionalAppiumOption("newCommandTimeout", 180);
        // A WebView do app é depurável no Debug; o Appium baixa o chromedriver compatível
        options.AddAdditionalAppiumOption("chromedriverAutodownload", true);
        options.AddAdditionalAppiumOption("ensureWebviewsHavePages", true);
        options.AddAdditionalAppiumOption("adbExecTimeout", 120000);
        // Emulador recém-iniciado no CI é lento: o padrão de 20 s para instalar o servidor do UiAutomator2 estoura
        options.AddAdditionalAppiumOption("uiautomator2ServerInstallTimeout", 180000);
        options.AddAdditionalAppiumOption("uiautomator2ServerLaunchTimeout", 180000);
        options.AddAdditionalAppiumOption("androidInstallTimeout", 300000);
        if (MobileEnvironment.DeviceName is { } device)
            options.DeviceName = device;
        if (MobileEnvironment.ApkPath is { } apk)
            options.App = apk;

        Driver = new AndroidDriver(MobileEnvironment.AppiumUrl, options, TimeSpan.FromMinutes(3));
        EnsureApiTunnel();
        ClearAppData();
        Relaunch();
    }

    /// <summary>
    /// Reaplica o `adb reverse` da API antes de cada teste. Numa rodada o app mostrou "Sem conexão com o servidor" no
    /// meio da suíte, sinal de que o túnel do emulador para a API tinha caído. Sem adb no PATH, segue sem fazer nada.
    /// </summary>
    private static void EnsureApiTunnel()
    {
        try
        {
            var port = MobileEnvironment.ApiUrl.Port;
            using var adb = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("adb", $"reverse tcp:{port} tcp:{port}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            adb?.WaitForExit(15000);
        }
        catch (Exception) { /* sem adb no PATH: o túnel precisa ter sido aberto por fora */ }
    }

    /// <summary>Fecha e abre o app de novo, mantendo os dados (simula o usuário fechando e voltando).</summary>
    public void Relaunch()
    {
        // Larga a WebView antiga: depois de reabrir o app o chromedriver da sessão anterior fica sem DevTools
        TrySwitchToNative();
        Driver.TerminateApp(MobileEnvironment.AppPackage);
        Driver.ActivateApp(MobileEnvironment.AppPackage);
        try
        {
            EnterWebView();
        }
        catch (WebDriverTimeoutException)
        {
            // Na primeira abertura a frio do emulador a WebView às vezes nem sobe (já passou e já falhou com o mesmo
            // APK): reinicia o app uma vez antes de dar o teste por perdido
            TrySwitchToNative();
            Driver.TerminateApp(MobileEnvironment.AppPackage);
            Driver.ActivateApp(MobileEnvironment.AppPackage);
            EnterWebView();
        }
    }

    private void TrySwitchToNative()
    {
        try { Driver.Context = "NATIVE_APP"; }
        catch (WebDriverException) { /* já estava no nativo ou a WebView sumiu */ }
    }

    private void ClearAppData()
    {
        Driver.TerminateApp(MobileEnvironment.AppPackage);
        Driver.ExecuteScript("mobile: clearApp", new Dictionary<string, object> { ["appId"] = MobileEnvironment.AppPackage });
    }

    private void EnterWebView()
    {
        // Repete até a WebView aceitar o contexto e responder: logo após abrir o app ela ainda pode estar sem DevTools
        Wait().Until(_ =>
        {
            try
            {
                var context = Driver.Contexts.FirstOrDefault(c => c.StartsWith("WEBVIEW_" + MobileEnvironment.AppPackage, StringComparison.Ordinal));
                if (context is null)
                    return false;
                Driver.Context = context;
                Driver.FindElement(By.CssSelector("body"));
                return true;
            }
            catch (WebDriverException)
            {
                TrySwitchToNative();
                return false;
            }
        });
    }

    /// <summary>Rola o elemento para o centro antes de tocar: a barra inferior fixa cobre o rodapé da página.</summary>
    private void Click(IWebElement element)
    {
        Driver.ExecuteScript("arguments[0].scrollIntoView({block: 'center'})", element);
        element.Click();
    }

    private WebDriverWait Wait() =>
        new(new SystemClock(), Driver, DefaultWait, TimeSpan.FromMilliseconds(300));

    public IWebElement WaitFor(string css) =>
        Poll($"elemento '{css}'", () =>
        {
            var element = Driver.FindElement(By.CssSelector(css));
            return element.Displayed ? element : null;
        });

    /// <summary>Espera o texto aparecer em algum elemento que case com o seletor.</summary>
    public IWebElement WaitForText(string css, string text) =>
        Poll($"'{text}' em '{css}'", () =>
            Driver.FindElements(By.CssSelector(css))
                .FirstOrDefault(e => e.Displayed && e.Text.Contains(text, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Repete a sonda até ela devolver algo. Elemento ainda ausente ou velho é só "tente de novo"; se o chromedriver
    /// perder o DevTools (a WebView foi recriada), reentra na WebView e continua. Ao estourar o tempo, a mensagem
    /// traz o que estava na tela e um screenshot é salvo em MOBILE_DIAG_DIR.
    /// </summary>
    private IWebElement Poll(string what, Func<IWebElement?> probe)
    {
        try
        {
            return Wait().Until(_ =>
            {
                try { return probe(); }
                catch (NoSuchElementException) { return null; }
                catch (StaleElementReferenceException) { return null; }
                catch (WebDriverException ex) when (ex is not WebDriverTimeoutException)
                {
                    // "disconnected: not connected to DevTools" e semelhantes
                    try { EnterWebView(); } catch (WebDriverTimeoutException) { }
                    return null;
                }
            })!;
        }
        catch (WebDriverTimeoutException)
        {
            throw new WebDriverTimeoutException($"Não apareceu {what} em {DefaultWait.TotalSeconds:0}s. {Describe()}");
        }
    }

    private string Describe()
    {
        var context = "?";
        var url = "?";
        var body = "?";
        try { context = Driver.Context; } catch (WebDriverException) { }
        try { url = Driver.Url; } catch (WebDriverException) { }
        try
        {
            var text = Driver.FindElement(By.CssSelector("body")).Text.ReplaceLineEndings(" | ");
            body = text.Length > 500 ? text[..500] : text;
        }
        catch (WebDriverException) { }
        SaveScreenshot();
        return $"Contexto: {context}; URL: {url}; Texto na tela: [{body}]";
    }

    /// <summary>
    /// Registra a tela inteira do aparelho (com barra de status) como evidência do passo, quando MOBILE_SHOTS_DIR
    /// está definida. Chamar com o estado já visível na tela, depois do WaitFor. Nunca derruba o teste.
    /// </summary>
    public void Capture(string name)
    {
        var dir = Environment.GetEnvironmentVariable("MOBILE_SHOTS_DIR");
        if (string.IsNullOrEmpty(dir))
            return;
        string? webView = null;
        try
        {
            Thread.Sleep(1500); // deixa terminar a animação de entrada da tela
            Directory.CreateDirectory(dir);
            webView = Driver.Context;
            Driver.Context = "NATIVE_APP";
            Driver.GetScreenshot().SaveAsFile(Path.Combine(dir, $"{name}.png"));
        }
        catch (Exception) { /* evidência nunca derruba o teste */ }
        finally
        {
            if (webView is not null && webView != "NATIVE_APP")
            {
                try { Driver.Context = webView; }
                catch (WebDriverException) { try { EnterWebView(); } catch (WebDriverTimeoutException) { } }
            }
        }
    }

    private void SaveScreenshot()
    {
        try
        {
            var dir = Environment.GetEnvironmentVariable("MOBILE_DIAG_DIR");
            if (string.IsNullOrEmpty(dir))
                return;
            Directory.CreateDirectory(dir);
            TrySwitchToNative();
            Driver.GetScreenshot().SaveAsFile(Path.Combine(dir, $"falha-{DateTime.UtcNow:HHmmss-fff}.png"));
        }
        catch (Exception) { /* diagnóstico nunca derruba o teste */ }
    }

    /// <summary>Espera uma condição sobre a página (ex.: todos os cartões da lista são de uma modalidade).</summary>
    public void WaitUntil(string what, Func<bool> condition) =>
        Poll(what, () =>
        {
            try { return condition() ? Driver.FindElement(By.CssSelector("body")) : null; }
            catch (StaleElementReferenceException) { return null; }
        });

    /// <summary>Textos dos elementos visíveis que casam com o seletor.</summary>
    public IReadOnlyList<string> TextsOf(string css) =>
        Driver.FindElements(By.CssSelector(css)).Where(e => e.Displayed).Select(e => e.Text).ToList();

    /// <summary>Atributo do primeiro elemento que casa com o seletor.</summary>
    public string? AttributeOf(string css, string attribute) => WaitFor(css).GetAttribute(attribute);

    /// <summary>Toca num item da barra inferior (Início, Vagas, Candidaturas, Avisos, Perfil).</summary>
    public void GoTo(string label) => TapText(".bottom-nav__link", label);

    /// <summary>
    /// Toca no botão e espera o resultado aparecer; se não aparecer em 12 s, toca de novo (até 3 vezes). Para validações
    /// de formulário, em que o primeiro toque às vezes se perde logo depois de digitar.
    /// </summary>
    public void TapUntilShown(string buttonCss, string buttonText, string expectCss, string expectText, int attempts = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            TapText(buttonCss, buttonText);
            try
            {
                new WebDriverWait(new SystemClock(), Driver, TimeSpan.FromSeconds(12), TimeSpan.FromMilliseconds(300)).Until(_ =>
                {
                    try
                    {
                        return Driver.FindElements(By.CssSelector(expectCss))
                            .FirstOrDefault(e => e.Displayed && e.Text.Contains(expectText, StringComparison.OrdinalIgnoreCase));
                    }
                    catch (StaleElementReferenceException) { return null; }
                });
                return;
            }
            catch (WebDriverTimeoutException) when (attempt < attempts) { }
        }
    }

    public void Tap(string css) => Click(WaitFor(css));

    public void TapText(string css, string text) => Click(WaitForText(css, text));

    public void Type(string css, string value)
    {
        // Logo após navegar, o campo já existe mas ainda não aceita digitação ("element not interactable"): repete
        Wait().Until(_ =>
        {
            try
            {
                var field = Driver.FindElement(By.CssSelector(css));
                field.Clear();
                field.SendKeys(value);
                return true;
            }
            catch (ElementNotInteractableException) { return false; }
            catch (NoSuchElementException) { return false; }
            catch (StaleElementReferenceException) { return false; }
        });
    }

    /// <summary>Entra pelo formulário de login do app (Início, Entrar).</summary>
    public void LogIn(string email, string password)
    {
        TapText("a.btn", "Entrar");
        Type("#login-email", email);
        Type("#login-password", password);
        Tap("form button[type=submit]");
    }

    public void Dispose()
    {
        try { Driver.Quit(); }
        catch { /* sessão já encerrada */ }
        Driver.Dispose();
    }
}
