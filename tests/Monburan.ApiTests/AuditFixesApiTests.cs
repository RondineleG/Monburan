using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.ApiTests;

/// <summary>
/// Regressões das correções da auditoria de produção (docs/AUDIT-REPORT.md).
/// </summary>
public class AuditFixesApiTests : IClassFixture<MonburanApiFactory>
{
    private const string Password = "Monburan123";
    private readonly HttpClient _client;

    public AuditFixesApiTests(MonburanApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UnknownApiRoute_ReturnsProblemDetailsInPortuguese()
    {
        var response = await _client.GetAsync("/api/v1/rota-que-nao-existe");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("title").GetString().Should().Be("Recurso não encontrado");
    }

    [Fact]
    public async Task MissingToken_ReturnsProblemDetails401_InPortuguese()
    {
        var response = await _client.GetAsync("/api/v1/candidates/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("title").GetString().Should().Be("Não autenticado");
        problem.GetProperty("detail").GetString().Should().Contain("Entre novamente");
    }

    [Fact]
    public async Task ValidationProblem_HasPortugueseTitle()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "X",
            Email = "invalido",
            Password = "1",
            ConfirmPassword = "2"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("title").GetString().Should().Be("Erro de validação");
    }

    [Fact]
    public async Task PublicJobEndpoints_HideSalary_WhenAdvertiserOptsOut()
    {
        var token = await LoginAsync("contato@techsolutions.com");
        var job = await CreateJobAsync(token, "Vaga com salário oculto", salaryDisplay: false);

        var publicDetail = await _client.GetFromJsonAsync<JobResponse>($"/api/v1/jobs/{job.Id}");
        publicDetail!.SalaryMin.Should().BeNull();
        publicDetail.SalaryMax.Should().BeNull();

        var publicList = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?size=100");
        publicList!.Jobs.Single(j => j.Id == job.Id).SalaryMin.Should().BeNull();

        // O dono continua vendo a faixa na própria gestão
        var mine = await SendAsync<ListJobsResponse>(HttpMethod.Get, "/api/v1/jobs/mine?size=100", token);
        mine.Jobs.Single(j => j.Id == job.Id).SalaryMin.Should().Be(5000);
    }

    [Fact]
    public async Task SalaryFilterAndSort_IgnoreHiddenSalaries_AndUseTheFloorWhenThereIsNoCeiling()
    {
        var token = await LoginAsync("contato@techsolutions.com");
        var hidden = await CreateJobAsync(token, "Vaga com salário oculto no filtro", salaryDisplay: false);
        var floorOnly = await CreateJobAsync(token, "Vaga a partir de 9 mil", salaryMin: 9000, salaryMax: null);

        // Filtro: o teto oculto (7.000) alcançaria 6.500, mas valor oculto não entra na conta
        var atLeast6500 = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?size=100&salaryMin=6500");
        atLeast6500!.Jobs.Should().NotContain(j => j.Id == hidden.Id);

        // Sem teto, vale o piso: "a partir de 9.000" paga ao menos 8.000
        var atLeast8000 = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?size=100&salaryMin=8000");
        atLeast8000!.Jobs.Should().Contain(j => j.Id == floorOnly.Id);

        // Ordenação: o valor de referência decide; a vaga com salário oculto fica depois de todas as que o exibem
        var sorted = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?size=100&sortBy=salary&sortDescending=true");
        var jobs = sorted!.Jobs.ToList();
        var hiddenIndex = jobs.FindIndex(j => j.Id == hidden.Id);
        hiddenIndex.Should().BeGreaterThanOrEqualTo(0);
        jobs.Skip(hiddenIndex).Should().OnlyContain(j => !j.SalaryMin.HasValue && !j.SalaryMax.HasValue);
        jobs.FindIndex(j => j.Id == floorOnly.Id).Should().BeLessThan(hiddenIndex);
    }

    [Fact]
    public async Task MineJobs_IncludesPausedJobs_WhilePublicListDoesNot()
    {
        var token = await LoginAsync("contato@techsolutions.com");

        var mine = await SendAsync<ListJobsResponse>(HttpMethod.Get, "/api/v1/jobs/mine?size=100", token);
        mine.Jobs.Should().Contain(j => j.Status == "Paused", "o seed tem uma vaga pausada da Tech Solutions");

        var publicList = await _client.GetFromJsonAsync<ListJobsResponse>("/api/v1/jobs?size=100");
        publicList!.Jobs.Should().OnlyContain(j => j.Status == "Active");
    }

    [Fact]
    public async Task CreateJob_BeyondFreePlanLimit_IsRejected()
    {
        var email = $"empresa-{Guid.NewGuid():N}@teste.com";
        var register = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "advertiser",
            Name = "Empresa Limite",
            Email = email,
            Password = "Senha123",
            ConfirmPassword = "Senha123"
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var token = (await register.Content.ReadFromJsonAsync<UserLoginResponse>())!.AccessToken;

        // Sem assinatura = plano Free (5 vagas ativas no seed)
        for (var i = 1; i <= 5; i++)
            await CreateJobAsync(token, $"Vaga {i} no limite");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs")
        {
            Content = JsonContent.Create(NewJob("Vaga além do limite"))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("detail").GetString().Should().Contain("5 vagas ativas");
    }

    [Fact]
    public async Task UploadPdf_WithForgedContent_IsRejected()
    {
        var token = await LoginAsync("joao.silva@email.com");
        using var content = new MultipartFormDataContent();
        var forged = new ByteArrayContent("<html><script>alert(1)</script></html>"u8.ToArray());
        forged.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(forged, "file", "curriculo.pdf");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/upload?fileType=Pdf") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("não é um PDF válido");
    }

    [Fact]
    public async Task UploadLogo_UpdatesTheAdvertiserProfile()
    {
        var token = await LoginAsync("rh@marketingdigital.com");
        using var content = new MultipartFormDataContent();
        var png = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. "logo"u8]);
        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(png, "file", "logo.png");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/advertisers/logo") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var profile = await SendAsync<AdvertiserResponse>(HttpMethod.Get, "/api/v1/advertisers/me", token);
        profile.LogoUrl.Should().StartWith("/uploads/").And.EndWith(".png");
    }

    [Fact]
    public async Task ResumeUpdate_WithSections_CanBeSavedAndEditedAgain()
    {
        // Antes: Update() de grafo desanexado com coleções owned lançava
        // "shadow key property 'Education.Id' is unknown" — editar currículo dava erro
        var token = await LoginAsync("maria.santos@email.com");
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/resumes")
        {
            Content = JsonContent.Create(new CreateResumeRequest { Title = "Currículo com seções", Summary = "Resumo" })
        };
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var created = await (await _client.SendAsync(create)).Content.ReadFromJsonAsync<ResumeResponse>();

        var first = new UpdateResumeRequest
        {
            Title = "Currículo com seções",
            Summary = "Resumo",
            ExperienceHistory = [new ExperienceRequest { Company = "ACME", Position = "Analista", StartDate = new DateTime(2020, 1, 1) }],
            EducationHistory = [new EducationRequest { Institution = "USP", Course = "Computação", StartDate = new DateTime(2015, 2, 1) }],
            Languages = [new LanguageRequest { Language = "Inglês" }],
            Skills = [new SkillRequest { Skill = "C#", Level = 4, YearsOfExperience = 5 }, new SkillRequest { Skill = "SQL", Level = 3 }]
        };
        (await PutAsync($"/api/v1/resumes/{created!.Id}", first, token)).StatusCode.Should().Be(HttpStatusCode.OK);

        var second = new UpdateResumeRequest
        {
            Title = "Currículo com seções v2",
            Summary = "Resumo",
            ExperienceHistory = [new ExperienceRequest { Company = "ACME", Position = "Analista Sênior", StartDate = new DateTime(2020, 1, 1), IsCurrent = true }],
            EducationHistory = [],
            Languages = [new LanguageRequest { Language = "Inglês" }, new LanguageRequest { Language = "Espanhol" }],
            Skills = [new SkillRequest { Skill = "c#", Level = 5, YearsOfExperience = 6 }, new SkillRequest { Skill = "Azure", Level = 1 }]
        };
        (await PutAsync($"/api/v1/resumes/{created.Id}", second, token)).StatusCode.Should().Be(HttpStatusCode.OK);

        var saved = await SendAsync<ResumeResponse>(HttpMethod.Get, $"/api/v1/resumes/{created.Id}", token);
        saved.Title.Should().Be("Currículo com seções v2");
        saved.ExperienceHistory!.Should().ContainSingle(e => e.Position == "Analista Sênior" && e.IsCurrent);
        saved.EducationHistory.Should().BeEmpty();
        saved.Languages!.Select(l => l.Language).Should().BeEquivalentTo(["Inglês", "Espanhol"]);
        saved.Skills!.Select(sk => sk.Skill).Should().BeEquivalentTo(["C#", "Azure"]);
        saved.Skills!.Single(sk => sk.Skill == "C#").Level.Should().Be(5);
    }

    [Fact]
    public async Task ApplicationsAndResumes_AreScopedToTheAuthenticatedCandidate()
    {
        var joao = await LoginAsync("joao.silva@email.com");
        var maria = await LoginAsync("maria.santos@email.com");

        var joaoProfile = await SendAsync<CandidateResponse>(HttpMethod.Get, "/api/v1/candidates/me", joao);
        var mariaProfile = await SendAsync<CandidateResponse>(HttpMethod.Get, "/api/v1/candidates/me", maria);

        var joaoApplications = await SendAsync<List<ApplicationResponse>>(HttpMethod.Get, "/api/v1/applications/mine", joao);
        var mariaApplications = await SendAsync<List<ApplicationResponse>>(HttpMethod.Get, "/api/v1/applications/mine", maria);

        joaoApplications.Should().NotBeEmpty().And.OnlyContain(a => a.CandidateId == joaoProfile.Id);
        mariaApplications.Should().NotBeEmpty().And.OnlyContain(a => a.CandidateId == mariaProfile.Id);
        joaoApplications.Should().OnlyContain(a => !string.IsNullOrEmpty(a.JobTitle), "a listagem inclui o título da vaga");
    }

    [Fact]
    public async Task MarkAllNotificationsAsRead_ZeroesUnreadCount()
    {
        var token = await LoginAsync("joao.silva@email.com");

        var response = await SendRawAsync(HttpMethod.Put, "/api/v1/notifications/mine/read-all", token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var unread = await SendAsync<JsonElement>(HttpMethod.Get, "/api/v1/notifications/mine/unread-count", token);
        unread.GetProperty("count").GetInt32().Should().Be(0);
    }

    private static CreateJobRequest NewJob(string title, bool salaryDisplay = true, decimal? salaryMin = 5000, decimal? salaryMax = 7000) => new()
    {
        Title = title,
        Description = "Descrição da vaga criada pelos testes da auditoria.",
        ExpirationDate = DateTime.UtcNow.AddDays(20),
        NumberOfOpenings = 1,
        Type = "Remote",
        IsRemote = true,
        SalaryMin = salaryMin,
        SalaryMax = salaryMax,
        SalaryCurrency = "BRL",
        SalaryDisplay = salaryDisplay
    };

    private async Task<JobResponse> CreateJobAsync(
        string token, string title, bool salaryDisplay = true, decimal? salaryMin = 5000, decimal? salaryMax = 7000)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs")
        {
            Content = JsonContent.Create(NewJob(title, salaryDisplay, salaryMin, salaryMax))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JobResponse>())!;
    }

    private async Task<string> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest { Email = email, Password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!.AccessToken;
    }

    private async Task<HttpResponseMessage> PutAsync<T>(string url, T body, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url, string token)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string url, string token)
    {
        var response = await SendRawAsync(method, url, token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
