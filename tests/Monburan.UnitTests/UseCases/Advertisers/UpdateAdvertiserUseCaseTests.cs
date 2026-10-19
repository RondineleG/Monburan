using FluentAssertions;
using NSubstitute;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Advertisers;
using Monburan.Core.Validations;
using Monburan.Core.ValueObjects;

namespace Monburan.UnitTests.UseCases.Advertisers;

public class UpdateAdvertiserUseCaseTests
{
    private readonly IBaseRepository<Advertiser> _advertisers = Substitute.For<IBaseRepository<Advertiser>>();
    private readonly IEntityValidator<Advertiser> _validator = Substitute.For<IEntityValidator<Advertiser>>();
    private readonly Guid _advertiserId = Guid.NewGuid();
    private readonly Guid _ownedAddressId = Guid.NewGuid();
    private readonly UpdateAdvertiserUseCase _useCase;

    public UpdateAdvertiserUseCaseTests()
    {
        // Validador válido por padrão: os testes de IDOR não dependem das regras do AdvertiserValidator
        _validator.ValidateUpdate(Arg.Any<Advertiser>()).Returns(ValidationResult.Success());

        _useCase = new UpdateAdvertiserUseCase(_advertisers, _validator);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAddressIdBelongsToAnotherProfile_ReturnsValidationError()
    {
        var advertiser = GivenAdvertiserWithAddress(_ownedAddressId);
        _advertisers.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Advertiser>.Success(advertiser));

        var request = new UpdateAdvertiserRequest
        {
            Id = _advertiserId,
            Name = "Burger LTDA",
            Phone = "1133334444",
            TaxId = "11222333000181",
            AddressId = Guid.NewGuid() // endereço de outra empresa
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == nameof(UpdateAdvertiserRequest.AddressId));
        await _advertisers.DidNotReceive().UpdateAsync(Arg.Any<Advertiser>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAddressIdIsTheProfileOwnAddress_UpdatesProfile()
    {
        var advertiser = GivenAdvertiserWithAddress(_ownedAddressId);
        _advertisers.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Advertiser>.Success(advertiser));
        _advertisers.UpdateAsync(Arg.Any<Advertiser>())
            .Returns(callInfo => RequestResult<Advertiser>.Success(callInfo.Arg<Advertiser>()));

        var request = new UpdateAdvertiserRequest
        {
            Id = _advertiserId,
            Name = "Burger LTDA",
            Phone = "1133334444",
            TaxId = "11222333000181",
            AddressId = _ownedAddressId
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.Success);
        await _advertisers.Received(1).UpdateAsync(Arg.Is<Advertiser>(a => a.AddressId != null && a.AddressId.Value == _ownedAddressId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidatorFails_ReturnsValidationErrorWithoutUpdating()
    {
        var advertiser = GivenAdvertiserWithAddress(_ownedAddressId);
        _advertisers.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Advertiser>.Success(advertiser));
        _validator.ValidateUpdate(Arg.Any<Advertiser>())
            .Returns(ValidationResult.Failure("CNPJ/CPF inválido.", "TaxId"));

        var request = new UpdateAdvertiserRequest
        {
            Id = _advertiserId,
            Name = "Burger LTDA",
            Phone = "1133334444",
            TaxId = "123"
        };

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == "TaxId");
        await _advertisers.DidNotReceive().UpdateAsync(Arg.Any<Advertiser>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTaxIdIsInformedAndInvalid_ReturnsValidationError()
    {
        var useCase = new UpdateAdvertiserUseCase(_advertisers, new AdvertiserValidator());
        var advertiser = GivenAdvertiserWithAddress(_ownedAddressId);
        _advertisers.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Advertiser>.Success(advertiser));

        var request = new UpdateAdvertiserRequest
        {
            Id = _advertiserId,
            Name = "Burger LTDA",
            Phone = "1133334444",
            TaxId = "11222333000199" // dígitos verificadores inválidos
        };

        var result = await useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().Contain(error => error.Field == "TaxId");
        await _advertisers.DidNotReceive().UpdateAsync(Arg.Any<Advertiser>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTaxIdIsNotInformed_UpdatesProfile()
    {
        // O perfil do anunciante nasce incompleto (sem telefone e sem CNPJ/CPF):
        // a atualização não pode barrar a edição de quem ainda não preencheu.
        var useCase = new UpdateAdvertiserUseCase(_advertisers, new AdvertiserValidator());
        var advertiser = GivenAdvertiserWithAddress(_ownedAddressId);
        advertiser.Phone = null;
        advertiser.TaxId = null;
        _advertisers.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Advertiser>.Success(advertiser));
        _advertisers.UpdateAsync(Arg.Any<Advertiser>())
            .Returns(callInfo => RequestResult<Advertiser>.Success(callInfo.Arg<Advertiser>()));

        var request = new UpdateAdvertiserRequest
        {
            Id = _advertiserId,
            Name = "Burger LTDA"
        };

        var result = await useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.Success);
        await _advertisers.Received(1).UpdateAsync(Arg.Any<Advertiser>());
    }

    private Advertiser GivenAdvertiserWithAddress(Guid addressId) => new()
    {
        Id = Id.Create(_advertiserId),
        Name = "Burger LTDA",
        Email = "contato@burger.com.br",
        Phone = "1133334444",
        TaxId = "11222333000181",
        AddressId = Id.Create(addressId),
        TenantId = "default"
    };
}
