using FluentAssertions;
using NSubstitute;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.JobAlerts;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.JobAlerts;

public class ListJobAlertsUseCaseTests
{
    private readonly IJobAlertRepository _alerts = Substitute.For<IJobAlertRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly ListJobAlertsUseCase _useCase;

    public ListJobAlertsUseCaseTests()
    {
        _useCase = new ListJobAlertsUseCase(_alerts);
    }

    private JobAlert AlertOf(string name) => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        CandidateId = Id.Create(_candidateId),
        Name = name,
        Keywords = "Backend",
        City = "São Paulo",
        State = "SP",
        Mode = "Remote",
        Category = "Tecnologia",
        SalaryMin = 5000,
        IsActive = true,
        TenantId = "default"
    };

    [Fact]
    public async Task ExecuteAsync_WhenThereAreAlerts_ReturnsThemMapped()
    {
        var first = AlertOf("Primeiro");
        var second = AlertOf("Segundo");
        _alerts.ListByCandidateAsync(_candidateId)
            .Returns(RequestResult<IEnumerable<JobAlert>>.Success([first, second]));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().HaveCount(2);
        result.Data!.Select(alert => alert.Name).Should().ContainInOrder("Primeiro", "Segundo");
        result.Data.First().State.Should().Be("SP");
        result.Data.First().Mode.Should().Be("Remote");
        result.Data.First().SalaryMin.Should().Be(5000);
        result.Data.First().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAlerts_ReturnsAnEmptyList()
    {
        _alerts.ListByCandidateAsync(_candidateId)
            .Returns(RequestResult<IEnumerable<JobAlert>>.Success([]));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_PropagatesTheError()
    {
        _alerts.ListByCandidateAsync(_candidateId).Returns(RequestResult<IEnumerable<JobAlert>>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.HasError);
    }
}
