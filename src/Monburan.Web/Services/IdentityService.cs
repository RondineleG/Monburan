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
        var result = await _signInManager.PasswordSignInAsync(request.Email, request.Password, false, true).ConfigureAwait(false);
        if (result.Succeeded)
            return await GenerateCredentials(request.Email).ConfigureAwait(false);

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

    public async Task<UserLoginResponse> LoginWithoutPassword(string usuarioId)
    {
        var response = new UserLoginResponse();
        var usuario = await _userManager.FindByIdAsync(usuarioId).ConfigureAwait(false);

        if (usuario is null)
        {
            response.AddErro("User not found");
            return response;
        }

        if (await _userManager.IsLockedOutAsync(usuario).ConfigureAwait(false))
            response.AddErro("This account is blocked");
        else if (!await _userManager.IsEmailConfirmedAsync(usuario).ConfigureAwait(false))
            response.AddErro("This account needs to confirm your email before you log in");

        if (response.Sucesso)
            return await GenerateCredentials(usuario.Email ?? string.Empty).ConfigureAwait(false);

        return response;
    }

    public async Task<UserCreateResponse> RegisterUser(UserCreateRequest request)
    {
        var applicationUser = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(applicationUser, request.Password).ConfigureAwait(false);
        if (result.Succeeded)
            await _userManager.SetLockoutEnabledAsync(applicationUser, false).ConfigureAwait(false);

        var response = new UserCreateResponse(result.Succeeded);
        if (!result.Succeeded)
            response.AddErros(result.Errors.Select(r => r.Description));

        return response;
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
        var refreshTokenClaims = await ObterClaims(user, adicionarClaimsUsuario: false).ConfigureAwait(false);

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
            new(JwtRegisteredClaimNames.Nbf, DateTime.Now.ToString()),
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