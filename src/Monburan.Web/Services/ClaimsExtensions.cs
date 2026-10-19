using System.Security.Claims;

namespace Monburan.Web.Services;

public static class ClaimsExtensions
{
    public const string CandidateIdClaimType = "candidate_id";
    public const string AdvertiserIdClaimType = "advertiser_id";
    public const string UserTypeClaimType = "user_type";
    public const string TwoFactorEnabledClaimType = "two_factor_enabled";

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
}
