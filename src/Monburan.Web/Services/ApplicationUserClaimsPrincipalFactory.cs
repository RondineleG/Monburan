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
                AddDisplayName(identity, candidate.Name);
            }

            var advertiser = await applicationDbContext.Advertisers
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Email == email && !a.IsDeleted)
                .ConfigureAwait(false);

            if (advertiser != null)
            {
                identity.AddClaim(new Claim(ClaimsExtensions.AdvertiserIdClaimType, advertiser.Id.Value.ToString()));
                identity.AddClaim(new Claim(ClaimsExtensions.UserTypeClaimType, "advertiser"));
                AddDisplayName(identity, advertiser.Name);
            }
        }

        return identity;
    }

    // Nome de exibição (só para a interface: iniciais e identidade no menu); o primeiro perfil encontrado vale
    private static void AddDisplayName(ClaimsIdentity identity, string name)
    {
        if (!string.IsNullOrWhiteSpace(name) && !identity.HasClaim(c => c.Type == ClaimsExtensions.DisplayNameClaimType))
            identity.AddClaim(new Claim(ClaimsExtensions.DisplayNameClaimType, name.Trim()));
    }

    /// <summary>
    /// Administrador definido por configuração (<c>Admin:Emails</c>, e-mails separados por vírgula ou ponto e vírgula).
    /// Só vale com o e-mail confirmado: sem isso, bastaria cadastrar o e-mail de outra pessoa para virar administrador.
    /// Tirar o e-mail da lista tira o acesso no próximo login (ou na próxima revalidação da sessão).
    /// </summary>
    private bool IsConfiguredAdmin(ApplicationUser user) => AdminEmails.IsConfiguredAdmin(user, AdminEmails.Read(configuration));
}
