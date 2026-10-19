using FluentAssertions;

using Monburan.Core.DTOs.Response;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class CandidateProgressTextTests
{
    [Theory]
    [InlineData(0, "Perfil 0% completo")]
    [InlineData(67, "Perfil 67% completo")]
    [InlineData(100, "Perfil 100% completo")]
    [InlineData(120, "Perfil 100% completo")]
    [InlineData(-5, "Perfil 0% completo")]
    public void Completeness_FormatsTheProgressLabel(int percent, string expected)
    {
        CandidateProgressText.Completeness(percent).Should().Be(expected);
    }

    [Fact]
    public void NextSteps_ListsOnlyThePendingItems_InChecklistOrder()
    {
        var progress = new CandidateProgressResponse
        {
            Checklist =
            [
                new CandidateProgressChecklistItem { Key = "name", Label = "Nome completo", Done = true, ActionUrl = "/profile/candidate" },
                new CandidateProgressChecklistItem { Key = "phone", Label = "Telefone", Done = false, ActionUrl = "/profile/candidate" },
                new CandidateProgressChecklistItem { Key = "resume", Label = "Currículo cadastrado", Done = false, ActionUrl = "/resumes" },
                new CandidateProgressChecklistItem { Key = "careerProfile", Label = "Perfil de carreira", Done = true, ActionUrl = "/career-profile" }
            ]
        };

        var nextSteps = CandidateProgressText.NextSteps(progress);

        nextSteps.Select(item => item.Key).Should().ContainInOrder("phone", "resume");
        nextSteps.Should().HaveCount(2);
    }
}
