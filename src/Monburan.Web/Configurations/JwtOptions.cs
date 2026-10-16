using System.Text;

using Microsoft.IdentityModel.Tokens;

namespace Monburan.Web.Configurations;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public int AccessTokenExpiration { get; set; }

    public string Audience { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public int RefreshTokenExpiration { get; set; }

    /// <summary>
    /// Chave simétrica usada para assinar os tokens. Em produção deve vir de
    /// variável de ambiente/secret store (Jwt__SigningKey), nunca commitada.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    private SigningCredentials? _signingCredentials;

    public SigningCredentials SigningCredentials => _signingCredentials ??= new SigningCredentials(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public TokenValidationParameters ToTokenValidationParameters(bool validateLifetime = true)
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningCredentials.Key,
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }
}
