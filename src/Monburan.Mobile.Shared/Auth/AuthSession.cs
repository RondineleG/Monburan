namespace Monburan.Mobile.Shared.Auth;

/// <summary>
/// Par de tokens persistido do usuário autenticado.
/// </summary>
public sealed record AuthSession(string AccessToken, string RefreshToken);
