using System.Net;
using System.Text.RegularExpressions;

using FluentAssertions;

namespace Monburan.ApiTests;

/// <summary>Entra pelo formulário de login do site (antiforgery e cookie de verdade), como um navegador faria.</summary>
internal static partial class CookieAuth
{
    /// <summary>O cliente precisa guardar cookies e não seguir redirecionamentos.</summary>
    public static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string email, string password)
    {
        var page = await client.GetStringAsync("/Account/Login");
        var token = AntiforgeryToken().Match(page).Groups[1].Value;
        token.Should().NotBeNullOrEmpty("a página de login traz o token antiforgery");

        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "false",
            ["_handler"] = "login",
            ["__RequestVerificationToken"] = token
        }));
    }

    public static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await PostLoginAsync(client, email, password);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, $"login de {email}: {await response.Content.ReadAsStringAsync()}");
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
