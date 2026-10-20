namespace Monburan.Mobile.Shared.Api;

public static class ApiClientNames
{
    /// <summary>Cliente HTTP com AuthHeaderHandler (bearer + refresh automático).</summary>
    public const string Api = "MonburanApi";

    /// <summary>Igual ao <see cref="Api"/>, com tempo limite maior: envio de PDF de até 10 MB em rede móvel lenta.</summary>
    public const string Upload = "MonburanUpload";

    /// <summary>Cliente HTTP sem autenticação — usado para login/refresh/register.</summary>
    public const string Auth = "MonburanAuth";
}
