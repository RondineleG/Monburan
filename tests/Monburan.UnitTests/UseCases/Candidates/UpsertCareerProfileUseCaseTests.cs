using FluentAssertions;
using NSubstitute;
using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Candidates;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.Candidates;

public class UpsertCareerProfileUseCaseTests
{
    private readonly ICareerProfileRepository _profiles = Substitute.For<ICareerProfileRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly UpsertCareerProfileUseCase _useCase;

    public UpsertCareerProfileUseCaseTests()
    {
        _profiles.CreateAsync(Arg.Any<CareerProfile>())
            .Returns(call => RequestResult<CareerProfile>.Success(call.Arg<CareerProfile>()));
        _profiles.UpdateAsync(Arg.Any<CareerProfile>())
            .Returns(call => RequestResult<CareerProfile>.Success(call.Arg<CareerProfile>()));

        _useCase = new UpsertCareerProfileUseCase(_profiles);
    }

    private static CareerProfileRequest ValidRequest() => new()
    {
        DesiredRole = "  Desenvolvedor Backend  ",
        City = "São Paulo",
        State = "sp",
        Mode = "remote",
        Category = "Tecnologia",
        MinimumSalary = 8000,
        ContractType = EContractType.Clt
    };

    [Fact]
    public async Task ExecuteAsync_WhenTheCandidateHasNoProfile_CreatesItNormalized()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(null));

        var result = await _useCase.ExecuteAsync(new UpsertCareerProfileCommand(_candidateId, ValidRequest()));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.DesiredRole.Should().Be("Desenvolvedor Backend");
        result.Data.State.Should().Be("SP");
        result.Data.Mode.Should().Be("Remote");
        result.Data.ContractType.Should().Be(EContractType.Clt);
        await _profiles.Received(1).CreateAsync(Arg.Is<CareerProfile>(profile =>
            profile.CandidateId.Value == _candidateId
            && profile.State == "SP"
            && profile.Mode == "Remote"
            && profile.TenantId == "default"));
        await _profiles.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheProfileExists_UpdatesIt()
    {
        var existing = new CareerProfile
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(_candidateId),
            DesiredRole = "Analista",
            State = "RJ",
            Mode = "OnSite",
            TenantId = "default"
        };
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(existing));

        var result = await _useCase.ExecuteAsync(new UpsertCareerProfileCommand(_candidateId, ValidRequest()));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.DesiredRole.Should().Be("Desenvolvedor Backend");
        result.Data.State.Should().Be("SP");
        await _profiles.Received(1).UpdateAsync(Arg.Is<CareerProfile>(profile =>
            profile.Id.Value == existing.Id.Value
            && profile.DesiredRole == "Desenvolvedor Backend"
            && profile.Mode == "Remote"));
        await _profiles.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidRequest_ReturnsValidationAndNeverTouchesTheRepository()
    {
        var request = ValidRequest();
        request.State = "São Paulo";

        var result = await _useCase.ExecuteAsync(new UpsertCareerProfileCommand(_candidateId, request));

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == nameof(CareerProfileRequest.State));
        await _profiles.DidNotReceiveWithAnyArgs().GetByCandidateIdAsync(default);
        await _profiles.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        await _profiles.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WithNegativeSalary_ReturnsValidation()
    {
        var request = ValidRequest();
        request.MinimumSalary = -1;

        var result = await _useCase.ExecuteAsync(new UpsertCareerProfileCommand(_candidateId, request));

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == nameof(CareerProfileRequest.MinimumSalary));
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheLookupFails_PropagatesTheErrorAndNeverWrites()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(new UpsertCareerProfileCommand(_candidateId, ValidRequest()));

        result.Status.Should().Be(EResultStatus.HasError);
        await _profiles.DidNotReceiveWithAnyArgs().CreateAsync(default!);
        await _profiles.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheWriteFails_PropagatesTheError()
    {
        _profiles.GetByCandidateIdAsync(_candidateId).Returns(RequestResult<CareerProfile?>.Success(null));
        _profiles.CreateAsync(Arg.Any<CareerProfile>()).Returns(RequestResult<CareerProfile>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(new UpsertCareerProfileCommand(_candidateId, ValidRequest()));

        result.Status.Should().Be(EResultStatus.HasError);
    }
}
