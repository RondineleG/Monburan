namespace Monburan.MobileUiTests;

/// <summary>Configuração por variáveis de ambiente, com padrões para o emulador local.</summary>
public static class MobileEnvironment
{
    public const string AppPackage = "com.monburan.app";

    public static bool Enabled => Environment.GetEnvironmentVariable("MOBILE_UI") == "1";

    /// <summary>Servidor Appium 2.x.</summary>
    public static Uri AppiumUrl => new(Get("APPIUM_URL", "http://127.0.0.1:4723/"));

    /// <summary>
    /// API vista pela máquina que roda os testes. O app (Debug) acessa http://127.0.0.1:5095 dentro do aparelho,
    /// que o `adb reverse tcp:5095 tcp:5095` liga à mesma porta daqui.
    /// </summary>
    public static Uri ApiUrl => new(Get("MOBILE_API_URL", "http://127.0.0.1:5095/"));

    /// <summary>APK Debug para instalar. Vazio = usar o app que já está instalado no aparelho.</summary>
    public static string? ApkPath =>
        Environment.GetEnvironmentVariable("MOBILE_APK") is { Length: > 0 } path ? Path.GetFullPath(path) : null;

    public static string? DeviceName => Environment.GetEnvironmentVariable("MOBILE_DEVICE");

    private static string Get(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
}
