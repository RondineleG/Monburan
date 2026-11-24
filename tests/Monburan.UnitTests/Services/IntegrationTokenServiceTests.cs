using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Web.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Monburan.UnitTests.Services;

public class IntegrationTokenServiceTests
{
    private readonly IIntegrationTokenRepository _repository = Substitute.For<IIntegrationTokenRepository>();
    private readonly IntegrationTokenService _service;

    public IntegrationTokenServiceTests()
    {
        _service = new IntegrationTokenService(_repository);
        _repository.CreateAsync(Arg.Any<IntegrationToken>(), Arg.Any<CancellationToken>())
            .Returns(call => RequestResult<IntegrationToken>.Success(call.Arg<IntegrationToken>()));
    }

    [Fact]
    public async Task CreateAsync_ReenvioDoMesmoRequestId_DevolveTokenExistenteSemValorEmClaro()
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var scopes = new List<string> { "readiness:read" };

        var primeiro = await _service.CreateAsync(userId, "HiMarshall", scopes, 90, requestId);
        primeiro.RawValue.Should().NotBeNullOrEmpty();
        primeiro.Token.Should().NotBeNull();

        _repository.FindByRequestIdAsync(requestId, userId, Arg.Any<CancellationToken>())
            .Returns(primeiro.Token);

        var replay = await _service.CreateAsync(userId, "HiMarshall", scopes, 90, requestId);
        replay.RawValue.Should().BeNull("o reenvio do POST não emite um segundo token");
        replay.Token.Should().BeSameAs(primeiro.Token);
        await _repository.Received(1).CreateAsync(Arg.Any<IntegrationToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_CorridaDeDoisEnvios_DevolveTokenPersistidoSemValorEmClaro()
    {
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var existente = new IntegrationToken { UserId = userId, RequestId = requestId, Name = "HiMarshall" };

        _repository.CreateAsync(Arg.Any<IntegrationToken>(), Arg.Any<CancellationToken>())
            .Throws(new DbUpdateException("duplicate key"));
        // A pré-chegada não acha nada; quem acha é o retry depois do índice único barrar o insert.
        _repository.FindByRequestIdAsync(requestId, userId, Arg.Any<CancellationToken>())
            .Returns((IntegrationToken?)null, existente);

        var resultado = await _service.CreateAsync(userId, "HiMarshall", ["readiness:read"], 90, requestId);

        resultado.RawValue.Should().BeNull();
        resultado.Token.Should().BeSameAs(existente);
    }

    [Fact]
    public async Task CreateAsync_SemRequestId_SempreEmiteTokenNovo()
    {
        var resultado = await _service.CreateAsync(Guid.NewGuid(), "HiMarshall", ["jobs:read"], 90);

        resultado.RawValue.Should().StartWith("mbi_");
        resultado.Token.Should().NotBeNull();
        await _repository.DidNotReceive().FindByRequestIdAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
