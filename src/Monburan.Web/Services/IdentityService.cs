using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Web.Configurations;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Monburan.Web.Services;

public class IdentityService : IIdentityService
{
    // Claim que distingue refresh de access token (não é mapeada pelo JwtSecurityTokenHandler)
    internal const string TokenUseClaim = "token_use";
    internal const string TokenUseAccess = "access";
    internal const string TokenUseRefresh = "refresh";

    // Claim com o SecurityStamp na emissão — revoga o refresh token se a senha mudar
    internal const string SecurityStampClaim = "security_stamp";

    public IdentityService(SignInManager<ApplicationUser> signInManager,
                           UserManager<ApplicationUser> userManager,
                           IOptions<JwtOptions> jwtOptions,
                           ApplicationDbContext applicationDbContext)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _jwtOptions = jwtOptions.Value;
        _applicationDbContext = applicationDbContext;
    }

    private readonly ApplicationDbContext _applicationDbContext;
    private readonly JwtOptions _jwtOptions;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public async Task<UserLoginResponse> Login(UserLoginRequest request)
    {
        // CheckPasswordSignInAsync não emite cookie Identity — a API só entrega JWT
        var user = await _userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
        var result = user is null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true).ConfigureAwait(false);
        if (result.Succeeded)
        {
            // Checado após a senha: conta desativada não pode obter um novo par de
            // tokens (o que anularia a revogação por SecurityStamp do refresh)
            if (!user!.IsActive)
            {
                var blocked = new UserLoginResponse();
                blocked.AddErro("This account is blocked");
                return blocked;
            }

            return await GenerateCredentials(request.Email).ConfigureAwait(false);
        }

        var response = new UserLoginResponse();
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
                response.AddErro("This account is blocked");
            else if (result.IsNotAllowed)
                response.AddErro("This account is not allwed to login");
            else if (result.RequiresTwoFactor)
                response.AddErro("It is necessary to confirm the login in its second authentication factor");
            else
                response.AddErro("User or password are incorrect");
        }

        return response;
    }

    public async Task<UserLoginResponse> RefreshToken(string refreshToken)
    {
        var response = new UserLoginResponse();
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var validation = await handler.ValidateTokenAsync(
                refreshToken, _jwtOptions.ToTokenValidationParameters()).ConfigureAwait(false);

            // O handler pode mapear "sub" para ClaimTypes.NameIdentifier — aceita ambos
            var userId = validation.ClaimsIdentity?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? validation.ClaimsIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Refresh tokens carregam "token_use"=refresh — um access token roubado
            // não pode ser trocado por um par novo com refresh de 30 dias
            var tokenUse = validation.ClaimsIdentity?.FindFirst(TokenUseClaim)?.Value;
            var issuedStamp = validation.ClaimsIdentity?.FindFirst(SecurityStampClaim)?.Value;

            if (!validation.IsValid || userId is null || tokenUse != TokenUseRefresh)
            {
                response.AddErro("Invalid refresh token");
                return response;
            }

            var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);

            // Revogação: conta desativada/bloqueada ou SecurityStamp diferente do da
            // emissão (troca de senha, "sair de todos os dispositivos") invalida o token
            if (user is null || !user.IsActive
                || await _userManager.IsLockedOutAsync(user).ConfigureAwait(false)
                || !string.Equals(issuedStamp, user.SecurityStamp, StringComparison.Ordinal))
            {
                response.AddErro("Invalid refresh token");
                return response;
            }

            return await GenerateCredentials(user.Email ?? string.Empty).ConfigureAwait(false);
        }
        catch
        {
            response.AddErro("Invalid refresh token");
            return response;
        }
    }

    private async Task<UserLoginResponse> GenerateCredentials(string email)
    {
        var user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null)
        {
            var response = new UserLoginResponse();
            response.AddErro("User not found");
            return response;
        }

        var accessTokenClaims = await ObterClaims(user, adicionarClaimsUsuario: true).ConfigureAwait(false);
        accessTokenClaims.Add(new Claim(TokenUseClaim, TokenUseAccess));

        var refreshTokenClaims = await ObterClaims(user, adicionarClaimsUsuario: false).ConfigureAwait(false);
        refreshTokenClaims.Add(new Claim(TokenUseClaim, TokenUseRefresh));
        refreshTokenClaims.Add(new Claim(SecurityStampClaim, user.SecurityStamp ?? string.Empty));

        var dataExpiracaoAccessToken = DateTime.Now.AddSeconds(_jwtOptions.AccessTokenExpiration);
        var dataExpiracaoRefreshToken = DateTime.Now.AddSeconds(_jwtOptions.RefreshTokenExpiration);

        var accessToken = GenerateToken(accessTokenClaims, dataExpiracaoAccessToken);
        var refreshToken = GenerateToken(refreshTokenClaims, dataExpiracaoRefreshToken);

        return new UserLoginResponse(true, accessToken, refreshToken);
    }

    private string GenerateToken(IEnumerable<Claim> claims, DateTime dataExpiracao)
    {
        var jwt = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.Now,
            expires: dataExpiracao,
            signingCredentials: _jwtOptions.SigningCredentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private async Task<IList<Claim>> ObterClaims(ApplicationUser user, bool adicionarClaimsUsuario)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.Now.ToUnixTimeSeconds().ToString())
        };

        if (adicionarClaimsUsuario)
        {
            var userClaims = await _userManager.GetClaimsAsync(user).ConfigureAwait(false);
            var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);

            claims.AddRange(userClaims);
            claims.AddRange(roles.Select(role => new Claim("role", role)));

            // Buscar Candidate e Advertiser pelo email e adicionar claims correspondentes
            var email = user.Email;
            if (!string.IsNullOrEmpty(email))
            {
                var candidate = await _applicationDbContext.Candidates
                    .FirstOrDefaultAsync(c => c.Email == email && !c.IsDeleted)
                    .ConfigureAwait(false);

                if (candidate != null)
                {
                    claims.Add(new Claim(ClaimsExtensions.CandidateIdClaimType, candidate.Id.Value.ToString()));
                    claims.Add(new Claim(ClaimsExtensions.UserTypeClaimType, "candidate"));
                }

                var advertiser = await _applicationDbContext.Advertisers
                    .FirstOrDefaultAsync(a => a.Email == email && !a.IsDeleted)
                    .ConfigureAwait(false);

                if (advertiser != null)
                {
                    claims.Add(new Claim(ClaimsExtensions.AdvertiserIdClaimType, advertiser.Id.Value.ToString()));
                    claims.Add(new Claim(ClaimsExtensions.UserTypeClaimType, "advertiser"));
                }
            }
        }

        return claims;
    }
}