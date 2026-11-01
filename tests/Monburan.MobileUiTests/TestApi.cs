using System.Net.Http.Json;

namespace Monburan.MobileUiTests;

/// <summary>Prepara contas pela API, para o teste de tela não depender de massa fixa.</summary>
public static class TestApi
{
    public const string Password = "Monburan123";

    public static async Task<string> RegisterCandidateAsync()
    {
        using var http = new HttpClient { BaseAddress = MobileEnvironment.ApiUrl };
        var email = $"ui.mobile.{Guid.NewGuid():N}@teste.com";
        var response = await http.PostAsJsonAsync("/api/auth/register", new
        {
            accountType = "candidate",
            name = "Candidato Teste de Tela",
            email,
            password = Password,
            confirmPassword = Password
        });
        response.EnsureSuccessStatusCode();
        return email;
    }
}
