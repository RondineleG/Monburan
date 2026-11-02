using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Models;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

/// <summary>A mesma API com a sessão revalidada a cada requisição (em produção é a cada 2 minutos).</summary>
public class ImmediateRevalidationApiFactory : MonburanApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero));
    }
}

/// <summary>Gestão das contas pela administração: regras, travas, auditoria e o efeito imediato da inativação.</summary>
public class AdminAccountServiceApiTests : IClassFixture<ImmediateRevalidationApiFactory>
{
    private const string Password = "Cavalo-Azul-Bateria-7";

    // Quem age: um administrador que não é nenhuma das contas dos testes
    private static readonly AdminActor Moderator = new("99999999-9999-9999-9999-999999999999", "moderador@monburan.com.br");

    private readonly ImmediateRevalidationApiFactory _factory;

    public AdminAccountServiceApiTests(ImmediateRevalidationApiFactory factory) => _factory = factory;

    private async Task<T> WithServicesAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private Task<RequestResultSummary> RunAsync(Func<AdminAccountService, Task<Monburan.Core.Requests.RequestResult>> action) =>
        WithServicesAsync(async services =>
        {
            var result = await action(services.GetRequiredService<AdminAccountService>());
            return new RequestResultSummary(result.Status, string.Join(" ", new[] { result.Message }.Concat(result.ValidationResult.Errors.Select(e => e.Message))).Trim());
        });

    private sealed record RequestResultSummary(EResultStatus Status, string Message);

    private async Task<ApplicationUser> CreateUserAsync(string prefix, bool confirmed = true, string? emailOverride = null)
    {
        var email = emailOverride ?? $"{prefix}.{Guid.NewGuid():N}@teste.com";
        return await WithServicesAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = confirmed, CreatedAt = DateTime.UtcNow, IsActive = true,
                FirstName = "Pessoa", LastName = prefix
            };
            (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
            return user;
        });
    }

    private async Task<ApplicationUser> ReloadAsync(Guid id) =>
        await WithServicesAsync(async services => (await services.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id.ToString()))!);

    private async Task<Advertiser> CreateAdvertiserWithJobsAsync(string email)
    {
        await using var context = await _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        var advertiser = new Advertiser
        {
            Id = Id.Create(Guid.NewGuid()), TenantId = "default", Name = "Empresa de Teste " + Guid.NewGuid().ToString("N")[..6],
            Email = email, PersonType = EPersonType.Company, Status = EAdvertiserStatus.Active
        };
        context.Advertisers.Add(advertiser);

        Job NewJob(string title, EJobStatus status, DateTime expires, string? reason = null) => new()
        {
            Id = Id.Create(Guid.NewGuid()), TenantId = "default", AdvertiserId = advertiser.Id, Title = title,
            Description = "Descrição da vaga de teste.", Status = status, ExpirationDate = expires,
            Location = new JobLocation { City = "São Paulo", State = "SP", Type = "Remote", IsRemote = true },
            Salary = new SalaryRange { Display = true },
            Moderation = new ModerationInfo { Status = EModerationStatus.Approved, Reason = reason },
            Boost = new JobBoost(), Highlight = new JobHighlight(), Counters = new JobCounters()
        };

        context.Jobs.AddRange(
            NewJob("Vaga ativa A " + advertiser.Name, EJobStatus.Active, DateTime.UtcNow.AddDays(20)),
            NewJob("Vaga pausada B " + advertiser.Name, EJobStatus.Paused, DateTime.UtcNow.AddDays(20)),
            NewJob("Vaga oculta por moderação C " + advertiser.Name, EJobStatus.Hidden, DateTime.UtcNow.AddDays(20), "Conteúdo enganoso"),
            NewJob("Vaga ativa vencida D " + advertiser.Name, EJobStatus.Active, DateTime.UtcNow.AddDays(-2)));
        await context.SaveChangesAsync();
        return advertiser;
    }

    private async Task<Dictionary<string, Job>> JobsOfAsync(Advertiser advertiser)
    {
        await using var context = await _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
        var jobs = await context.Jobs.AsNoTracking().Where(job => job.AdvertiserId == advertiser.Id).ToListAsync();
        return jobs.ToDictionary(job => job.Title.Split(' ')[1] is "ativa" ? (job.Title.Contains("vencida") ? "D" : "A") : job.Title.Split(' ')[1] is "pausada" ? "B" : "C");
    }

    private Task<List<AdminAuditEntry>> AuditOfAsync(string term) =>
        WithServicesAsync(async services =>
            (await services.GetRequiredService<IAdminAuditLog>().ListAsync(new AdminAuditFilter(SearchTerm: term), 1, 50)).Data.ToList());

    private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private async Task<HttpResponseMessage> ApiLoginAsync(HttpClient client, string email, string password) =>
        await client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest { Email = email, Password = password });

    // ---------------------------------------------------------------- inativar e reativar

    [Fact]
    public async Task Deactivate_NeedsAReasonOfAReasonableSize()
    {
        var user = await CreateUserAsync("motivo");

        (await RunAsync(service => service.DeactivateAsync(user.Id, "  ", Moderator))).Status.Should().Be(EResultStatus.HasValidation);
        (await RunAsync(service => service.DeactivateAsync(user.Id, "abc", Moderator))).Status.Should().Be(EResultStatus.HasValidation);
        (await RunAsync(service => service.DeactivateAsync(user.Id, new string('x', 501), Moderator))).Status.Should().Be(EResultStatus.HasValidation);

        (await ReloadAsync(user.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_RecordsWhoWhenAndWhy_AndWritesTheAuditTrail()
    {
        var user = await CreateUserAsync("inativar");

        var result = await RunAsync(service => service.DeactivateAsync(user.Id, "  Anúncios enganosos repetidos  ", Moderator));

        result.Status.Should().Be(EResultStatus.Success, result.Message);
        var saved = await ReloadAsync(user.Id);
        saved.IsActive.Should().BeFalse();
        saved.DeactivatedBy.Should().Be("moderador@monburan.com.br");
        saved.DeactivationReason.Should().Be("Anúncios enganosos repetidos");
        saved.DeactivatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(30));

        var audit = (await AuditOfAsync(user.Email!)).Should().ContainSingle(entry => entry.Action == AdminAuditActions.AccountDeactivated).Which;
        audit.ActorEmail.Should().Be("moderador@monburan.com.br");
        audit.TargetId.Should().Be(user.Id.ToString());
        audit.Details.Should().Be("Anúncios enganosos repetidos");
    }

    [Fact]
    public async Task Deactivate_Yourself_IsRefused()
    {
        var user = await CreateUserAsync("proprio");

        var result = await RunAsync(service => service.DeactivateAsync(user.Id, "teste de trava", new AdminActor(user.Id.ToString(), user.Email!)));

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("própria conta");
        (await ReloadAsync(user.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_AnAlreadyInactiveAccount_AndAnUnknownOne_AreRefused()
    {
        var user = await CreateUserAsync("duas-vezes");
        await RunAsync(service => service.DeactivateAsync(user.Id, "primeira vez", Moderator));

        (await RunAsync(service => service.DeactivateAsync(user.Id, "segunda vez", Moderator))).Message.Should().Contain("já está inativa");
        (await RunAsync(service => service.DeactivateAsync(Guid.NewGuid(), "não existe", Moderator))).Status.Should().Be(EResultStatus.EntityNotFound);
    }

    [Fact]
    public async Task Activate_RestoresTheAccount_AndClearsTheDeactivationData()
    {
        var user = await CreateUserAsync("reativar");
        await RunAsync(service => service.DeactivateAsync(user.Id, "motivo de teste", Moderator));

        var result = await RunAsync(service => service.ActivateAsync(user.Id, Moderator));

        result.Status.Should().Be(EResultStatus.Success, result.Message);
        var saved = await ReloadAsync(user.Id);
        saved.IsActive.Should().BeTrue();
        saved.DeactivatedAt.Should().BeNull();
        saved.DeactivatedBy.Should().BeNull();
        saved.DeactivationReason.Should().BeNull();
        (await AuditOfAsync(user.Email!)).Select(entry => entry.Action)
            .Should().Contain([AdminAuditActions.AccountDeactivated, AdminAuditActions.AccountActivated]);
        (await RunAsync(service => service.ActivateAsync(user.Id, Moderator))).Message.Should().Contain("já está ativa");
    }

    // ---------------------------------------------------------------- efeito sobre perfis e vagas

    [Fact]
    public async Task DeactivatingACompany_HidesItsActiveJobs_AndReactivatingRestoresOnlyThose()
    {
        var user = await CreateUserAsync("empresa");
        var advertiser = await CreateAdvertiserWithJobsAsync(user.Email!);

        await RunAsync(service => service.DeactivateAsync(user.Id, "empresa suspeita de fraude", Moderator));

        var hidden = await JobsOfAsync(advertiser);
        hidden["A"].Status.Should().Be(EJobStatus.Hidden);
        hidden["A"].Moderation!.Reason.Should().Be(AdminAccountService.JobsHiddenReason);
        hidden["D"].Status.Should().Be(EJobStatus.Hidden);
        hidden["B"].Status.Should().Be(EJobStatus.Paused, "vaga pausada pela empresa não é tocada");
        hidden["C"].Status.Should().Be(EJobStatus.Hidden);
        hidden["C"].Moderation!.Reason.Should().Be("Conteúdo enganoso", "a moderação anterior continua como estava");

        var publicJobs = await _factory.CreateClient().GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?size=100");
        publicJobs!.Jobs.Should().NotContain(job => job.AdvertiserId == advertiser.Id.Value);

        await RunAsync(service => service.ActivateAsync(user.Id, Moderator));

        var restored = await JobsOfAsync(advertiser);
        restored["A"].Status.Should().Be(EJobStatus.Active);
        restored["A"].Moderation!.Reason.Should().BeNull();
        restored["D"].Status.Should().Be(EJobStatus.Expired, "a validade já tinha passado");
        restored["B"].Status.Should().Be(EJobStatus.Paused);
        restored["C"].Status.Should().Be(EJobStatus.Hidden, "foi um moderador que escondeu: a reativação da conta não desfaz");
        restored["C"].Moderation!.Reason.Should().Be("Conteúdo enganoso");

        var againPublic = await _factory.CreateClient().GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?size=100");
        againPublic!.Jobs.Should().Contain(job => job.AdvertiserId == advertiser.Id.Value && job.Title.StartsWith("Vaga ativa A"));
    }

    [Fact]
    public async Task TheProfileOfACandidateIsBlockedAndReleased_WithTheAccount()
    {
        var user = await CreateUserAsync("candidato");
        var candidateId = Id.Create(Guid.NewGuid());
        await using (var context = await _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync())
        {
            context.Candidates.Add(new Candidate { Id = candidateId, TenantId = "default", Name = "Candidata de Teste", Email = user.Email!, Status = EAccountStatus.Active });
            await context.SaveChangesAsync();
        }

        async Task<EAccountStatus> StatusAsync()
        {
            await using var context = await _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
            return (await context.Candidates.AsNoTracking().SingleAsync(candidate => candidate.Id == candidateId)).Status;
        }

        await RunAsync(service => service.DeactivateAsync(user.Id, "perfil falso", Moderator));
        (await StatusAsync()).Should().Be(EAccountStatus.Blocked);

        await RunAsync(service => service.ActivateAsync(user.Id, Moderator));
        (await StatusAsync()).Should().Be(EAccountStatus.Active);
    }

    // ---------------------------------------------------------------- o efeito vale na hora

    [Fact]
    public async Task AnInactiveAccount_CannotLogInThroughTheApi_AndCanAgainAfterReactivation()
    {
        var user = await CreateUserAsync("api-login");
        using var client = NewClient();
        (await ApiLoginAsync(client, user.Email!, Password)).StatusCode.Should().Be(HttpStatusCode.OK);

        await RunAsync(service => service.DeactivateAsync(user.Id, "teste de login pela API", Moderator));

        var refused = await ApiLoginAsync(client, user.Email!, Password);
        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("inativa");

        await RunAsync(service => service.ActivateAsync(user.Id, Moderator));
        (await ApiLoginAsync(client, user.Email!, Password)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnInactiveAccount_CannotLogInThroughTheSite_ButOnlyWhoKnowsThePasswordIsToldWhy()
    {
        var user = await CreateUserAsync("site-login");
        await RunAsync(service => service.DeactivateAsync(user.Id, "teste de login pelo site", Moderator));
        using var client = NewClient();

        var rightPassword = await CookieAuth.PostLoginAsync(client, user.Email!, Password);
        var wrongPassword = await CookieAuth.PostLoginAsync(client, user.Email!, "senha-errada-nunca-vale-1");

        rightPassword.StatusCode.Should().Be(HttpStatusCode.OK, "a página volta com o aviso, sem sessão");
        // O Razor escreve os acentos como entidades HTML: compara o texto que o navegador mostraria
        WebUtility.HtmlDecode(await rightPassword.Content.ReadAsStringAsync()).Should().Contain("sua conta está inativa");
        WebUtility.HtmlDecode(await wrongPassword.Content.ReadAsStringAsync()).Should().Contain("e-mail ou senha inválidos").And.NotContain("inativa");
    }

    [Fact]
    public async Task ATokenAlreadyIssued_StopsWorkingWhenTheAccountIsDeactivated()
    {
        var email = $"token.{Guid.NewGuid():N}@teste.com";
        using var client = NewClient();
        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate", Name = "Pessoa do Token", Email = email, Password = Password, ConfirmPassword = Password
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var token = (await register.Content.ReadFromJsonAsync<UserLoginResponse>())!.AccessToken;

        async Task<HttpStatusCode> CallAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/candidates/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return (await client.SendAsync(request)).StatusCode;
        }

        (await CallAsync()).Should().Be(HttpStatusCode.OK);
        var userId = (await WithServicesAsync(async s => await s.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email)))!.Id;

        await RunAsync(service => service.DeactivateAsync(userId, "token ainda válido", Moderator));

        (await CallAsync()).Should().Be(HttpStatusCode.Unauthorized);

        await RunAsync(service => service.ActivateAsync(userId, Moderator));
        (await CallAsync()).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ASessionAlreadyOpenOnTheSite_FallsWhenTheAccountIsDeactivated()
    {
        var user = await CreateUserAsync("sessao");
        using var client = NewClient();
        await CookieAuth.LoginAsync(client, user.Email!, Password);
        (await client.GetAsync("/conta")).StatusCode.Should().Be(HttpStatusCode.OK);

        await RunAsync(service => service.DeactivateAsync(user.Id, "sessão aberta", Moderator));

        var after = await client.GetAsync("/conta");
        after.StatusCode.Should().Be(HttpStatusCode.Redirect);
        after.Headers.Location!.ToString().Should().Contain("/entrar");
    }

    // ---------------------------------------------------------------- demais ações

    [Fact]
    public async Task Unlock_ReleasesAnAccountLockedByWrongPasswords_AndRefusesWhenItIsNotLocked()
    {
        var user = await CreateUserAsync("desbloqueio");
        using var client = NewClient();
        (await RunAsync(service => service.UnlockAsync(user.Id, Moderator))).Message.Should().Contain("não está bloqueada");

        for (var attempt = 1; attempt <= 5; attempt++)
            await ApiLoginAsync(client, user.Email!, "errada-" + attempt);
        (await ApiLoginAsync(client, user.Email!, Password)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var listed = await WithServicesAsync(s => s.GetRequiredService<AdminAccountService>().ListAsync(new AdminAccountFilter(user.Email, null, "locked"), 1, 20));
        listed.Items.Should().ContainSingle(row => row.Id == user.Id && row.IsLockedOut);

        (await RunAsync(service => service.UnlockAsync(user.Id, Moderator))).Status.Should().Be(EResultStatus.Success);
        (await ApiLoginAsync(client, user.Email!, Password)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AuditOfAsync(user.Email!)).Should().Contain(entry => entry.Action == AdminAuditActions.AccountUnlocked);
    }

    [Fact]
    public async Task ConfirmEmail_MarksTheAccountAndTheProfile_AndRefusesTheSecondTime()
    {
        var user = await CreateUserAsync("confirmar", confirmed: false);
        var candidateId = Id.Create(Guid.NewGuid());
        await using (var context = await _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync())
        {
            context.Candidates.Add(new Candidate { Id = candidateId, TenantId = "default", Name = "Sem confirmar", Email = user.Email!, IsEmailVerified = false });
            await context.SaveChangesAsync();
        }

        (await RunAsync(service => service.ConfirmEmailAsync(user.Id, Moderator))).Status.Should().Be(EResultStatus.Success);

        (await ReloadAsync(user.Id)).EmailConfirmed.Should().BeTrue();
        await using (var context = await _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync())
            (await context.Candidates.AsNoTracking().SingleAsync(candidate => candidate.Id == candidateId)).IsEmailVerified.Should().BeTrue();
        (await RunAsync(service => service.ConfirmEmailAsync(user.Id, Moderator))).Message.Should().Contain("já está confirmado");
    }

    [Fact]
    public async Task RevokeSessions_ChangesTheSecurityStamp()
    {
        var user = await CreateUserAsync("sessoes");
        var before = (await ReloadAsync(user.Id)).SecurityStamp;

        (await RunAsync(service => service.RevokeSessionsAsync(user.Id, Moderator))).Status.Should().Be(EResultStatus.Success);

        (await ReloadAsync(user.Id)).SecurityStamp.Should().NotBe(before);
        (await AuditOfAsync(user.Email!)).Should().Contain(entry => entry.Action == AdminAuditActions.AccountSessionsRevoked);
    }

    [Fact]
    public async Task ResetTwoFactor_TurnsItOff_ClearsTheRecoveryCodes_AndRefusesForYourselfOrWhenItIsOff()
    {
        var user = await CreateUserAsync("doisfatores");
        (await RunAsync(service => service.ResetTwoFactorAsync(user.Id, Moderator))).Message.Should().Contain("não usa");

        await WithServicesAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var current = (await users.FindByIdAsync(user.Id.ToString()))!;
            await users.SetTwoFactorEnabledAsync(current, true);
            await users.GenerateNewTwoFactorRecoveryCodesAsync(current, 5);
            return true;
        });

        (await RunAsync(service => service.ResetTwoFactorAsync(user.Id, new AdminActor(user.Id.ToString(), user.Email!)))).Message.Should().Contain("própria conta");
        (await RunAsync(service => service.ResetTwoFactorAsync(user.Id, Moderator))).Status.Should().Be(EResultStatus.Success);

        await WithServicesAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var current = (await users.FindByIdAsync(user.Id.ToString()))!;
            current.TwoFactorEnabled.Should().BeFalse();
            (await users.CountRecoveryCodesAsync(current)).Should().Be(0);
            return true;
        });
    }

    // ---------------------------------------------------------------- lista, busca, filtros e contagens

    [Fact]
    public async Task List_SearchesByNameOrEmail_Filters_AndCounts()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var active = await CreateUserAsync("lista-ativa-" + tag);
        var inactive = await CreateUserAsync("lista-inativa-" + tag);
        var unconfirmed = await CreateUserAsync("lista-sem-email-" + tag, confirmed: false);
        await RunAsync(service => service.DeactivateAsync(inactive.Id, "para a lista", Moderator));
        await CreateAdvertiserWithJobsAsync(active.Email!);

        Task<AdminAccountPage> ListAsync(string? term = null, string? type = null, string? status = null, int page = 1, int size = 20) =>
            WithServicesAsync(s => s.GetRequiredService<AdminAccountService>().ListAsync(new AdminAccountFilter(term, type, status), page, size));

        (await ListAsync(tag)).Items.Select(row => row.Id).Should().BeEquivalentTo([active.Id, inactive.Id, unconfirmed.Id]);
        (await ListAsync(tag, status: "inactive")).Items.Should().ContainSingle().Which.Id.Should().Be(inactive.Id);
        (await ListAsync(tag, status: "active")).Items.Select(row => row.Id).Should().BeEquivalentTo([active.Id, unconfirmed.Id]);
        (await ListAsync(tag, status: "unconfirmed")).Items.Should().ContainSingle().Which.Id.Should().Be(unconfirmed.Id);
        (await ListAsync(tag, type: "advertiser")).Items.Should().ContainSingle().Which.Id.Should().Be(active.Id);
        (await ListAsync(tag.ToUpperInvariant())).TotalCount.Should().Be(3, "a busca ignora maiúsculas");

        var first = await ListAsync(tag, size: 2);
        first.Items.Should().HaveCount(2);
        first.TotalPages.Should().Be(2);
        (await ListAsync(tag, page: 2, size: 2)).Items.Should().ContainSingle();

        var row = (await ListAsync(inactive.Email)).Items.Single();
        row.IsActive.Should().BeFalse();
        row.DeactivationReason.Should().Be("para a lista");
        row.DeactivatedBy.Should().Be("moderador@monburan.com.br");
        (await ListAsync(active.Email)).Items.Single().ProfileType.Should().Be("advertiser");

        var counts = first.Counts;
        counts.Total.Should().BeGreaterThanOrEqualTo(3);
        counts.Inactive.Should().BeGreaterThanOrEqualTo(1);
        counts.Active.Should().Be(counts.Total - counts.Inactive);
    }

    [Fact]
    public async Task Get_ReturnsTheSameRowShapeAsTheList_OrNullForAnUnknownId()
    {
        var user = await CreateUserAsync("uma-conta");

        var row = await WithServicesAsync(s => s.GetRequiredService<AdminAccountService>().GetAsync(user.Id));

        row.Should().NotBeNull();
        row!.Email.Should().Be(user.Email);
        row.IsActive.Should().BeTrue();
        row.EmailConfirmed.Should().BeTrue();
        (await WithServicesAsync(s => s.GetRequiredService<AdminAccountService>().GetAsync(Guid.NewGuid()))).Should().BeNull();
    }
}
