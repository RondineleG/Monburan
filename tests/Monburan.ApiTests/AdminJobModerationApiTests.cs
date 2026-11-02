using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Services;
using Monburan.Core.UseCases.Admin;

namespace Monburan.ApiTests;

/// <summary>
/// Moderação das vagas de ponta a ponta: o que a administração faz tira a vaga do ar para todos, a empresa dona vê o motivo e
/// é avisada, e a vaga fora do ar não vaza pela API nem pelo site.
/// </summary>
public class AdminJobModerationApiTests : IClassFixture<MonburanApiFactory>
{
    private const string Password = "Monburan123";
    private const string OwnerEmail = "contato@techsolutions.com";
    private const string OtherCompanyEmail = "rh@marketingdigital.com";
    private static readonly AdminActor Moderator = new("99999999-9999-9999-9999-999999999999", "moderador@monburan.com.br");

    private readonly MonburanApiFactory _factory;
    private readonly HttpClient _client;

    public AdminJobModerationApiTests(MonburanApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> TokenAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest { Email = email, Password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!.AccessToken;
    }

    private async Task<JobResponse> CreateJobAsync(string title)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs")
        {
            Content = JsonContent.Create(new CreateJobRequest
            {
                Title = title, Description = "Vaga criada pelos testes de moderação.", ExpirationDate = DateTime.UtcNow.AddDays(20),
                NumberOfOpenings = 1, Type = "Remote", IsRemote = true, SalaryCurrency = "BRL", SalaryDisplay = true
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(OwnerEmail));
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JobResponse>())!;
    }

    private async Task<HttpStatusCode> GetJobAsync(Guid id, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/jobs/{id}");
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await _client.SendAsync(request)).StatusCode;
    }

    private async Task<RequestResultSummary> ModerateAsync(Guid jobId, EJobStatus status, string? reason = null, DateTime? renew = null)
    {
        using var scope = _factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<AdminChangeJobStatusUseCase>()
            .ExecuteAsync(new AdminChangeJobStatusRequest(jobId, status, reason, renew, Moderator));
        return new RequestResultSummary(result.Status, result.Message);
    }

    private sealed record RequestResultSummary(EResultStatus Status, string Message);

    private async Task<List<JobResponse>> PublicListAsync() =>
        (await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?size=100"))!.Jobs.ToList();

    private async Task<List<JobResponse>> MineAsync(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/jobs/mine?size=100");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ListJobsResponse>())!.Jobs.ToList();
    }

    private HttpClient SiteClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    [Fact]
    public async Task AHiddenJob_LeavesThePublicList_AndTheOwnerSeesWhyInTheirOwnList()
    {
        var job = await CreateJobAsync("Vaga que será ocultada " + Guid.NewGuid().ToString("N")[..6]);
        (await PublicListAsync()).Should().Contain(item => item.Id == job.Id);

        var result = await ModerateAsync(job.Id, EJobStatus.Hidden, "Remuneração não informada de forma clara");

        result.Status.Should().Be(EResultStatus.Success, result.Message);
        (await PublicListAsync()).Should().NotContain(item => item.Id == job.Id);

        var mine = (await MineAsync(await TokenAsync(OwnerEmail))).Single(item => item.Id == job.Id);
        mine.Status.Should().Be("Hidden");
        mine.ModerationReason.Should().Be("Remuneração não informada de forma clara");
    }

    [Fact]
    public async Task CompanyProfile_ShowsOnlyPublicJobs()
    {
        var active = await CreateJobAsync("Vaga pública da empresa " + Guid.NewGuid().ToString("N")[..6]);
        var paused = await CreateJobAsync("Vaga pausada da empresa " + Guid.NewGuid().ToString("N")[..6]);
        var hidden = await CreateJobAsync("Vaga oculta da empresa " + Guid.NewGuid().ToString("N")[..6]);
        await ModerateAsync(paused.Id, EJobStatus.Paused);
        await ModerateAsync(hidden.Id, EJobStatus.Hidden, "Revisão pendente");

        using var site = SiteClient();
        var response = await site.GetAsync($"/empresas/{active.AdvertiserId}");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(active.Title);
        html.Should().NotContain(paused.Title).And.NotContain(hidden.Title);
    }

    [Fact]
    public async Task ANonPublicJob_DoesNotLeakThroughTheDetailEndpoint_ExceptToItsOwner()
    {
        var job = await CreateJobAsync("Vaga que não pode vazar " + Guid.NewGuid().ToString("N")[..6]);
        (await GetJobAsync(job.Id)).Should().Be(HttpStatusCode.OK);

        await ModerateAsync(job.Id, EJobStatus.Rejected, "Texto discriminatório no anúncio");

        (await GetJobAsync(job.Id)).Should().Be(HttpStatusCode.NotFound, "visitante");
        (await GetJobAsync(job.Id, await TokenAsync("joao.silva@email.com"))).Should().Be(HttpStatusCode.NotFound, "candidato");
        (await GetJobAsync(job.Id, await TokenAsync(OtherCompanyEmail))).Should().Be(HttpStatusCode.NotFound, "outra empresa");

        using var ownerRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/jobs/{job.Id}");
        ownerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(OwnerEmail));
        var owner = await _client.SendAsync(ownerRequest);
        owner.StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.Content.ReadFromJsonAsync<JobResponse>())!.ModerationReason.Should().Be("Texto discriminatório no anúncio");
    }

    [Fact]
    public async Task APausedJob_IsAlsoOnlyForItsOwner_AndAnActiveOrExpiredOneStaysPublic()
    {
        var paused = await CreateJobAsync("Vaga pausada " + Guid.NewGuid().ToString("N")[..6]);
        var active = await CreateJobAsync("Vaga ativa " + Guid.NewGuid().ToString("N")[..6]);
        await ModerateAsync(paused.Id, EJobStatus.Paused);

        (await GetJobAsync(paused.Id)).Should().Be(HttpStatusCode.NotFound);
        (await GetJobAsync(paused.Id, await TokenAsync(OwnerEmail))).Should().Be(HttpStatusCode.OK);
        (await GetJobAsync(active.Id)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheCompany_IsToldInsideThePlatform_AndCannotReactivateWhatTheModerationHid()
    {
        var job = await CreateJobAsync("Vaga com aviso " + Guid.NewGuid().ToString("N")[..6]);
        await ModerateAsync(job.Id, EJobStatus.Hidden, "Conteúdo repetido de outra vaga");
        var token = await TokenAsync(OwnerEmail);

        using var notificationsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications/mine?size=50");
        notificationsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var notifications = await (await _client.SendAsync(notificationsRequest)).Content.ReadAsStringAsync();
        notifications.Should().Contain("Sua vaga foi ocultada pela moderação").And.Contain("Conteúdo repetido de outra vaga");

        using var scope = _factory.Services.CreateScope();
        var own = await scope.ServiceProvider.GetRequiredService<Monburan.Core.UseCases.Jobs.ChangeJobStatusUseCase>()
            .ExecuteAsync(new Monburan.Core.UseCases.Jobs.ChangeJobStatusRequest(job.Id, job.AdvertiserId!.Value, EJobStatus.Active));
        own.Status.Should().Be(EResultStatus.DomainError);
        own.Message.Should().Contain("não pode ser reativada");
    }

    [Fact]
    public async Task ReactivatingAJob_BringsItBack_AndClearsTheReason()
    {
        var job = await CreateJobAsync("Vaga que volta " + Guid.NewGuid().ToString("N")[..6]);
        await ModerateAsync(job.Id, EJobStatus.Hidden, "Revisão de conteúdo em andamento");

        var result = await ModerateAsync(job.Id, EJobStatus.Active);

        result.Status.Should().Be(EResultStatus.Success, result.Message);
        (await PublicListAsync()).Should().Contain(item => item.Id == job.Id);
        var mine = (await MineAsync(await TokenAsync(OwnerEmail))).Single(item => item.Id == job.Id);
        mine.Status.Should().Be("Active");
        mine.ModerationReason.Should().BeNull();
        (await GetJobAsync(job.Id)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ADeletedJob_VanishesEverywhere_AndTheDeletionIsAudited()
    {
        var job = await CreateJobAsync("Vaga que será removida " + Guid.NewGuid().ToString("N")[..6]);

        using (var scope = _factory.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<AdminDeleteJobUseCase>()
                .ExecuteAsync(new AdminDeleteJobRequest(job.Id, "Oferta fraudulenta confirmada", Moderator));
            result.Status.Should().Be(EResultStatus.Success, result.Message);
        }

        (await GetJobAsync(job.Id)).Should().Be(HttpStatusCode.NotFound);
        (await PublicListAsync()).Should().NotContain(item => item.Id == job.Id);
        (await MineAsync(await TokenAsync(OwnerEmail))).Should().NotContain(item => item.Id == job.Id);

        using var auditScope = _factory.Services.CreateScope();
        var entries = (await auditScope.ServiceProvider.GetRequiredService<IAdminAuditLog>()
            .ListAsync(new AdminAuditFilter(SearchTerm: job.Title), 1, 20)).Data.ToList();
        entries.Should().Contain(entry => entry.Action == AdminAuditActions.JobDeleted && entry.Details!.Contains("Oferta fraudulenta"));
    }

    [Fact]
    public async Task TheJobPage_ShowsAnyJobToAdminsAndOwners_ButIs404ForEveryoneElse()
    {
        var job = await CreateJobAsync("Vaga da página " + Guid.NewGuid().ToString("N")[..6]);
        await ModerateAsync(job.Id, EJobStatus.Hidden, "Faltam informações obrigatórias");

        (await _client.GetAsync($"/jobs/{job.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "visitante");

        using var candidate = SiteClient();
        await CookieAuth.LoginAsync(candidate, "joao.silva@email.com", Password);
        (await candidate.GetAsync($"/jobs/{job.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "candidato");

        using var admin = SiteClient();
        await CookieAuth.LoginAsync(admin, "admin@monburan.com.br", Password);
        var adminPage = await admin.GetAsync($"/jobs/{job.Id}");
        adminPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminHtml = WebUtility.HtmlDecode(await adminPage.Content.ReadAsStringAsync());
        adminHtml.Should().Contain("você a vê como administrador").And.Contain("Faltam informações obrigatórias");

        using var owner = SiteClient();
        await CookieAuth.LoginAsync(owner, OwnerEmail, Password);
        var ownerPage = await owner.GetAsync($"/jobs/{job.Id}");
        ownerPage.StatusCode.Should().Be(HttpStatusCode.OK);
        WebUtility.HtmlDecode(await ownerPage.Content.ReadAsStringAsync()).Should().Contain("Motivo informado pela moderação").And.Contain("Faltam informações obrigatórias");
    }

    [Fact]
    public async Task TheAdminJobsPage_IsClosedToVisitorsAndToNonAdmins()
    {
        using var anonymous = SiteClient();
        var visitor = await anonymous.GetAsync("/admin/vagas");
        visitor.StatusCode.Should().Be(HttpStatusCode.Redirect);
        visitor.Headers.Location!.ToString().Should().Contain("/entrar");

        using var company = SiteClient();
        await CookieAuth.LoginAsync(company, OwnerEmail, Password);
        var denied = await company.GetAsync("/admin/vagas");
        denied.StatusCode.Should().Be(HttpStatusCode.Redirect);
        denied.Headers.Location!.ToString().Should().Contain("/acesso-negado");

        using var admin = SiteClient();
        await CookieAuth.LoginAsync(admin, "admin@monburan.com.br", Password);
        (await admin.GetAsync("/admin/vagas")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/admin/contas")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/admin/auditoria")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
