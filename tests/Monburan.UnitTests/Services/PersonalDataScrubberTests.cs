using FluentAssertions;

using Monburan.Core.Services;

namespace Monburan.UnitTests.Services;

public class PersonalDataScrubberTests
{
    [Theory]
    [InlineData("Contato: ana.souza+vagas@exemplo.com.br para detalhes", "Contato: [e-mail] para detalhes")]
    [InlineData("Ligue (11) 99999-8888 ou (21) 3333-4444", "Ligue [telefone] ou [telefone]")]
    [InlineData("Whats +55 11 98888-7777", "Whats [telefone]")]
    [InlineData("Celular 98765-4321", "Celular [telefone]")]
    [InlineData("11 98765-4321", "[telefone]")]
    [InlineData("CPF 123.456.789-09 e CNPJ 12.345.678/0001-95", "CPF [documento] e CNPJ [documento]")]
    [InlineData("Documento 12345678909", "Documento [documento]")]
    [InlineData("CEP 01310-100, São Paulo", "CEP [cep], São Paulo")]
    [InlineData("Portfólio em https://github.com/ana-souza/projetos e www.ana.dev.br", "Portfólio em [link] e [link]")]
    [InlineData("Perfil linkedin.com/in/ana-souza", "Perfil [link]")]
    public void Scrub_ReplacesPersonalDataWithMarkers(string input, string expected)
    {
        PersonalDataScrubber.Scrub(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Trabalhei de 2018-2021 como analista")]
    [InlineData("Período 2019 2022 na área comercial")]
    [InlineData("Aumentei as vendas em 35% em 12 meses")]
    [InlineData("Atendi 1500 clientes e 20 equipes em 2023")]
    [InlineData("Desenvolvedor Pleno (03/2021 a atual)")]
    public void Scrub_KeepsPeriodsAndNumbersThatAreNotPersonalData(string input)
    {
        PersonalDataScrubber.Scrub(input).Should().Be(input);
    }

    [Fact]
    public void Scrub_ReturnsEmpty_ForBlankText()
    {
        PersonalDataScrubber.Scrub(null).Should().BeEmpty();
        PersonalDataScrubber.Scrub("  \n ").Should().BeEmpty();
    }
}
