using FluentAssertions;

using Monburan.Core.UseCases.Resumes;

namespace Monburan.UnitTests.UseCases.Resumes;

public class ResumeImprovementParserTests
{
    private const string Json = """
        {"summary": "Desenvolvedor full stack com 6 anos de experiência.",
         "experiences": [{"company": "Empresa Alfa", "position": "Pleno", "suggestions": ["Desenvolvi APIs REST.", "Reduzi [X%] dos chamados."]}],
         "skillsToAdd": ["Git", "REST"],
         "missingSections": ["Projetos"],
         "tips": ["Use palavras-chave."]}
        """;

    [Fact]
    public void TryParse_ReadsPlainJson()
    {
        ResumeImprovementParser.TryParse(Json, out var response).Should().BeTrue();

        response!.Summary.Should().StartWith("Desenvolvedor full stack");
        response.Experiences.Should().ContainSingle();
        response.Experiences[0].Company.Should().Be("Empresa Alfa");
        response.Experiences[0].Suggestions.Should().Equal("Desenvolvi APIs REST.", "Reduzi [X%] dos chamados.");
        response.SkillsToAdd.Should().Equal("Git", "REST");
        response.MissingSections.Should().Equal("Projetos");
        response.Tips.Should().Equal("Use palavras-chave.");
    }

    [Fact]
    public void TryParse_AcceptsCodeFencesAndSurroundingText()
    {
        var content = "Claro! Segue o resultado:\n```json\n" + Json + "\n```\nQualquer dúvida, avise.";

        ResumeImprovementParser.TryParse(content, out var response).Should().BeTrue();

        response!.Summary.Should().NotBeEmpty();
    }

    [Fact]
    public void TryParse_AcceptsSnakeCaseNamesAndTextInPlaceOfLists()
    {
        const string content = """
            {"Summary": "Resumo.", "skills_to_add": "- Git\n- Docker\n* Kubernetes", "missing-sections": ["Certificações"], "TIPS": []}
            """;

        ResumeImprovementParser.TryParse(content, out var response).Should().BeTrue();

        response!.SkillsToAdd.Should().Equal("Git", "Docker", "Kubernetes");
        response.MissingSections.Should().Equal("Certificações");
        response.Tips.Should().BeEmpty();
    }

    [Fact]
    public void TryParse_LimitsAndCleansWhatTheModelReturns()
    {
        var tips = string.Join(",", Enumerable.Range(1, 20).Select(index => $"\"dica {index}\""));
        var content = "{\"summary\": \"" + new string('a', 3000) + "\", \"tips\": [" + tips + ", \"\", \"   \"]}";

        ResumeImprovementParser.TryParse(content, out var response).Should().BeTrue();

        response!.Summary.Length.Should().BeLessThanOrEqualTo(1501);
        response.Summary.Should().EndWith("…");
        response.Tips.Should().HaveCount(8);
        response.Tips.Should().NotContain(string.Empty);
    }

    [Fact]
    public void TryParse_DropsExperiencesWithoutSuggestions()
    {
        const string content = """
            {"summary": "Ok.", "experiences": [{"company": "A", "position": "B", "suggestions": []}, {"company": "C", "position": "D", "suggestions": ["Frase."]}, "texto solto"]}
            """;

        ResumeImprovementParser.TryParse(content, out var response).Should().BeTrue();

        response!.Experiences.Should().ContainSingle(experience => experience.Company == "C");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Não consegui gerar sugestões.")]
    [InlineData("{ isto não é json }")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{}")]
    [InlineData("{\"summary\": \"\", \"experiences\": [], \"tips\": []}")]
    public void TryParse_Fails_WhenThereIsNothingUsable(string? content)
    {
        ResumeImprovementParser.TryParse(content, out var response).Should().BeFalse();

        response.Should().BeNull();
    }
}
