namespace Monburan.Mobile.Shared.Auth;

/// <summary>Resultado da tentativa de renovar a sessão.</summary>
public enum RefreshOutcome
{
    /// <summary>Há um access token novo (renovado agora ou por outra chamada).</summary>
    Renewed,

    /// <summary>O servidor recusou o refresh token (ou não há sessão): a sessão acabou.</summary>
    Rejected,

    /// <summary>Não deu para renovar agora (sem rede, tempo esgotado, 5xx, 429): a sessão continua valendo.</summary>
    Unavailable
}
