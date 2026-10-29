using FluentAssertions;
using Microsoft.Extensions.Logging;
using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.Storage;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Submissions;
using NSubstitute;
using Xunit;

namespace Monburan.UnitTests.Submissions;

public class SubmitJobSuggestionUseCaseTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1];

    private readonly IJobSubmissionRepository _submissions = Substitute.For<IJobSubmissionRepository>();
    private readonly IEvidenceStorage _evidence = Substitute.For<IEvidenceStorage>();
    private readonly ISubmissionRateLimiter _limiter = Substitute.For<ISubmissionRateLimiter>();
    private readonly SubmitJobSuggestionUseCase _useCase;

    public SubmitJobSuggestionUseCaseTests()
    {
        _limiter.TryAcquire(Arg.Any<string>(), out Arg.Any<TimeSpan>()).Returns(callInfo =>
        {
            callInfo[1] = TimeSpan.Zero;
            return true;
        });
        _evidence.SaveAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => $"chave-{Guid.NewGuid():N}");
        _submissions.CreateAsync(Arg.Any<JobSubmission>(), Arg.Any<CancellationToken>())
            .Returns(call => RequestResult<JobSubmission>.Success(call.Arg<JobSubmission>()));

        _useCase = new SubmitJobSuggestionUseCase(_submissions, _evidence, _limiter, Substitute.For<ILogger<SubmitJobSuggestionUseCase>>());
    }

    [Fact]
    public async Task ValidSuggestion_IsRegisteredAsPendingWithItsEvidence()
    {
        var result = await _useCase.ExecuteAsync(Valid());

        result.Status.Should().Be(EResultStatus.Success);
        await _submissions.Received(1).CreateAsync(Arg.Is<JobSubmission>(s =>
            s.Status == ESubmissionStatus.Pending
            && s.Evidences.Count == 1
            && s.History.Single().Status == ESubmissionStatus.Pending), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidSuggestion_StoresTheSubmitterOnlyAsAHash()
    {
        var request = Valid();
        request.SubmitterKey = "203.0.113.7";

        await _useCase.ExecuteAsync(request);

        await _submissions.Received(1).CreateAsync(Arg.Is<JobSubmission>(s =>
            s.SubmitterKeyHash != null && !s.SubmitterKeyHash.Contains("203.0.113.7")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuggestionWithoutContactEmail_IsAccepted()
    {
        var request = Valid();
        request.ContactEmail = null;

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.Success);
    }

    [Fact]
    public async Task WithoutAnyImage_IsRefusedAndNothingIsStored()
    {
        var request = Valid();
        request.Evidences = [];

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        await _evidence.DidNotReceive().SaveAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _submissions.DidNotReceive().CreateAsync(Arg.Any<JobSubmission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImageWhoseContentIsNotAnImage_IsRefused()
    {
        var request = Valid();
        request.Evidences = [new SubmissionEvidenceInput("vaga.jpg", "image/jpeg", "<html>"u8.ToArray())];

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        await _evidence.DidNotReceive().SaveAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImageAboveTheSizeLimit_IsRefused()
    {
        var request = Valid();
        var big = new byte[6 * 1024 * 1024];
        Jpeg.CopyTo(big, 0);
        request.Evidences = [new SubmissionEvidenceInput("grande.jpg", "image/jpeg", big)];

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
    }

    [Fact]
    public async Task WithoutSourceUrlOrDescription_IsRefused()
    {
        var request = Valid();
        request.SourceUrl = null;
        request.SourceDescription = "curto";

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.ErrorMessage.Should().Contain("Informe onde encontrou");
    }

    [Fact]
    public async Task SourceAddressWithoutWebProtocol_IsRefused()
    {
        var request = Valid();
        request.SourceUrl = "vagas.exemplo.com.br";

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
    }

    [Fact]
    public async Task WhenRateLimited_IsRefusedBeforeAnythingIsStored()
    {
        _limiter.TryAcquire(Arg.Any<string>(), out Arg.Any<TimeSpan>()).Returns(callInfo =>
        {
            callInfo[1] = TimeSpan.FromMinutes(5);
            return false;
        });

        var result = await _useCase.ExecuteAsync(Valid());

        result.Status.Should().Be(EResultStatus.DomainError);
        await _evidence.DidNotReceive().SaveAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenTheRecordCannotBeSaved_TheStoredImagesAreRemoved()
    {
        _submissions.CreateAsync(Arg.Any<JobSubmission>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<JobSubmission>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(Valid());

        result.Status.Should().NotBe(EResultStatus.Success);
        await _evidence.Received(1).DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static SubmitJobSuggestionRequest Valid() => new()
    {
        Title = "Eletricista",
        CompanyName = null,
        City = "Itajaí",
        State = "SC",
        SourceDescription = "Divulgado no mural da Agência do Trabalhador",
        Evidences = [new SubmissionEvidenceInput("cartaz.jpg", "image/jpeg", Jpeg)],
        SubmitterKey = "anonimo-1"
    };
}
