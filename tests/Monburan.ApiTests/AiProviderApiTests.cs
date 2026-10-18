using FluentAssertions;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;

namespace Monburan.ApiTests;

/// <summary>Provedores de IA no app de verdade: o que a inicialização cadastra e a configuração que protege a rede.</summary>
public class AiProviderApiTests : IClassFixture<MonburanApiFactory>
{
    private readonly MonburanApiFactory _factory;

    public AiProviderApiTests(MonburanApiFactory factory) => _factory = factory;

    [Fact]
    public async Task OnTheFirstRun_TheAppRegistersTheKnownProviders_WithXiaomiAsTheEnabledDefault()
    {
        using var scope = _factory.Services.CreateScope();
        var providers = (await scope.ServiceProvider.GetRequiredService<IAiProviderRepository>().ListAsync()).Data!.ToList();

        providers.Select(provider => provider.Name).Should().Contain(["Xiaomi MiMo", "DeepSeek", "Anthropic (Claude)"]);
        var standard = providers.Should().ContainSingle(provider => provider.IsDefault).Which;
        standard.Name.Should().Be("Xiaomi MiMo");
        standard.IsEnabled.Should().BeTrue();
        providers.Where(provider => provider.Id != standard.Id).Should().OnlyContain(provider => !provider.IsEnabled);
        providers.Should().OnlyContain(provider => provider.ApiKeyEnvVar.StartsWith("AI_KEY_"));
    }

    [Fact]
    public void TheCatalogIsWarmAtStartup_SoTheFirstRequestAlreadyKnowsTheDefault()
    {
        var catalog = _factory.Services.GetRequiredService<IAiProviderCatalog>();

        catalog.Default.Should().NotBeNull();
        catalog.Default!.Name.Should().Be("Xiaomi MiMo");
        catalog.Default.Protocol.Should().Be(EAiProtocol.OpenAiCompatible);
        catalog.All.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Production_BlocksPrivateNetworksForProviders_AndOnlyDevelopmentAllowsThem()
    {
        var root = _factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;

        var production = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "appsettings.json")).Build();
        var development = new ConfigurationBuilder().AddJsonFile(Path.Combine(root, "appsettings.Development.json")).Build();

        production.GetValue<bool>("Ai:AllowPrivateNetworks").Should().BeFalse();
        development.GetValue<bool>("Ai:AllowPrivateNetworks").Should().BeTrue();
    }

    [Fact]
    public void TheAiSettingsSection_HoldsOnlyGeneralLimits_NeverProviderDataOrKeys()
    {
        // Endereço, modelo e variável da chave vivem no banco (tela do administrador); a chave, só no ambiente do servidor
        var root = _factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;

        foreach (var file in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            var path = Path.Combine(root, file);

            new ConfigurationBuilder().AddJsonFile(path).Build().GetSection("Ai").GetChildren()
                .Select(child => child.Key).Where(key => key != "//")
                .Should().BeSubsetOf(["MaxRequestsPerHour", "AllowPrivateNetworks", "CatalogRefreshSeconds"], because: file);

            var text = File.ReadAllText(path);
            foreach (var host in new[] { "xiaomimimo", "openrouter.ai", "api.openai.com", "api.anthropic.com", "deepseek.com" })
                text.Should().NotContain(host, because: $"{file} não guarda endereço de provedor");
        }
    }
}
