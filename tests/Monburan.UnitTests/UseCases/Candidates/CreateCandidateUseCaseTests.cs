using FluentAssertions;
using NSubstitute;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Candidates;
using Monburan.Core.Validations;

namespace Monburan.UnitTests.UseCases.Candidates;

public class CreateCandidateUseCaseTests
{
    private readonly IAccountProfileRepository _candidates = Substitute.For<IAccountProfileRepository>();
    private readonly CreateCandidateUseCase _useCase;

    public CreateCandidateUseCaseTests()
    {
        // Validador real: o teste prova que o CandidateValidator está de fato no fluxo
        _useCase = new CreateCandidateUseCase(_candidates, new CandidateValidator());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameIsTooShort_ReturnsValidationError()
    {
        var request = new CreateCandidateRequest
        {
            Name = "A",
            Email = "maria@teste.com"
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().Contain(error => error.Field == "Name");
        await _candidates.DidNotReceive().CreateCandidateAsync(Arg.Any<Candidate>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailIsInvalid_ReturnsValidationError()
    {
        var request = new CreateCandidateRequest
        {
            Name = "Maria Souza",
            Email = "maria@@teste"
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().Contain(error => error.Field == "Email");
        await _candidates.DidNotReceive().CreateCandidateAsync(Arg.Any<Candidate>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDateOfBirthMeansUnder14_ReturnsValidationError()
    {
        var request = new CreateCandidateRequest
        {
            Name = "Maria Souza",
            Email = "maria@teste.com",
            DateOfBirth = DateTime.UtcNow.AddYears(-10)
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().Contain(error => error.Field == "DateOfBirth");
        await _candidates.DidNotReceive().CreateCandidateAsync(Arg.Any<Candidate>());
    }

    [Fact]
    public async Task ExecuteAsync_WithOnlyNameAndEmail_CreatesProfile()
    {
        // O cadastro (web e API) cria o perfil só com nome e e-mail: a data de
        // nascimento é preenchida depois e não pode barrar a criação.
        _candidates.CreateCandidateAsync(Arg.Any<Candidate>())
            .Returns(callInfo => RequestResult<Candidate>.Success(callInfo.Arg<Candidate>()));

        var request = new CreateCandidateRequest
        {
            Name = "Maria Souza",
            Email = "maria@teste.com"
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.Success);
        await _candidates.Received(1).CreateCandidateAsync(Arg.Any<Candidate>());
    }
}
