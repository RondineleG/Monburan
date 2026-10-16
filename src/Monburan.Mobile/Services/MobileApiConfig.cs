namespace Monburan.Mobile.Services;

/// <summary>
/// URL base da API. Em debug com emulador: rode `adb reverse tcp:5095 tcp:5095`
/// para mapear o localhost do emulador ao da máquina (alternativa: http://10.0.2.2:5095).
/// Para device físico na mesma rede, use o IP da máquina (ex.: http://192.168.x.x:5095).
/// </summary>
public static class MobileApiConfig
{
#if DEBUG
    public static readonly Uri BaseUrl = new("http://127.0.0.1:5095/");
#else
    // API e site convivem na mesma origem — o deploy publica tudo em monburan.com.br
    public static readonly Uri BaseUrl = new("https://monburan.com.br/");
#endif
}
