using FluentAssertions;

using Monburan.Core.DTOs.Response;
using Monburan.Core.UseCases.Resumes;

namespace Monburan.UnitTests.UseCases.Resumes;

public class ResumeAiPromptTests
{
    private static ResumeResponse Resume() => new()
    {
        Id = Guid.NewGuid(),
        CandidateId = Guid.NewGuid(),
        Title = "Desenvolvedor Full Stack",
        Summary = "Programador com 6 anos de experiência. Fale comigo: ana@exemplo.com ou (11) 99999-8888.",
        ExperienceHistory =
        [
            new ExperienceResponse
            {
                Company = "Empresa Alfa Ltda",
                Position = "Desenvolvedor Pleno",
                Description = "Telas e APIs.\n\n  Portfólio: https://github.com/ana/projetos  ",
                StartDate = new DateTime(2021, 3, 1),
                IsCurrent = true
            },
            new ExperienceResponse
            {
                Company = "Beta Sistemas",
                Position = "Desenvolvedor Júnior",
                StartDate = new DateTime(2018, 6, 1),
                EndDate = new DateTime(2021, 2, 1)
            }
        ],
        EducationHistory =
        [
            new EducationResponse
            {
                Course = "Análise e Desenvolvimento de Sistemas",
                Institution = "Faculdade Exemplo",
                Level = "Technical",
                Status = "Completed",
                EndDate = new DateTime(2018, 12, 1)
            }
        ],
        Skills = [new SkillResponse { Skill = "C#", Level = 4, YearsOfExperience = 6 }, new SkillResponse { Skill = "React", Level = 2, YearsOfExperience = 1 }],
        Languages = [new LanguageResponse { Language = "Inglês", Level = "Intermediate" }]
    };

    [Fact]
    public void Describe_IncludesTheProfessionalContent_InPortuguese()
    {
        var text = ResumeAiPrompt.Describe(Resume());

        text.Should().Contain("Título do currículo: Desenvolvedor Full Stack");
        text.Should().Contain("1. Desenvolvedor Pleno — Empresa Alfa Ltda (03/2021 a atual)");
        text.Should().Contain("2. Desenvolvedor Júnior — Beta Sistemas (06/2018 a 02/2021)");
        text.Should().Contain("- Análise e Desenvolvimento de Sistemas (técnico) — Faculdade Exemplo, concluído, 2018");
        text.Should().Contain("- C# (nível 4 de 5, 6 anos)");
        text.Should().Contain("- React (nível 2 de 5, 1 ano)");
        text.Should().Contain("- Inglês (intermediário)");
    }

    [Fact]
    public void Describe_NeverSendsPersonalDataTypedInFreeText()
    {
        var text = ResumeAiPrompt.Describe(Resume());

        text.Should().NotContain("ana@exemplo.com");
        text.Should().NotContain("99999-8888");
        text.Should().NotContain("github.com");
        text.Should().Contain("[e-mail]").And.Contain("[telefone]").And.Contain("[link]");
    }

    [Fact]
    public void Describe_KeepsLineBreaksButDropsBlankLines()
    {
        var text = ResumeAiPrompt.Describe(Resume());

        text.Should().Contain("Descrição: Telas e APIs.\nPortfólio: [link]");
    }

    [Fact]
    public void Describe_IsLimitedInSize()
    {
        var resume = Resume();
        resume.Summary = new string('a', 5000);
        resume.ExperienceHistory = Enumerable.Range(1, 30)
            .Select(index => new ExperienceResponse { Company = $"Empresa {index}", Position = "Cargo", Description = new string('b', 3000) })
            .ToList();

        var text = ResumeAiPrompt.Describe(resume);

        text.Length.Should().BeLessThanOrEqualTo(ResumeAiPrompt.MaxCharacters);
        text.Should().NotContain("Empresa 16", "só as 15 primeiras experiências são enviadas");
    }

    [Fact]
    public void Describe_MarksMissingSummary()
    {
        var resume = Resume();
        resume.Summary = null;

        ResumeAiPrompt.Describe(resume).Should().Contain("Resumo atual: (não informado)");
    }

    [Fact]
    public void Build_PutsTheRulesInTheSystemMessageAndTheResumeInTheUserMessage()
    {
        var messages = ResumeAiPrompt.Build(Resume());

        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be("system");
        messages[0].Content.Should().Contain("Nunca invente").And.Contain("JSON");
        messages[1].Role.Should().Be("user");
        messages[1].Content.Should().StartWith("Título do currículo:");
    }

    [Fact]
    public void SystemPrompt_BansInventedFactsAndFillInPlaceholders()
    {
        ResumeAiPrompt.SystemPrompt.Should()
            .Contain("Não use marcadores de preenchimento")
            .And.Contain("explicitamente citadas nas descrições")
            .And.NotContain("marcador entre colchetes");
    }

    [Fact]
    public void HasContent_NeedsSomethingBesidesTheTitle()
    {
        ResumeAiPrompt.HasContent(new ResumeResponse { Title = "Só título" }).Should().BeFalse();
        ResumeAiPrompt.HasContent(new ResumeResponse { Title = "x", Summary = "Resumo" }).Should().BeTrue();
        ResumeAiPrompt.HasContent(new ResumeResponse { Title = "x", Skills = [new SkillResponse { Skill = "C#" }] }).Should().BeTrue();
    }
}
