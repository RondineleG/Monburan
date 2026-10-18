using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;

using Monburan.Core.DTOs.Request;

namespace Monburan.ApiTests;

/// <summary>Política de senha (tamanho e senhas comuns) e bloqueio da conta depois de várias senhas erradas.</summary>
public class PasswordPolicyApiTests : IClassFixture<MonburanApiFactory>
{
    private const string GoodPassword = "Cavalo-Azul-Bateria-7";

    private readonly HttpClient _client;

    public PasswordPolicyApiTests(MonburanApiFactory factory) => _client = factory.CreateClient();

    private static string NewEmail(string prefix = "pessoa") => $"{prefix}.{Guid.NewGuid():N}@teste.com";

    private Task<HttpResponseMessage> RegisterAsync(string email, string password) =>
        _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            AccountType = "candidate",
            Name = "Pessoa Teste",
            Email = email,
            Password = password,
            ConfirmPassword = password
        });

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new UserLoginRequest { Email = email, Password = password });

    private static async Task<string> ProblemTextAsync(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();

    [Fact]
    public async Task Register_WithAnAcceptablePassword_Succeeds()
    {
        var response = await RegisterAsync(NewEmail(), GoodPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await ProblemTextAsync(response));
    }

    [Fact]
    public async Task Register_WithNineCharacters_IsRejected_SayingTheMinimumIsTen()
    {
        var response = await RegisterAsync(NewEmail(), "Abc12345x");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProblemTextAsync(response)).Should().Contain("10");
    }

    [Theory]
    [InlineData("Senha12345")]
    [InlineData("Password123")]
    [InlineData("Qwerty12345")]
    public async Task Register_WithACommonPassword_IsRejected(string password)
    {
        var response = await RegisterAsync(NewEmail(), password);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProblemTextAsync(response)).Should().Contain("muito comum");
    }

    [Fact]
    public async Task Register_WithAPasswordBuiltFromTheEmail_IsRejected()
    {
        var local = $"joaozinho{Guid.NewGuid():N}"[..14];
        var response = await RegisterAsync($"{local}@teste.com", $"{char.ToUpperInvariant(local[0])}{local[1..]}123");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProblemTextAsync(response)).Should().Contain("e-mail");
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_LocksTheAccount_EvenForTheRightPassword()
    {
        var email = NewEmail("bloqueio");
        (await RegisterAsync(email, GoodPassword)).StatusCode.Should().Be(HttpStatusCode.Created);

        for (var attempt = 1; attempt <= 5; attempt++)
            (await LoginAsync(email, "senha-errada-" + attempt)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var locked = await LoginAsync(email, GoodPassword);

        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await locked.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("detail").GetString().Should().Contain("blocked");
    }

    [Fact]
    public async Task Login_ASuccessfulLoginResetsTheFailureCount()
    {
        var email = NewEmail("zera");
        (await RegisterAsync(email, GoodPassword)).StatusCode.Should().Be(HttpStatusCode.Created);

        for (var round = 1; round <= 3; round++)
        {
            for (var attempt = 1; attempt <= 4; attempt++)
                (await LoginAsync(email, "errada-" + attempt)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            (await LoginAsync(email, GoodPassword)).StatusCode.Should().Be(HttpStatusCode.OK, $"rodada {round}");
        }
    }

    [Fact]
    public async Task Login_ForAnUnknownEmail_ReportsTheSameErrorAsAWrongPassword()
    {
        // Não revela se a conta existe
        var unknown = await (await LoginAsync(NewEmail("naoexiste"), GoodPassword)).Content.ReadFromJsonAsync<JsonElement>();
        var email = NewEmail("existe");
        await RegisterAsync(email, GoodPassword);
        var wrong = await (await LoginAsync(email, "errada-demais-1")).Content.ReadFromJsonAsync<JsonElement>();

        unknown.GetProperty("detail").GetString().Should().Be(wrong.GetProperty("detail").GetString());
    }
}
