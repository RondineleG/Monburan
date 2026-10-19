using FluentAssertions;

using Microsoft.Extensions.Configuration;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class AiSecretReaderTests
{
    private static AiSecretReader Reader(params (string Name, string? Value)[] settings) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(setting => setting.Name, setting => setting.Value))
            .Build());

    [Fact]
    public void Get_ReturnsTheTrimmedValueOfAnAiKeyVariable()
    {
        var reader = Reader(("AI_KEY_OPENROUTER", "  valor-da-chave \n"));

        reader.Get("AI_KEY_OPENROUTER").Should().Be("valor-da-chave");
        reader.IsSet("AI_KEY_OPENROUTER").Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Get_ReturnsNull_WhenTheVariableIsEmptyOrBlank(string? value)
    {
        var reader = Reader(("AI_KEY_VAZIA", value));

        reader.Get("AI_KEY_VAZIA").Should().BeNull();
        reader.IsSet("AI_KEY_VAZIA").Should().BeFalse();
    }

    [Fact]
    public void Get_ReturnsNull_WhenTheVariableDoesNotExist()
    {
        var reader = Reader(("AI_KEY_OUTRA", "x"));

        reader.Get("AI_KEY_INEXISTENTE").Should().BeNull();
        reader.IsSet("AI_KEY_INEXISTENTE").Should().BeFalse();
    }

    [Theory]
    [InlineData("Jwt__SigningKey")]
    [InlineData("Jwt:SigningKey")]
    [InlineData("ConnectionStrings:DefaultConnection")]
    [InlineData("POSTGRES_PASSWORD")]
    [InlineData("SMTP_PASSWORD")]
    [InlineData("ai_key_minuscula")]
    [InlineData("AI_KEY_")]
    [InlineData("AI_KEY_COM-HIFEN")]
    [InlineData("AI_KEY_COM:DOISPONTOS")]
    [InlineData("AI_KEY_COM_QUEBRA\n")]
    [InlineData(" AI_KEY_COM_ESPACO")]
    public void Get_NeverReadsVariablesOutsideTheAllowList_EvenWhenTheyHaveAValue(string name)
    {
        // Sem isso, um provedor apontando para um segredo qualquer faria o servidor mandá-lo a um endereço escolhido
        var reader = Reader((name, "segredo-que-nunca-pode-sair"));

        reader.Get(name).Should().BeNull();
        reader.IsSet(name).Should().BeFalse();
    }

    [Fact]
    public void Get_RejectsANameLongerThanTheLimit()
    {
        var name = "AI_KEY_" + new string('A', 61);
        var reader = Reader((name, "x"));

        reader.Get(name).Should().BeNull();
    }
}
