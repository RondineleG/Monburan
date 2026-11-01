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
    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(45);

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
        options.AddAdditionalAppiumOption("adbExecTimeout", 60000);
        if (MobileEnvironment.DeviceName is { } device)
            options.DeviceName = device;
        if (MobileEnvironment.ApkPath is { } apk)
            options.App = apk;

        Driver = new AndroidDriver(MobileEnvironment.AppiumUrl, options, TimeSpan.FromMinutes(3));
        ClearAppData();
        Relaunch();
    }

    /// <summary>Fecha e abre o app de novo, mantendo os dados (simula o usuário fechando e voltando).</summary>
    public void Relaunch()
    {
        Driver.TerminateApp(MobileEnvironment.AppPackage);
        Driver.ActivateApp(MobileEnvironment.AppPackage);
        EnterWebView();
    }

    private void ClearAppData()
    {
        Driver.TerminateApp(MobileEnvironment.AppPackage);
        Driver.ExecuteScript("mobile: clearApp", new Dictionary<string, object> { ["appId"] = MobileEnvironment.AppPackage });
    }

    private void EnterWebView()
    {
        var context = Wait().Until(_ =>
            Driver.Contexts.FirstOrDefault(c => c.StartsWith("WEBVIEW_" + MobileEnvironment.AppPackage, StringComparison.Ordinal)));
        Driver.Context = context;
    }

    private WebDriverWait Wait() =>
        new(new SystemClock(), Driver, DefaultWait, TimeSpan.FromMilliseconds(300));

    public IWebElement WaitFor(string css) =>
        Wait().Until(_ =>
        {
            try
            {
                var element = Driver.FindElement(By.CssSelector(css));
                return element.Displayed ? element : null;
            }
            catch (NoSuchElementException) { return null; }
            catch (StaleElementReferenceException) { return null; }
        });

    /// <summary>Espera o texto aparecer em algum elemento que case com o seletor.</summary>
    public IWebElement WaitForText(string css, string text) =>
        Wait().Until(_ =>
        {
            try
            {
                return Driver.FindElements(By.CssSelector(css))
                    .FirstOrDefault(e => e.Displayed && e.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
            }
            catch (StaleElementReferenceException) { return null; }
        });

    public void Tap(string css) => WaitFor(css).Click();

    public void TapText(string css, string text) => WaitForText(css, text).Click();

    public void Type(string css, string value)
    {
        var field = WaitFor(css);
        field.Clear();
        field.SendKeys(value);
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
