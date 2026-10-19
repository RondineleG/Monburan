using FluentAssertions;

using Monburan.Core.DTOs.Response;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class ApplicationTimelineBuilderTests
{
    [Fact]
    public void Build_OrdersTheStepsFromOldestToNewest()
    {
        var history = new List<ApplicationHistoryItemResponse>
        {
            new() { Status = "Viewed", Date = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc) },
            new() { Status = "Sent", Date = new DateTime(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc) },
            new() { Status = "Selected", Date = new DateTime(2026, 10, 15, 18, 30, 0, DateTimeKind.Utc) }
        };

        var timeline = ApplicationTimelineBuilder.Build(history);

        timeline.Select(step => step.Status).Should().ContainInOrder("Sent", "Viewed", "Selected");
    }

    [Fact]
    public void Build_TranslatesTheStatusAndCleansTheNotes()
    {
        var history = new List<ApplicationHistoryItemResponse>
        {
            new()
            {
                Status = "Sent",
                Date = new DateTime(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc),
                Note = "  Currículo recebido  ",
                Responsible = "  "
            }
        };

        var step = ApplicationTimelineBuilder.Build(history).Single();

        step.Label.Should().Be("Enviada");
        step.Note.Should().Be("Currículo recebido");
        step.Responsible.Should().BeNull("nota ou responsável em branco não devem aparecer");
    }

    [Fact]
    public void Build_WithoutHistory_ReturnsNoSteps()
    {
        ApplicationTimelineBuilder.Build([]).Should().BeEmpty();
    }
}
