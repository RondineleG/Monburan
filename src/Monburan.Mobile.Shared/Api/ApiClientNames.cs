namespace Monburan.Mobile.Shared.Api;

public static class ApiClientNames
{
    /// <summary>Cliente HTTP com AuthHeaderHandler (bearer + refresh automático).</summary>
    public const string Api = "MonburanApi";

    /// <summary>Cliente HTTP sem autenticação — usado para login/refresh/register.</summary>
    public const string Auth = "MonburanAuth";
}
