using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;

namespace Monburan.ApiTests;

/// <summary>Limite de currículos do plano aplicado no servidor: o plano gratuito de candidato permite 3.</summary>
public sealed class ResumeLimitApiTests : IClassFixture<MonburanApiFactory>
{
    private const string Password = "Monburan123";
    private readonly HttpClient _client;

    public ResumeLimitApiTests(MonburanApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task FreeCandidate_CannotCreateMoreResumesThanThePlanAllows()
    {
        var register = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Candidata Gratuita",
            Email = $"candidata-{Guid.NewGuid():N}@test.com",
            Password = Password,
            ConfirmPassword = Password
        });
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var token = (await register.Content.ReadFromJsonAsync<UserLoginResponse>())!.AccessToken;

        async Task<HttpResponseMessage> CreateAsync(int number)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/resumes")
            {
                Content = JsonContent.Create(new CreateResumeRequest { Title = $"Currículo {number}" })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await _client.SendAsync(request);
        }

        for (var number = 1; number <= 3; number++)
            (await CreateAsync(number)).StatusCode.Should().Be(HttpStatusCode.Created);

        var fourth = await CreateAsync(4);
        fourth.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await fourth.Content.ReadAsStringAsync()).Should().Contain("até 3 currículos");
    }
}
