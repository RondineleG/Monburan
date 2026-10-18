namespace Monburan.Web.Data;

public static class DevelopmentIdentitySeedData
{
    private const string Password = "Monburan123";
    private const string AdminRole = "Admin";
    private const string AdminEmail = "admin@monburan.com.br";

    // Segunda conta de administração de desenvolvimento, com a senha escolhida pelo dono do projeto. A senha não passa na política
    // de produção (falta um dígito), então é gravada direto pelo hasher em vez de por CreateAsync(usuário, senha). Este seed só
    // roda em Development (Program.cs) e o repositório é público: nunca reutilize esta senha fora do ambiente local.
    private const string SecondAdminEmail = "adm@monburan.com.br";
    private const string SecondAdminPassword = "Adm@monburan@norando";

    private static readonly string[] Emails =
    [
        "contato@techsolutions.com",
        "rh@marketingdigital.com",
        "joao.silva@email.com",
        "maria.santos@email.com"
    ];

    public static async Task InitializeAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        if (!await roleManager.RoleExistsAsync(AdminRole).ConfigureAwait(false))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(AdminRole)).ConfigureAwait(false);
        }

        var admin = await userManager.FindByEmailAsync(AdminEmail).ConfigureAwait(false);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var adminResult = await userManager.CreateAsync(admin, Password).ConfigureAwait(false);
            if (!adminResult.Succeeded)
            {
                var errors = string.Join(", ", adminResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException(
                    $"Não foi possível criar o usuário administrador de desenvolvimento: {errors}");
            }
        }

        await userManager.SetLockoutEnabledAsync(admin, false).ConfigureAwait(false);

        if (!await userManager.IsInRoleAsync(admin, AdminRole).ConfigureAwait(false))
        {
            await userManager.AddToRoleAsync(admin, AdminRole).ConfigureAwait(false);
        }

        await EnsureSecondAdminAsync(userManager).ConfigureAwait(false);

        foreach (var email in Emails)
        {
            var user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
            if (user is not null)
            {
                var resetToken = await userManager.GeneratePasswordResetTokenAsync(user)
                    .ConfigureAwait(false);
                var resetResult = await userManager.ResetPasswordAsync(user, resetToken, Password)
                    .ConfigureAwait(false);
                if (!resetResult.Succeeded)
                {
                    var errors = string.Join(", ", resetResult.Errors.Select(error => error.Description));
                    throw new InvalidOperationException(
                        $"Não foi possível atualizar o usuário de desenvolvimento {email}: {errors}");
                }

                await userManager.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
                await userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
                await userManager.SetLockoutEnabledAsync(user, false).ConfigureAwait(false);
                continue;
            }

            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var result = await userManager.CreateAsync(user, Password).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException(
                    $"Não foi possível criar o usuário de desenvolvimento {email}: {errors}");
            }

            await userManager.SetLockoutEnabledAsync(user, false).ConfigureAwait(false);
        }
    }

    private static async Task EnsureSecondAdminAsync(UserManager<ApplicationUser> userManager)
    {
        var admin = await userManager.FindByEmailAsync(SecondAdminEmail).ConfigureAwait(false);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = SecondAdminEmail,
                Email = SecondAdminEmail,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            admin.PasswordHash = userManager.PasswordHasher.HashPassword(admin, SecondAdminPassword);

            var result = await userManager.CreateAsync(admin).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException(
                    $"Não foi possível criar o usuário administrador de desenvolvimento {SecondAdminEmail}: {errors}");
            }
        }

        await userManager.SetLockoutEnabledAsync(admin, false).ConfigureAwait(false);

        if (!await userManager.IsInRoleAsync(admin, AdminRole).ConfigureAwait(false))
        {
            await userManager.AddToRoleAsync(admin, AdminRole).ConfigureAwait(false);
        }
    }
}
