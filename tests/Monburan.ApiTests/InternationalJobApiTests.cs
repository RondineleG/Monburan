using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Services;
using Monburan.Web.Services;

namespace Monburan.ApiTests;

/// <summary>Campos de carreira internacional da vaga atrás da flag carreira-internacional, conferida no servidor.</summary>
public sealed class InternationalJobApiTests : IClassFixture<MonburanApiFactory>
{
    private const string Password = "Monburan123";
    private static readonly AdminActor Actor = new(Guid.NewGuid().ToString(), "admin@monburan.com.br");

    private readonly MonburanApiFactory _factory;
    private readonly HttpClient _client;

    public InternationalJobApiTests(MonburanApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterCompanyAsync()
    {
        var email = $"empresa-intl-{Guid.NewGuid():N}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "advertiser",
            Name = "Empresa Internacional",
            Email = email,
            Password = Password,
            ConfirmPassword = Password
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<UserLoginResponse>())!.AccessToken;
    }

    private static CreateJobRequest Request(string title) => new()
    {
        Title = title,
        Description = "Vaga remota para quem mora em Portugal ou Espanha.",
        ExpirationDate = DateTime.UtcNow.AddDays(30),
        NumberOfOpenings = 1,
        Type = "Remote",
        IsRemote = true,
        Country = "pt",
        SalaryMin = 3000,
        SalaryMax = 4000,
        SalaryCurrency = "eur",
        RemoteScope = ERemoteScope.Restricted,
        EligibleCountries = "pt, es",
        OffersRelocation = false,
        SponsorsVisa = null
    };

    private async Task<JobResponse> CreateAsync(string token, CreateJobRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs") { Content = JsonContent.Create(request) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JobResponse>())!;
    }

    [Fact]
    public async Task NewFields_AreDroppedWithFlagOff_AndSavedWithFlagOn()
    {
        var token = await RegisterCompanyAsync();

        // Flag inexistente = desligada: país e moeda (que já existiam) valem; os campos novos são descartados
        var withoutFlag = await CreateAsync(token, Request("Vaga internacional sem flag"));
        withoutFlag.Country.Should().Be("PT");
        withoutFlag.SalaryCurrency.Should().Be("EUR");
        withoutFlag.RemoteScope.Should().BeNull();
        withoutFlag.EligibleCountries.Should().BeNull();
        withoutFlag.OffersRelocation.Should().BeNull();

        using (var scope = _factory.Services.CreateScope())
        {
            var admin = scope.ServiceProvider.GetRequiredService<FeatureFlagAdminService>();
            (await admin.SaveFlagAsync(new FeatureFlagInput(null, InternationalJobRules.FeatureKey, "Carreira internacional", true, [], null), Actor))
                .Status.Should().Be(EResultStatus.Success);
        }

        var withFlag = await CreateAsync(token, Request("Vaga internacional com flag"));
        withFlag.RemoteScope.Should().Be("Restricted");
        withFlag.EligibleCountries.Should().Be("PT,ES");
        withFlag.OffersRelocation.Should().BeFalse("\"não oferece\" é informação e não pode virar nulo");
        withFlag.SponsorsVisa.Should().BeNull("não informado continua não informado");

        var read = await _client.GetFromJsonAsync<JobResponse>($"/api/v1/jobs/{withFlag.Id}");
        read!.EligibleCountries.Should().Be("PT,ES");
    }

    [Fact]
    public async Task RestrictedRemoteWithoutCountries_IsRejected()
    {
        var token = await RegisterCompanyAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var admin = scope.ServiceProvider.GetRequiredService<FeatureFlagAdminService>();
            var flags = await admin.ListFlagsAsync();
            if (flags.All(flag => flag.Name != InternationalJobRules.FeatureKey))
                await admin.SaveFlagAsync(new FeatureFlagInput(null, InternationalJobRules.FeatureKey, "Carreira internacional", true, [], null), Actor);
        }

        var request = Request("Vaga restrita sem países");
        request.EligibleCountries = null;

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs") { Content = JsonContent.Create(request) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("EligibleCountries");
    }
}
