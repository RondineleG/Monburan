using FluentAssertions;
using NSubstitute;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Candidates;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.Candidates;

public class GetCareerProfileUseCaseTests
{
    private readonly ICareerProfileRepository _profiles = Substitute.For<ICareerProfileRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly GetCareerProfileUseCase _useCase;

    public GetCareerProfileUseCaseTests()
    {
        _useCase = new GetCareerProfileUseCase(_profiles);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheCandidateHasNoProfile_ReturnsSuccessWithNullFields()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(null));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().NotBeNull();
        result.Data!.DesiredRole.Should().BeNull();
        result.Data.City.Should().BeNull();
        result.Data.ContractType.Should().BeNull();
        result.Data.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheProfileExists_ReturnsItMapped()
    {
        var profile = new CareerProfile
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(_candidateId),
            DesiredRole = "Desenvolvedor Backend",
            City = "São Paulo",
            State = "SP",
            Mode = "Remote",
            Category = "Tecnologia",
            MinimumSalary = 8000,
            ContractType = EContractType.Clt,
            TenantId = "default"
        };
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(profile));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.DesiredRole.Should().Be("Desenvolvedor Backend");
        result.Data.State.Should().Be("SP");
        result.Data.Mode.Should().Be("Remote");
        result.Data.MinimumSalary.Should().Be(8000);
        result.Data.ContractType.Should().Be(EContractType.Clt);
        result.Data.UpdatedAt.Should().Be(profile.UpdatedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_PropagatesTheError()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.HasError);
    }
}
