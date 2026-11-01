namespace Monburan.MobileUiTests;

/// <summary>
/// Teste que exige emulador/aparelho Android e servidor Appium. Sem MOBILE_UI=1 ele é pulado, para que
/// `dotnet build` e `dotnet test` da solução continuem verdes em máquinas sem Android.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MobileUiFactAttribute : FactAttribute
{
    public MobileUiFactAttribute()
    {
        if (!MobileEnvironment.Enabled)
            Skip = "Teste de tela em dispositivo: defina MOBILE_UI=1 (veja tests/Monburan.MobileUiTests/README.md).";
    }
}
