using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Monburan.Web.Services;

/// <summary>
/// Faz a inativação de uma conta valer no login e nas sessões já abertas. A conta inativa só é revelada a quem acertou a
/// senha (senão daria para descobrir quais e-mails têm conta inativa); as sessões abertas caem na próxima revalidação,
/// que a própria inativação antecipa ao trocar o carimbo de segurança.
/// </summary>
public sealed class ApplicationSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public override async Task<Microsoft.AspNetCore.Identity.SignInResult> CheckPasswordSignInAsync(ApplicationUser user, string password, bool lockoutOnFailure)
    {
        var result = await base.CheckPasswordSignInAsync(user, password, lockoutOnFailure);
        return result.Succeeded && !user.IsActive ? Microsoft.AspNetCore.Identity.SignInResult.NotAllowed : result;
    }

    public override async Task<ApplicationUser?> ValidateSecurityStampAsync(ClaimsPrincipal? principal)
    {
        var user = await base.ValidateSecurityStampAsync(principal);
        return user is { IsActive: true } ? user : null;
    }
}
