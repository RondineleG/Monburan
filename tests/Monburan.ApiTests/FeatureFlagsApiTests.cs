using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;

using FluentAssertions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

/// <summary>Feature flags por grupo de acesso: avaliação, administração, endpoint do app e quem pode gerenciar.</summary>
public sealed class FeatureFlagsApiTests : IClassFixture<MonburanApiFactory>
{
    private const string Joao = "joao.silva@email.com";
    private const string Maria = "maria.santos@email.com";
    private const string Recruiter = "contato@techsolutions.com";
    private const string Password = "Monburan123";
    private static readonly AdminActor Actor = new(Guid.NewGuid().ToString(), "admin@monburan.com.br");

    private readonly MonburanApiFactory _factory;
    private readonly HttpClient _client;

    public FeatureFlagsApiTests(MonburanApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    private static string NewName() => "flag-" + Guid.NewGuid().ToString("N")[..10];

    private async Task WithAdminAsync(Func<FeatureFlagAdminService, Task> action)
    {
        using var scope = _factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<FeatureFlagAdminService>());
    }

    private async Task SaveFlagAsync(string name, bool everyone = false, params string[] groups) =>
        await WithAdminAsync(async admin =>
            (await admin.SaveFlagAsync(new FeatureFlagInput(null, name, "Funcionalidade nova", everyone, groups, null), Actor))
                .Status.Should().Be(EResultStatus.Success));

    private async Task<List<string>> FeaturesAsync(string? email = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/features");
        if (email is not null)
        {
            var login = await _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest { Email = email, Password = Password });
            login.EnsureSuccessStatusCode();
            var token = (await login.Content.ReadFromJsonAsync<UserLoginResponse>())!.AccessToken;
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<FeatureListResponse>())!.Features;
    }

    [Fact]
    public async Task Visitor_SeesOnlyFlagsEnabledForEveryone()
    {
        var open = NewName();
        var forCandidates = NewName();
        await SaveFlagAsync(open, everyone: true);
        await SaveFlagAsync(forCandidates, everyone: false, "candidato");

        var features = await FeaturesAsync();

        features.Should().Contain(open).And.NotContain(forCandidates);
    }

    [Fact]
    public async Task ImplicitGroups_SeparateCandidatesFromRecruiters()
    {
        var forCandidates = NewName();
        var forRecruiters = NewName();
        await SaveFlagAsync(forCandidates, false, "candidato");
        await SaveFlagAsync(forRecruiters, false, "recrutador");

        (await FeaturesAsync(Joao)).Should().Contain(forCandidates).And.NotContain(forRecruiters);
        (await FeaturesAsync(Recruiter)).Should().Contain(forRecruiters).And.NotContain(forCandidates);
    }

    [Fact]
    public async Task BetaGroup_TurnsTheFlagOnOnlyForItsMembers_AndLeavingTakesEffectAtOnce()
    {
        // Em produção o grupo vem da migration (HasData); o EF InMemory da factory não aplica HasData
        await WithAdminAsync(async admin =>
        {
            if ((await admin.ListGroupsAsync()).All(g => g.Name != "candidato-beta"))
                await admin.CreateGroupAsync("candidato-beta", "Candidatos que testam funcionalidades novas", Actor);
        });

        var flag = NewName();
        await SaveFlagAsync(flag, false, "candidato-beta");
        (await FeaturesAsync(Joao)).Should().NotContain(flag);

        await WithAdminAsync(async admin =>
            (await admin.AddMemberAsync("candidato-beta", Joao, Actor)).Status.Should().Be(EResultStatus.Success));

        (await FeaturesAsync(Joao)).Should().Contain(flag);
        (await FeaturesAsync(Maria)).Should().NotContain(flag);

        await WithAdminAsync(async admin =>
        {
            var member = (await admin.ListMembersAsync("candidato-beta")).Single(m => m.Email == Joao);
            (await admin.RemoveMemberAsync("candidato-beta", member.UserId, Actor)).Status.Should().Be(EResultStatus.Success);
        });

        (await FeaturesAsync(Joao)).Should().NotContain(flag);
    }

    [Fact]
    public async Task FeatureFlagService_ChecksTheSameRules_ForTheSite()
    {
        var flag = NewName();
        await SaveFlagAsync(flag, false, "admin");
        var flags = _factory.Services.GetRequiredService<IFeatureFlags>();

        var admin = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "cookie"));
        var visitor = new ClaimsPrincipal(new ClaimsIdentity());

        (await flags.IsEnabledAsync(flag, admin)).Should().BeTrue();
        (await flags.IsEnabledAsync(flag, visitor)).Should().BeFalse();
        (await flags.IsEnabledAsync("nao-existe", admin)).Should().BeFalse();
    }

    [Fact]
    public async Task Admin_RefusesInvalidNames_UnknownGroups_Renames_AndDeletingAGroupInUse()
    {
        await WithAdminAsync(async admin =>
        {
            (await admin.SaveFlagAsync(new FeatureFlagInput(null, "Nome Invalido", "x", false, [], null), Actor))
                .Status.Should().NotBe(EResultStatus.Success);
            (await admin.SaveFlagAsync(new FeatureFlagInput(null, NewName(), "x", false, ["grupo-que-nao-existe"], null), Actor))
                .Status.Should().NotBe(EResultStatus.Success);

            var name = NewName();
            (await admin.SaveFlagAsync(new FeatureFlagInput(null, name, "x", false, [], null), Actor)).Status.Should().Be(EResultStatus.Success);
            var saved = (await admin.ListFlagsAsync()).Single(f => f.Name == name);
            (await admin.SaveFlagAsync(new FeatureFlagInput(saved.Id.Value, name + "-novo", "x", false, [], null), Actor))
                .Status.Should().NotBe(EResultStatus.Success);

            var group = "grupo-" + Guid.NewGuid().ToString("N")[..8];
            (await admin.CreateGroupAsync(group, "Turma de teste de interface", Actor)).Status.Should().Be(EResultStatus.Success);
            (await admin.SaveFlagAsync(new FeatureFlagInput(saved.Id.Value, name, "x", false, [group], null), Actor)).Status.Should().Be(EResultStatus.Success);

            var row = (await admin.ListGroupsAsync()).Single(g => g.Name == group);
            (await admin.DeleteGroupAsync(row.Id, Actor)).Status.Should().NotBe(EResultStatus.Success);

            (await admin.CreateGroupAsync("admin", "grupo automático", Actor)).Status.Should().NotBe(EResultStatus.Success);
        });
    }

    [Fact]
    public async Task ProductRole_OnlyAdminsGrant_AndTheTeamCanManageFlags_ButNotTheRestOfTheAdmin()
    {
        await WithAdminAsync(async admin =>
        {
            (await admin.SetProductRoleAsync(Maria, grant: true, Actor, actorIsAdmin: false)).Status.Should().NotBe(EResultStatus.Success);
            (await admin.SetProductRoleAsync(Maria, grant: true, Actor, actorIsAdmin: true)).Status.Should().Be(EResultStatus.Success);
            (await admin.ListProductTeamAsync()).Should().Contain(m => m.Email == Maria);
            (await admin.SetProductRoleAsync(Maria, grant: false, Actor, actorIsAdmin: true)).Status.Should().Be(EResultStatus.Success);
        });

        var authorization = _factory.Services.GetRequiredService<IAuthorizationService>();
        var product = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Produto"), new Claim(ClaimsExtensions.TwoFactorEnabledClaimType, "true")], "cookie"));
        var candidate = new ClaimsPrincipal(new ClaimsIdentity([new Claim("user_type", "candidate")], "cookie"));

        (await authorization.AuthorizeAsync(product, "FeatureManagers")).Succeeded.Should().BeTrue();
        (await authorization.AuthorizeAsync(product, "AdminOnly")).Succeeded.Should().BeFalse();
        (await authorization.AuthorizeAsync(candidate, "FeatureManagers")).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task AdminPage_RequiresSignIn()
    {
        var response = await _client.GetAsync("/admin/funcionalidades");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/entrar");
    }
}
