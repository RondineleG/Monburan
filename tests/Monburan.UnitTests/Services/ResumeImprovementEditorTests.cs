using Monburan.Core.DTOs.Response;
using Monburan.Web.Models;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Services;

public class ResumeImprovementEditorTests
{
    private static ResumeResponse Resume() => new()
    {
        Id = Guid.NewGuid(),
        CandidateId = Guid.NewGuid(),
        Title = "Desenvolvedora",
        Summary = "Resumo original",
        IsActive = true,
        SourceFileName = "cv.pdf",
        ExperienceHistory =
        [
            new ExperienceResponse { Company = "Tech", Position = "Dev", Description = "Fiz coisas", IsCurrent = true },
            new ExperienceResponse { Company = "Outra", Position = "QA", Description = "Testei" }
        ],
        EducationHistory = [new EducationResponse { Course = "CC", Institution = "UF", Level = "Bachelor", Status = "Completed" }],
        Languages = [new LanguageResponse { Language = "Inglês", Level = "Advanced" }],
        Skills = [new SkillResponse { Skill = "C#", Level = 4, YearsOfExperience = 5 }]
    };

    private static ResumeImprovementResponse Result() => new()
    {
        Summary = "Resumo melhorado [X%]",
        Experiences =
        [
            new ExperienceImprovement { Company = " Tech ", Position = "dev", Suggestions = ["Entreguei X% a mais"] },
            new ExperienceImprovement { Company = "Inexistente", Position = "CTO", Suggestions = ["Sugestão solta"] }
        ],
        SkillsToAdd = [" dotnet core ", "c#", "Comunicação"],
        MissingSections = ["Projetos"],
        Tips = ["Use verbos de ação"],
        GeneratedAt = DateTime.UtcNow
    };

    [Fact]
    public void MatchExperienceIndex_UniqueMatch_ReturnsIndex()
    {
        var index = ResumeImprovementEditor.MatchExperienceIndex(Resume().ExperienceHistory!, " tech ", "DEV");
        Assert.Equal(0, index);
    }

    [Fact]
    public void MatchExperienceIndex_Ambiguous_ReturnsMinusTwo()
    {
        var originals = new List<ExperienceResponse>
        {
            new() { Company = "Tech", Position = "Dev" },
            new() { Company = "Tech", Position = "Dev" }
        };
        Assert.Equal(-2, ResumeImprovementEditor.MatchExperienceIndex(originals, "Tech", "Dev"));
    }

    [Fact]
    public void CreateDraft_UnmatchedExperience_IsNotAssignable()
    {
        var draft = ResumeImprovementEditor.CreateDraft(Guid.NewGuid(), Resume(), Result());
        Assert.Null(draft.Experiences[1].MatchedIndex);
        Assert.Equal(0, draft.Experiences[0].MatchedIndex);
        Assert.All(draft.Experiences, e => Assert.False(e.Selected));
        Assert.All(draft.Skills, s => Assert.False(s.Selected));
    }

    [Fact]
    public void CreateDraft_DedupesSkillsAgainstExisting()
    {
        // "c#" já existe no currículo; "dotnet core" normaliza para .NET (nova)
        var draft = ResumeImprovementEditor.CreateDraft(Guid.NewGuid(), Resume(), Result());
        Assert.Equal([".NET", "Comunicação"], draft.Skills.Select(s => s.Skill).ToList());
    }

    [Fact]
    public void BuildUpdateRequest_SummaryOnly_KeepsAllOtherFields()
    {
        var resume = Resume();
        var draft = ResumeImprovementEditor.CreateDraft(resume.CandidateId, resume, Result());
        draft.SummarySelected = true;
        draft.EditedSummary = "Novo resumo";

        var request = ResumeImprovementEditor.BuildUpdateRequest(resume, draft);

        Assert.Equal(resume.Id, request.Id);
        Assert.Equal(resume.CandidateId, request.CandidateId);
        Assert.Equal(resume.Title, request.Title);
        Assert.Equal(resume.IsActive, request.IsActive);
        Assert.Equal("Novo resumo", request.Summary);
        Assert.Equal("Fiz coisas", request.ExperienceHistory![0].Description);
        Assert.Single(request.EducationHistory!);
        Assert.Single(request.Languages!);
        var skill = Assert.Single(request.Skills!);
        Assert.Equal("C#", skill.Skill);
        Assert.Equal(4, skill.Level);
        Assert.Equal(5, skill.YearsOfExperience);
    }

    [Fact]
    public void BuildUpdateRequest_SelectedExperience_AppendsAndPreservesMetadata()
    {
        var resume = Resume();
        var draft = ResumeImprovementEditor.CreateDraft(resume.CandidateId, resume, Result());
        draft.Experiences[0].Selected = true;
        draft.Skills[0].Selected = true; // .NET

        var request = ResumeImprovementEditor.BuildUpdateRequest(resume, draft);

        Assert.StartsWith("Fiz coisas", request.ExperienceHistory![0].Description);
        Assert.Contains("Entreguei X% a mais", request.ExperienceHistory[0].Description);
        Assert.Equal("Testei", request.ExperienceHistory[1].Description);
        Assert.Equal(2, request.Skills!.Count);
        Assert.Equal(".NET", request.Skills[1].Skill);
    }

    [Fact]
    public void PlaceholderBlocks_OnlySelectedFields()
    {
        var resume = Resume();
        var draft = ResumeImprovementEditor.CreateDraft(resume.CandidateId, resume, Result());
        // Nada selecionado → nada bloqueado, mesmo com [X%] na sugestão
        Assert.Empty(ResumeImprovementEditor.PlaceholderBlocks(draft));

        draft.SummarySelected = true;
        Assert.Contains("Resumo", ResumeImprovementEditor.PlaceholderBlocks(draft));

        draft.EditedSummary = "Resumo final sem lacuna";
        Assert.Empty(ResumeImprovementEditor.PlaceholderBlocks(draft));
    }

    [Fact]
    public void SnapshotChanged_DetectsEdits()
    {
        var before = Resume();
        Assert.False(ResumeImprovementEditor.SnapshotChanged(before, Resume()));

        var edited = Resume();
        edited.Title = "Outro título";
        Assert.True(ResumeImprovementEditor.SnapshotChanged(before, edited));
    }

    [Fact]
    public void Preview_AppliesOnlySelected()
    {
        var resume = Resume();
        var draft = ResumeImprovementEditor.CreateDraft(resume.CandidateId, resume, Result());
        draft.Skills[0].Selected = true;

        var preview = ResumeImprovementEditor.Preview(resume, draft);

        Assert.Equal("Resumo original", preview.Summary);
        Assert.Equal(2, preview.Skills!.Count);
        Assert.Equal("Fiz coisas", preview.ExperienceHistory![0].Description);
    }
}

public class PlaceholderTheoryTests
{
    [Theory]
    [InlineData("[X%]", true)]
    [InlineData("[X]", true)]
    [InlineData("[??]", true)]
    [InlineData("[...]", true)]
    [InlineData("[insira o valor]", true)]
    [InlineData("[preencha aqui]", true)]
    [InlineData("[projeto executivo]", false)]
    [InlineData("[Linux]", false)]
    [InlineData("texto normal", false)]
    public void ContainsPlaceholder_Theory(string text, bool expected) =>
        Assert.Equal(expected, ResumeImprovementEditor.ContainsPlaceholder(text));

    [Fact]
    public void HasApplicableChanges_NoSelection_False()
    {
        var draft = ResumeImprovementEditor.CreateDraft(Guid.NewGuid(), new ResumeResponse(),
            new ResumeImprovementResponse { Summary = "x", SkillsToAdd = ["Y"] });
        Assert.False(ResumeImprovementEditor.HasApplicableChanges(draft));
    }
}
