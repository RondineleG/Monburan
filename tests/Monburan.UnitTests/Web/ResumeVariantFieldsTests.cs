using FluentAssertions;

using Monburan.Core.DTOs.Response;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class ResumeVariantFieldsTests
{
    private static ResumeResponse Resume() => new()
    {
        Title = "Atendente",
        Summary = "Cinco anos de atendimento.",
        ExperienceHistory =
        [
            new ExperienceResponse { Position = "Caixa", Company = "Mercado Bom", Description = "Fechamento de caixa", StartDate = new DateTime(2020, 1, 1) },
            new ExperienceResponse { Position = "Vendedora", Company = "Loja Sol" }
        ],
        EducationHistory = [new EducationResponse { Course = "Administração", Institution = "Univali", Level = "Bachelor" }],
        Skills = [new SkillResponse { Skill = "Atendimento", Level = 3 }],
        Languages = [new LanguageResponse { Language = "Espanhol", Level = "Intermediate" }]
    };

    [Fact]
    public void From_UsesTheBackendKeysInTheBaseOrder()
    {
        var keys = ResumeVariantFields.From(Resume()).Select(field => field.Key).ToList();

        keys.Should().Equal(
            "title", "summary",
            "experience:0:position", "experience:0:company", "experience:0:description",
            "experience:1:position", "experience:1:company",
            "education:0:course", "education:0:institution",
            "skills:0", "languages:0");
    }

    [Fact]
    public void Localize_ReplacesOnlyTranslatedTexts_AndKeepsTheStructure()
    {
        var localized = ResumeVariantFields.Localize(Resume(), new Dictionary<string, string>
        {
            ["title"] = "Dependienta",
            ["experience:1:position"] = "Vendedora de tienda",
            ["skills:0"] = "  "
        });

        localized.Title.Should().Be("Dependienta");
        localized.Summary.Should().Be("Cinco anos de atendimento.", "sem tradução fica o original");
        localized.ExperienceHistory![0].Position.Should().Be("Caixa");
        localized.ExperienceHistory[0].StartDate.Should().Be(new DateTime(2020, 1, 1));
        localized.ExperienceHistory[1].Position.Should().Be("Vendedora de tienda");
        localized.Skills![0].Skill.Should().Be("Atendimento", "tradução em branco não apaga o original");
        localized.Skills[0].Level.Should().Be(3);
    }

    [Fact]
    public void Localize_NeverChangesTheBaseResume()
    {
        var original = Resume();

        ResumeVariantFields.Localize(original, new Dictionary<string, string> { ["title"] = "Dependienta" });

        original.Title.Should().Be("Atendente");
    }
}
