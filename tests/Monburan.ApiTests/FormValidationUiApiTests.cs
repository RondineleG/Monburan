using System.Net;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc.Testing;

namespace Monburan.ApiTests;

public class FormValidationUiApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly MonburanApiFactory _factory;

    public FormValidationUiApiTests(MonburanApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_DescribesValidationErrorsAtTheirFields()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await CookieAuth.PostLoginAsync(client, "endereco-invalido", "");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("aria-describedby=\"login-email-error\"")
            .And.Contain("id=\"login-email-error\"")
            .And.Contain("Informe um e-mail válido");
        html.Should().Contain("aria-describedby=\"login-password-error\"")
            .And.Contain("id=\"login-password-error\"")
            .And.Contain("Informe sua senha");
    }

    [Fact]
    public async Task AddressForm_HasAValidationTargetForEveryField()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        await CookieAuth.LoginAsync(client, "joao.silva@email.com", "Monburan123");

        var response = await client.GetAsync("/enderecos/novo");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var field in new[] { "zipcode", "street", "number", "complement", "district", "city", "state", "country" })
        {
            html.Should().Contain($"aria-describedby=\"{field}-error\"");
            html.Should().Contain($"id=\"{field}-error\"");
        }
    }
}
