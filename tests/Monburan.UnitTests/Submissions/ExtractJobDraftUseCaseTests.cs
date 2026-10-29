using FluentAssertions;
using Microsoft.Extensions.Logging;
using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.Storage;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.UseCases.Submissions;
using Monburan.Core.ValueObjects;
using NSubstitute;
using Xunit;

namespace Monburan.UnitTests.Submissions;

public class ExtractJobDraftUseCaseTests
{
    private const string GoodJson = """
        {"title": {"value": "Eletricista", "confidence": "high"},
         "companyName": {"value": "Construtora Exemplo", "confidence": "high"},
         "description": {"value": "Instalações elétricas residenciais.", "confidence": "high"},
         "channel": {"value": "onSite", "confidence": "high"},
         "instructions": {"value": "Agência do Trabalhador, segunda a sexta, 8h às 11h", "confidence": "high"}}
        """;

    private readonly IJobSubmissionRepository _submissions = Substitute.For<IJobSubmissionRepository>();
    private readonly IEvidenceStorage _evidence = Substitute.For<IEvidenceStorage>();
    private readonly IAiTextClient _ai = Substitute.For<IAiTextClient>();
    private readonly IAiUsageLimiter _limiter = Substitute.For<IAiUsageLimiter>();
    private readonly JobSubmission _submission;
    private readonly ExtractJobDraftUseCase _useCase;

    public ExtractJobDraftUseCaseTests()
    {
        _submission = Submission("aaa111", "bbb222");
        _submissions.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(_ => RequestResult<JobSubmission>.Success(_submission));
        _submissions.UpdateAsync(Arg.Any<JobSubmission>(), Arg.Any<CancellationToken>())
            .Returns(call => RequestResult<JobSubmission>.Success(call.Arg<JobSubmission>()));
        _evidence.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new byte[] { 0xFF, 0xD8, 0xFF, 1, 2, 3 });
        _ai.IsConfigured.Returns(true);
        _ai.ProviderName.Returns("Provedor de teste");
        _ai.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AiCompletion(GoodJson, "modelo-x", 10, 20));
        _limiter.TryAcquire(Arg.Any<string>(), out Arg.Any<TimeSpan>()).Returns(call =>
        {
            call[1] = TimeSpan.Zero;
            return true;
        });

        _useCase = new ExtractJobDraftUseCase(_submissions, _evidence, _ai, _limiter, Substitute.For<ILogger<ExtractJobDraftUseCase>>());
    }

    [Fact]
    public async Task SendsEveryEvidenceImageAsJsonRequest()
    {
        var result = await _useCase.ExecuteAsync(new ExtractJobDraftRequest(_submission.Id.Value));

        result.Status.Should().Be(EResultStatus.Success);
        await _ai.Received(1).CompleteAsync(Arg.Is<AiCompletionRequest>(r =>
            r.JsonObject
            && r.Messages.Last().Images!.Count == 2
            && r.Messages.Last().Images![0].MediaType == "image/jpeg"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoresTheDraftOnTheSubmissionWithItsEvidenceFingerprintAndProvider()
    {
        await _useCase.ExecuteAsync(new ExtractJobDraftRequest(_submission.Id.Value));

        _submission.AiDraftJson.Should().NotBeNullOrEmpty();
        _submission.AiDraftEvidenceHash.Should().NotBeNullOrEmpty();
        _submission.AiDraftProvider.Should().Be("Provedor de teste");
        _submission.AiDraftAt.Should().NotBeNull();
        _submission.Status.Should().Be(ESubmissionStatus.Pending, "gerar rascunho não decide a sugestão");
    }

    [Fact]
    public async Task ReturnsTheSavedDraftInsteadOfCallingTheProviderAgain()
    {
        var first = await _useCase.ExecuteAsync(new ExtractJobDraftRequest(_submission.Id.Value));
        first.Status.Should().Be(EResultStatus.Success);

        var second = await _useCase.ExecuteAsync(new ExtractJobDraftRequest(_submission.Id.Value));

        second.Status.Should().Be(EResultStatus.Success);
        second.Data!.Fields[AiJobDraftParser.Channel].Value.Should().Be("OnSite");
        await _ai.Received(1).CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangedEvidenceGeneratesANewDraft()
    {
        await _useCase.ExecuteAsync(new ExtractJobDraftRequest(_submission.Id.Value));
        _submission.Evidences.Add(new JobSubmissionEvidence { Id = Id.Create(Guid.NewGuid()), SubmissionId = _submission.Id, Sha256 = "ccc333", StorageKey = "k3", ContentType = "image/png", TenantId = "default" });

        await _useCase.ExecuteAsync(new ExtractJobDraftRequest(_submission.Id.Value));

        await _ai.Received(2).CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithoutAConfiguredProvider_DoesNotCallAnything()
    {
        _ai.IsConfigured.Returns(false);

        var result = await _useCase.ExecuteAsync(new ExtractJobDraftRequest(_submission.Id.Value));

        result.Status.Should().Be(EResultStatus.DomainError);
        await _ai.DidNotReceive().CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithoutEvidence_IsRefused()
    {
        _submission.Evidences.Clear();

        var result = await _useCase.ExecuteAsync(new ExtractJobDraftRequest(_submission.Id.Value));

        result.Status.Should().Be(EResultStatus.DomainError);
        await _ai.DidNotReceive().CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenRateLimited_DoesNotCallTheProvider()
    {
        _limiter.TryAcquire(Arg.Any<string>(), out Arg.Any<TimeSpan>()).Returns(call =>
        {
            call[1] = TimeSpan.FromMinutes(10);
            return false;
        });

        var result = await _useCase.ExecuteAsync(new ExtractJobDraftRequest(_submission.Id.Value));

        result.Status.Should().Be(EResultStatus.DomainError);
        await _ai.DidNotReceive().CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProviderWithoutVision_ReturnsAClearMessageAndStoresNothing()
    {
        _ai.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<AiCompletion>(_ => throw new AiClientException(AiFailureKind.VisionNotSupported, "sem visão"));

        var result = await _useCase.ExecuteAsync(new ExtractJobDraftRequest(_submission.Id.Value));

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("multimodal");
        _submission.AiDraftJson.Should().BeNull();
    }

    [Fact]
    public async Task ProviderFailure_ReturnsAFallbackMessageWithoutDetails()
    {
        _ai.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<AiCompletion>(_ => throw new AiClientException(AiFailureKind.Timeout, "detalhe interno do provedor"));

        var result = await _useCase.ExecuteAsync(new ExtractJobDraftRequest(_submission.Id.Value));

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("Preencha manualmente");
        result.Message.Should().NotContain("detalhe interno");
        _submission.AiDraftJson.Should().BeNull();
    }

    [Fact]
    public async Task UnreadableResponse_IsNotStored()
    {
        _ai.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AiCompletion("isto não é json", null, null, null));

        var result = await _useCase.ExecuteAsync(new ExtractJobDraftRequest(_submission.Id.Value));

        result.Status.Should().Be(EResultStatus.DomainError);
        _submission.AiDraftJson.Should().BeNull();
    }

    [Fact]
    public async Task MissingEvidenceFile_IsRefusedBeforeCallingTheProvider()
    {
        _evidence.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        var result = await _useCase.ExecuteAsync(new ExtractJobDraftRequest(_submission.Id.Value));

        result.Status.Should().Be(EResultStatus.DomainError);
        await _ai.DidNotReceive().CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    private static JobSubmission Submission(params string[] hashes)
    {
        var id = Id.Create(Guid.NewGuid());
        var submission = new JobSubmission { Id = id, Title = "Eletricista", TenantId = "default" };
        foreach (var hash in hashes)
        {
            submission.Evidences.Add(new JobSubmissionEvidence
            {
                Id = Id.Create(Guid.NewGuid()),
                SubmissionId = id,
                Sha256 = hash,
                StorageKey = $"chave-{hash}",
                ContentType = "image/jpeg",
                TenantId = "default"
            });
        }
        return submission;
    }
}
