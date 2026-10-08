using Microsoft.Extensions.Options;

using System.Security.Claims;

namespace Monburan.Web.Services;

public class ApplicationUserClaimsPrincipalFactory(UserManager<ApplicationUser> userManager,
                                                   ApplicationDbContext applicationDbContext,
                                                   IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser>(userManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user).ConfigureAwait(false);

        var roles = await UserManager.GetRolesAsync(user).ConfigureAwait(false);
        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(identity.RoleClaimType, role));
        }

        var email = user.Email;
        if (!string.IsNullOrEmpty(email))
        {
            var candidate = await applicationDbContext.Candidates
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Email == email && !c.IsDeleted)
                .ConfigureAwait(false);

            if (candidate != null)
            {
                identity.AddClaim(new Claim(ClaimsExtensions.CandidateIdClaimType, candidate.Id.Value.ToString()));
                identity.AddClaim(new Claim(ClaimsExtensions.UserTypeClaimType, "candidate"));
            }

            var advertiser = await applicationDbContext.Advertisers
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Email == email && !a.IsDeleted)
                .ConfigureAwait(false);

            if (advertiser != null)
            {
                identity.AddClaim(new Claim(ClaimsExtensions.AdvertiserIdClaimType, advertiser.Id.Value.ToString()));
                identity.AddClaim(new Claim(ClaimsExtensions.UserTypeClaimType, "advertiser"));
            }
        }

        return identity;
    }
}
