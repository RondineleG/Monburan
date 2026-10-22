using Microsoft.AspNetCore.Identity;

using Monburan.Web.Models;

namespace Monburan.Web.Components.Account
{
    internal sealed class IdentityUserAccessor(UserManager<ApplicationUser> userManager, IdentityRedirectManager redirectManager)
    {
        public async Task<ApplicationUser> GetRequiredUserAsync(HttpContext context)
        {
            var user = await userManager.GetUserAsync(context.User);

            if (user is null)
            {
                // O ID da conta não aparece para a pessoa: só o recado de como seguir
                redirectManager.RedirectToWithStatus("Account/InvalidUser", "Erro: não foi possível carregar sua conta. Entre novamente.", context);
            }

            return user;
        }
    }
}