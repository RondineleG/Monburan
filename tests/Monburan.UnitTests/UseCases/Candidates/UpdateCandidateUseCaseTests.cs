using FluentAssertions;
using NSubstitute;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Candidates;
using Monburan.Core.Validations;
using Monburan.Core.ValueObjects;

namespace Monburan.UnitTests.UseCases.Candidates;

public class UpdateCandidateUseCaseTests
{
    private readonly IBaseRepository<Candidate> _candidates = Substitute.For<IBaseRepository<Candidate>>();
    private readonly IEntityValidator<Candidate> _validator = Substitute.For<IEntityValidator<Candidate>>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly Guid _ownedAddressId = Guid.NewGuid();
    private readonly UpdateCandidateUseCase _useCase;

    public UpdateCandidateUseCaseTests()
    {
        // Validador válido por padrão: os testes de IDOR não dependem das regras do CandidateValidator
        _validator.ValidateUpdate(Arg.Any<Candidate>()).Returns(ValidationResult.Success());

        _useCase = new UpdateCandidateUseCase(_candidates, _validator);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAddressIdBelongsToAnotherProfile_ReturnsValidationError()
    {
        var candidate = GivenCandidateWithAddress(_ownedAddressId);
        _candidates.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Candidate>.Success(candidate));

        var request = new UpdateCandidateRequest
        {
            Id = _candidateId,
            Name = "Maria Souza",
            AddressId = Guid.NewGuid() // endereço de outra pessoa
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == nameof(UpdateCandidateRequest.AddressId));
        await _candidates.DidNotReceive().UpdateAsync(Arg.Any<Candidate>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAddressIdIsTheProfileOwnAddress_UpdatesProfile()
    {
        var candidate = GivenCandidateWithAddress(_ownedAddressId);
        _candidates.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Candidate>.Success(candidate));
        _candidates.UpdateAsync(Arg.Any<Candidate>())
            .Returns(callInfo => RequestResult<Candidate>.Success(callInfo.Arg<Candidate>()));

        var request = new UpdateCandidateRequest
        {
            Id = _candidateId,
            Name = "Maria Souza",
            AddressId = _ownedAddressId
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.Success);
        await _candidates.Received(1).UpdateAsync(Arg.Is<Candidate>(c => c.AddressId != null && c.AddressId.Value == _ownedAddressId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenProfileHasNoAddressAndAddressIdIsInformed_ReturnsValidationError()
    {
        var candidate = GivenCandidateWithAddress(_ownedAddressId);
        candidate.AddressId = null;
        _candidates.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Candidate>.Success(candidate));

        var request = new UpdateCandidateRequest
        {
            Id = _candidateId,
            Name = "Maria Souza",
            AddressId = Guid.NewGuid()
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == nameof(UpdateCandidateRequest.AddressId));
        await _candidates.DidNotReceive().UpdateAsync(Arg.Any<Candidate>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProfileIsNotFound_ReturnsNotFound()
    {
        _candidates.GetByIdAsync(Arg.Any<Id>())
            .Returns(RequestResult<Candidate>.EntityNotFound("Candidate", _candidateId, "Candidato não encontrado."));

        var request = new UpdateCandidateRequest { Id = _candidateId, Name = "Maria Souza" };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.EntityNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidatorFails_ReturnsValidationErrorWithoutUpdating()
    {
        var candidate = GivenCandidateWithAddress(_ownedAddressId);
        _candidates.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Candidate>.Success(candidate));
        _validator.ValidateUpdate(Arg.Any<Candidate>())
            .Returns(ValidationResult.Failure("Nome deve ter entre 3 e 100 caracteres.", "Name"));

        var request = new UpdateCandidateRequest { Id = _candidateId, Name = "A" };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == "Name");
        await _candidates.DidNotReceive().UpdateAsync(Arg.Any<Candidate>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDateOfBirthIsInformedAndMeansUnder14_ReturnsValidationError()
    {
        var useCase = new UpdateCandidateUseCase(_candidates, new CandidateValidator());
        var candidate = GivenCandidateWithAddress(_ownedAddressId);
        _candidates.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Candidate>.Success(candidate));

        var request = new UpdateCandidateRequest
        {
            Id = _candidateId,
            Name = "Maria Souza",
            DateOfBirth = DateTime.UtcNow.AddYears(-10)
        };

        var result = await useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().Contain(error => error.Field == "DateOfBirth");
        await _candidates.DidNotReceive().UpdateAsync(Arg.Any<Candidate>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDateOfBirthIsNotInformed_UpdatesProfile()
    {
        // O aplicativo mobile atualiza o perfil sem a data de nascimento de quem
        // ainda não preencheu o campo: a atualização não pode barrar a edição.
        var useCase = new UpdateCandidateUseCase(_candidates, new CandidateValidator());
        var candidate = GivenCandidateWithAddress(_ownedAddressId);
        candidate.DateOfBirth = null;
        _candidates.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Candidate>.Success(candidate));
        _candidates.UpdateAsync(Arg.Any<Candidate>())
            .Returns(callInfo => RequestResult<Candidate>.Success(callInfo.Arg<Candidate>()));

        var request = new UpdateCandidateRequest
        {
            Id = _candidateId,
            Name = "Maria Souza",
            Phone = "(11) 98888-7777"
        };

        var result = await useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.Success);
        await _candidates.Received(1).UpdateAsync(Arg.Any<Candidate>());
    }

    private Candidate GivenCandidateWithAddress(Guid addressId) => new()
    {
        Id = Id.Create(_candidateId),
        Name = "Maria Souza",
        Email = "maria@teste.com",
        Phone = "11999990000",
        DateOfBirth = new DateTime(1990, 5, 10),
        AddressId = Id.Create(addressId),
        TenantId = "default"
    };
}
