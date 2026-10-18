using FluentAssertions;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Web.Models;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

/// <summary>Papel de administrador: quem concede, quem retira e a trava do último administrador ativo.</summary>
public class AdminRoleApiTests : IClassFixture<MonburanApiFactory>
{
    private const string Password = "Cavalo-Azul-Bateria-7";
    private static readonly AdminActor Moderator = new("99999999-9999-9999-9999-999999999999", "moderador@monburan.com.br");

    private readonly MonburanApiFactory _factory;

    public AdminRoleApiTests(MonburanApiFactory factory) => _factory = factory;

    internal static async Task<T> WithServicesAsync<T>(WebApplicationFactory<Program> factory, Func<IServiceProvider, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    internal static async Task<ApplicationUser> CreateUserAsync(WebApplicationFactory<Program> factory, string email, bool confirmed = true) =>
        await WithServicesAsync(factory, async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = confirmed, CreatedAt = DateTime.UtcNow, IsActive = true };
            (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
            return user;
        });

    internal static Task<RequestResult> RunAsync(WebApplicationFactory<Program> factory, Func<AdminAccountService, Task<RequestResult>> action) =>
        WithServicesAsync(factory, services => action(services.GetRequiredService<AdminAccountService>()));

    private static async Task<bool> IsInAdminRoleAsync(WebApplicationFactory<Program> factory, Guid id) =>
        await WithServicesAsync(factory, async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            return await users.IsInRoleAsync((await users.FindByIdAsync(id.ToString()))!, "Admin");
        });

    private static async Task<string?> StampOfAsync(WebApplicationFactory<Program> factory, Guid id) =>
        await WithServicesAsync(factory, async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            return await users.GetSecurityStampAsync((await users.FindByIdAsync(id.ToString()))!);
        });

    private Task<ApplicationUser> SeededAdminAsync() =>
        WithServicesAsync(_factory, async services => (await services.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("admin@monburan.com.br"))!);

    [Fact]
    public async Task Grant_NeedsAnActiveAccountWithAConfirmedEmail()
    {
        var unconfirmed = await CreateUserAsync(_factory, $"sem-confirmar.{Guid.NewGuid():N}@teste.com", confirmed: false);
        var inactive = await CreateUserAsync(_factory, $"inativa.{Guid.NewGuid():N}@teste.com");
        await RunAsync(_factory, service => service.DeactivateAsync(inactive.Id, "para o teste", Moderator));

        (await RunAsync(_factory, service => service.SetAdminAsync(unconfirmed.Id, true, Moderator))).Message.Should().Contain("Confirme o e-mail");
        (await RunAsync(_factory, service => service.SetAdminAsync(inactive.Id, true, Moderator))).Message.Should().Contain("Reative a conta");

        (await IsInAdminRoleAsync(_factory, unconfirmed.Id)).Should().BeFalse();
        (await IsInAdminRoleAsync(_factory, inactive.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Grant_AddsTheRole_RefreshesTheSession_AndIsAudited()
    {
        var user = await CreateUserAsync(_factory, $"nova-admin.{Guid.NewGuid():N}@teste.com");
        var stampBefore = await StampOfAsync(_factory, user.Id);

        var result = await RunAsync(_factory, service => service.SetAdminAsync(user.Id, true, Moderator));

        result.Status.Should().Be(EResultStatus.Success, result.Message);
        (await IsInAdminRoleAsync(_factory, user.Id)).Should().BeTrue();
        (await StampOfAsync(_factory, user.Id)).Should().NotBe(stampBefore);
        (await WithServicesAsync(_factory, s => s.GetRequiredService<IAdminAuditLog>().ListAsync(new AdminAuditFilter(SearchTerm: user.Email), 1, 10)))
            .Data.Should().ContainSingle(entry => entry.Action == AdminAuditActions.AccountAdminGranted);

        (await RunAsync(_factory, service => service.SetAdminAsync(user.Id, true, Moderator))).Message.Should().Contain("já é de administrador");
    }

    [Fact]
    public async Task Revoke_RefusesYourself_AndSomeoneWhoIsNotAnAdmin()
    {
        var admin = await CreateUserAsync(_factory, $"admin-propria.{Guid.NewGuid():N}@teste.com");
        await RunAsync(_factory, service => service.SetAdminAsync(admin.Id, true, Moderator));
        var regular = await CreateUserAsync(_factory, $"comum.{Guid.NewGuid():N}@teste.com");

        (await RunAsync(_factory, service => service.SetAdminAsync(admin.Id, false, new AdminActor(admin.Id.ToString(), admin.Email!))))
            .Message.Should().Contain("próprio papel");
        (await RunAsync(_factory, service => service.SetAdminAsync(regular.Id, false, Moderator))).Message.Should().Contain("não é de administrador");
        (await IsInAdminRoleAsync(_factory, admin.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Revoke_RemovesTheRole_WhenThereIsAnotherActiveAdmin()
    {
        var other = await CreateUserAsync(_factory, $"outra-admin.{Guid.NewGuid():N}@teste.com");
        await RunAsync(_factory, service => service.SetAdminAsync(other.Id, true, Moderator));

        var result = await RunAsync(_factory, service => service.SetAdminAsync(other.Id, false, Moderator));

        result.Status.Should().Be(EResultStatus.Success, result.Message);
        (await IsInAdminRoleAsync(_factory, other.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task TheAdminFilter_ListsTheAdministrators()
    {
        var seeded = await SeededAdminAsync();

        var page = await WithServicesAsync(_factory, s => s.GetRequiredService<AdminAccountService>().ListAsync(new AdminAccountFilter(Type: "admin"), 1, 50));

        page.Items.Should().Contain(row => row.Id == seeded.Id && row.IsAdmin);
        page.Items.Should().OnlyContain(row => row.IsAdmin);
    }
}

/// <summary>
/// A trava do último administrador ativo. Fica numa classe própria (banco só dele): se outros testes criassem
/// administradores, a conta do seed deixaria de ser a única e a trava não seria exercitada.
/// </summary>
public class LastAdminApiTests : IClassFixture<MonburanApiFactory>
{
    private static readonly AdminActor Moderator = new("99999999-9999-9999-9999-999999999999", "moderador@monburan.com.br");

    private readonly MonburanApiFactory _factory;

    public LastAdminApiTests(MonburanApiFactory factory) => _factory = factory;

    private Task<ApplicationUser> SeededAdminAsync() =>
        AdminRoleApiTests.WithServicesAsync(_factory, async services => (await services.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("admin@monburan.com.br"))!);

    [Fact]
    public async Task TheLastActiveAdmin_CannotBeDeactivatedOrDemoted_ButCanOnceAnotherExists()
    {
        var seeded = await SeededAdminAsync();

        // Só o administrador do seed: nada pode tirá-lo
        (await AdminRoleApiTests.RunAsync(_factory, service => service.DeactivateAsync(seeded.Id, "teste da trava", Moderator))).Message.Should().Contain("última conta de administrador");
        (await AdminRoleApiTests.RunAsync(_factory, service => service.SetAdminAsync(seeded.Id, false, Moderator))).Message.Should().Contain("última conta de administrador");

        // Com um segundo administrador ativo, o primeiro pode sair
        var second = await AdminRoleApiTests.CreateUserAsync(_factory, $"segundo-admin.{Guid.NewGuid():N}@teste.com");
        await AdminRoleApiTests.RunAsync(_factory, service => service.SetAdminAsync(second.Id, true, Moderator));
        (await AdminRoleApiTests.RunAsync(_factory, service => service.DeactivateAsync(seeded.Id, "segundo administrador já existe", Moderator))).Status.Should().Be(EResultStatus.Success);

        // E agora o segundo é o último: uma conta inativa não conta
        (await AdminRoleApiTests.RunAsync(_factory, service => service.DeactivateAsync(second.Id, "não pode", Moderator))).Message.Should().Contain("última conta de administrador");
        (await AdminRoleApiTests.RunAsync(_factory, service => service.SetAdminAsync(second.Id, false, Moderator))).Message.Should().Contain("última conta de administrador");

        // Devolve o estado: reativa o primeiro
        (await AdminRoleApiTests.RunAsync(_factory, service => service.ActivateAsync(seeded.Id, Moderator))).Status.Should().Be(EResultStatus.Success);
        (await AdminRoleApiTests.RunAsync(_factory, service => service.DeactivateAsync(second.Id, "agora o primeiro está de volta", Moderator))).Status.Should().Be(EResultStatus.Success);
    }

}

/// <summary>Administradores definidos por e-mail (ADMIN_EMAILS): contam como administradores, mas só a configuração os retira.</summary>
public class ConfiguredAdminApiTests : IClassFixture<AdminEmailsApiFactory>
{
    private static readonly AdminActor Moderator = new("99999999-9999-9999-9999-999999999999", "moderador@monburan.com.br");

    private readonly AdminEmailsApiFactory _factory;

    public ConfiguredAdminApiTests(AdminEmailsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AConfiguredAdmin_IsFlagged_AndCannotBeDemotedOnTheScreen()
    {
        var owner = await AdminRoleApiTests.CreateUserAsync(_factory, "dono@exemplo.com");

        var page = await AdminRoleApiTests.WithServicesAsync(_factory,
            s => s.GetRequiredService<AdminAccountService>().ListAsync(new AdminAccountFilter(Type: "admin"), 1, 50));
        var row = page.Items.Should().ContainSingle(item => item.Id == owner.Id).Which;
        row.IsAdmin.Should().BeTrue();
        row.IsConfiguredAdmin.Should().BeTrue();

        var refused = await AdminRoleApiTests.RunAsync(_factory, service => service.SetAdminAsync(owner.Id, false, Moderator));
        refused.Status.Should().Be(EResultStatus.DomainError);
        refused.Message.Should().Contain("ADMIN_EMAILS");
        (await AdminRoleApiTests.RunAsync(_factory, service => service.SetAdminAsync(owner.Id, true, Moderator))).Message.Should().Contain("já é de administrador");
    }

    [Fact]
    public async Task AConfiguredAdmin_CountsAsAnActiveAdmin_SoTheSeededOneCanBeDeactivated()
    {
        var owner = await AdminRoleApiTests.CreateUserAsync(_factory, "outra.pessoa@exemplo.com");
        var seeded = await AdminRoleApiTests.WithServicesAsync(_factory,
            async s => (await s.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("admin@monburan.com.br"))!);

        var result = await AdminRoleApiTests.RunAsync(_factory, service => service.DeactivateAsync(seeded.Id, "há outro administrador configurado", Moderator));

        result.Status.Should().Be(EResultStatus.Success, result.Message);
        (await AdminRoleApiTests.RunAsync(_factory, service => service.ActivateAsync(seeded.Id, Moderator))).Status.Should().Be(EResultStatus.Success);
        owner.Email.Should().NotBeNull();
    }
}
