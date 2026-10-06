using Microsoft.IdentityModel.Tokens;

namespace Monburan.Web.Configurations;

public class JwtOptions
{
    public int AccessTokenExpiration { get; set; }

    public string Audience { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public int RefreshTokenExpiration { get; set; }

    public SigningCredentials SigningCredentials { get; set; } = null!;
}