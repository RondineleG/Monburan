using System.Security.Claims;

namespace Monburan.Web.Services;

public static class ClaimsExtensions
{
    public const string CandidateIdClaimType = "candidate_id";
    public const string AdvertiserIdClaimType = "advertiser_id";
    public const string UserTypeClaimType = "user_type";
    public const string TwoFactorEnabledClaimType = "two_factor_enabled";
    public const string DisplayNameClaimType = "display_name";

    public static IEnumerable<Claim> WithCandidateId(this IEnumerable<Claim> claims, Guid candidateId)
    {
        return claims.Concat(new[]
        {
            new Claim(CandidateIdClaimType, candidateId.ToString()),
            new Claim(UserTypeClaimType, "candidate")
        });
    }

    public static IEnumerable<Claim> WithAdvertiserId(this IEnumerable<Claim> claims, Guid advertiserId)
    {
        return claims.Concat(new[]
        {
            new Claim(AdvertiserIdClaimType, advertiserId.ToString()),
            new Claim(UserTypeClaimType, "advertiser")
        });
    }

    public static Guid? GetCandidateId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(CandidateIdClaimType);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    public static Guid? GetAdvertiserId(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(AdvertiserIdClaimType);
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    public static string GetUserType(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(UserTypeClaimType) ?? string.Empty;
    }

    /// <summary>Nome para a interface (perfil de candidato ou empresa); sem perfil, a parte local do e-mail.</summary>
    public static string GetDisplayName(this ClaimsPrincipal principal)
    {
        var name = principal.FindFirstValue(DisplayNameClaimType);
        if (!string.IsNullOrWhiteSpace(name))
            return name;
        var login = principal.Identity?.Name ?? string.Empty;
        var at = login.IndexOf('@');
        return at > 0 ? login[..at] : login;
    }

    /// <summary>Até duas iniciais do nome de exibição, em maiúsculas (ex.: "João Silva" → "JS").</summary>
    public static string GetInitials(this ClaimsPrincipal principal) => Initials(principal.GetDisplayName());

    public static string Initials(string name)
    {
        var parts = name.Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant(),
            _ => string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[^1][0]))
        };
    }
}
