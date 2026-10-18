using Microsoft.Extensions.Options;

using System.Security.Claims;

namespace Monburan.Web.Services;

public class ApplicationUserClaimsPrincipalFactory(UserManager<ApplicationUser> userManager,
                                                   ApplicationDbContext applicationDbContext,
                                                   IOptions<IdentityOptions> optionsAccessor,
                                                   IConfiguration configuration)
    : UserClaimsPrincipalFactory<ApplicationUser>(userManager, optionsAccessor)
{
    private const string AdminRole = "Admin";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user).ConfigureAwait(false);

        var roles = await UserManager.GetRolesAsync(user).ConfigureAwait(false);
        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(identity.RoleClaimType, role));
        }

        if (IsConfiguredAdmin(user) && !roles.Contains(AdminRole))
        {
            identity.AddClaim(new Claim(identity.RoleClaimType, AdminRole));
        }

        // Quem tem a verificação em duas etapas ligada: a política de administrador confere esta claim
        if (user.TwoFactorEnabled)
        {
            identity.AddClaim(new Claim(ClaimsExtensions.TwoFactorEnabledClaimType, "true"));
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

    /// <summary>
    /// Administrador definido por configuração (<c>Admin:Emails</c>, e-mails separados por vírgula ou ponto e vírgula).
    /// Só vale com o e-mail confirmado: sem isso, bastaria cadastrar o e-mail de outra pessoa para virar administrador.
    /// Tirar o e-mail da lista tira o acesso no próximo login (ou na próxima revalidação da sessão).
    /// </summary>
    private bool IsConfiguredAdmin(ApplicationUser user)
    {
        if (!user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email))
            return false;

        var configured = configuration["Admin:Emails"];
        if (string.IsNullOrWhiteSpace(configured))
            return false;

        return configured
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(user.Email, StringComparer.OrdinalIgnoreCase);
    }
}
