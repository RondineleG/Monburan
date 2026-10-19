using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Monburan.Web.Services;

/// <summary>Exige que o administrador tenha a verificação em duas etapas ligada (ver <see cref="SecurityOptions.RequireTwoFactorForAdmins"/>).</summary>
public sealed class AdminTwoFactorRequirement : IAuthorizationRequirement;

public sealed class AdminTwoFactorHandler(IOptionsMonitor<SecurityOptions> options) : AuthorizationHandler<AdminTwoFactorRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminTwoFactorRequirement requirement)
    {
        if (!options.CurrentValue.RequireTwoFactorForAdmins || HasTwoFactor(context.User))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }

    public static bool HasTwoFactor(System.Security.Claims.ClaimsPrincipal user) =>
        user.HasClaim(ClaimsExtensions.TwoFactorEnabledClaimType, "true");
}
