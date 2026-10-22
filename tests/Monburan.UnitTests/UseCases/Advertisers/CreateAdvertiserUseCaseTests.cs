using FluentAssertions;
using NSubstitute;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Advertisers;
using Monburan.Core.Validations;

namespace Monburan.UnitTests.UseCases.Advertisers;

public class CreateAdvertiserUseCaseTests
{
    private readonly IAccountProfileRepository _advertisers = Substitute.For<IAccountProfileRepository>();
    private readonly CreateAdvertiserUseCase _useCase;

    public CreateAdvertiserUseCaseTests()
    {
        // Validador real: o teste prova que o AdvertiserValidator está de fato no fluxo
        _useCase = new CreateAdvertiserUseCase(_advertisers, new AdvertiserValidator());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNameIsTooShort_ReturnsValidationError()
    {
        var request = new CreateAdvertiserRequest
        {
            Name = "B",
            Email = "contato@burger.com.br",
            PersonType = EPersonType.Company
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().Contain(error => error.Field == "Name");
        await _advertisers.DidNotReceive().CreateAdvertiserAsync(Arg.Any<Advertiser>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTaxIdIsInvalid_ReturnsValidationError()
    {
        var request = new CreateAdvertiserRequest
        {
            Name = "Burger LTDA",
            Email = "contato@burger.com.br",
            PersonType = EPersonType.Company,
            Phone = "1133334444",
            TaxId = "11222333000199" // dígitos verificadores inválidos
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().Contain(error => error.Field == "TaxId");
        await _advertisers.DidNotReceive().CreateAdvertiserAsync(Arg.Any<Advertiser>());
    }

    [Fact]
    public async Task ExecuteAsync_WithOnlyNameAndEmail_CreatesProfile()
    {
        // O cadastro (web e API) cria o perfil só com nome, e-mail e tipo de pessoa:
        // telefone e CNPJ/CPF são preenchidos depois e não podem barrar a criação.
        _advertisers.CreateAdvertiserAsync(Arg.Any<Advertiser>())
            .Returns(callInfo => RequestResult<Advertiser>.Success(callInfo.Arg<Advertiser>()));

        var request = new CreateAdvertiserRequest
        {
            Name = "Burger LTDA",
            Email = "contato@burger.com.br",
            PersonType = EPersonType.Company
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.Success);
        await _advertisers.Received(1).CreateAdvertiserAsync(Arg.Any<Advertiser>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTaxIdIsValid_CreatesProfile()
    {
        _advertisers.CreateAdvertiserAsync(Arg.Any<Advertiser>())
            .Returns(callInfo => RequestResult<Advertiser>.Success(callInfo.Arg<Advertiser>()));

        var request = new CreateAdvertiserRequest
        {
            Name = "Burger LTDA",
            Email = "contato@burger.com.br",
            PersonType = EPersonType.Company,
            Phone = "1133334444",
            TaxId = "11222333000181"
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.Success);
        await _advertisers.Received(1).CreateAdvertiserAsync(Arg.Any<Advertiser>());
    }
}
