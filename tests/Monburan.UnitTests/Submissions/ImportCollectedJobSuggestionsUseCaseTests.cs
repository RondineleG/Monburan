using System.Security.Claims;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Submissions;
using NSubstitute;
using Xunit;

namespace Monburan.UnitTests.Submissions;

public class ImportCollectedJobSuggestionsUseCaseTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 11, 10, 8, 30, 0, TimeSpan.Zero);

    private readonly IJobSubmissionRepository _submissions = Substitute.For<IJobSubmissionRepository>();
    private readonly IFeatureFlags _featureFlags = Substitute.For<IFeatureFlags>();
    private readonly ILogger<ImportCollectedJobSuggestionsUseCase> _logger =
        Substitute.For<ILogger<ImportCollectedJobSuggestionsUseCase>>();
    private readonly List<JobSubmission> _stored = [];
    private readonly ImportCollectedJobSuggestionsUseCase _useCase;

    public ImportCollectedJobSuggestionsUseCaseTests()
    {
        _featureFlags.IsEnabledAsync("captacao-vagas", Arg.Any<ClaimsPrincipal?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _submissions.FindDuplicatesAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Monburan.Core.ValueObjects.Id?>(),
                Arg.Any<CancellationToken>())
            .Returns(RequestResult<IReadOnlyList<DuplicateCandidate>>.Success([]));
        _submissions.CreateAsync(Arg.Any<JobSubmission>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _stored.Add(call.Arg<JobSubmission>());
                return RequestResult<JobSubmission>.Success(call.Arg<JobSubmission>());
            });

        _useCase = new ImportCollectedJobSuggestionsUseCase(_submissions, _featureFlags, _logger);
    }

    [Fact]
    public async Task WhenTheCollectorFeatureIsOff_NothingIsImported()
    {
        _featureFlags.IsEnabledAsync("captacao-vagas", Arg.Any<ClaimsPrincipal?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest([Job()]), null);

        result.Status.Should().Be(EResultStatus.HasValidation);
        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task AnEmptyBatch_IsRejected()
    {
        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest([]), null);

        result.Status.Should().Be(EResultStatus.HasValidation);
        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task ABatchOverTheLimit_IsRejected()
    {
        var jobs = Enumerable.Range(0, 251)
            .Select(i => Job(sourceUrl: $"https://pgempregos.com.br/vaga/{i}"))
            .ToList();

        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest(jobs), null);

        result.Status.Should().Be(EResultStatus.HasValidation);
        _stored.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AJobWithoutTitle_IsRejected(string title)
    {
        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest([Job(title: title)]), null);

        result.Status.Should().Be(EResultStatus.HasValidation);
        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task AJobWithATitleOverTheLimit_IsRejected()
    {
        var job = Job(title: new string('a', 201));

        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest([job]), null);

        result.Status.Should().Be(EResultStatus.HasValidation);
        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task AJobWithoutACaptureTime_IsRejected()
    {
        var job = Job() with { CapturedAt = default };

        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest([job]), null);

        result.Status.Should().Be(EResultStatus.HasValidation);
        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task AJobWithAnInsecureSourceUrl_IsRejected()
    {
        var job = Job(sourceUrl: "http://pgempregos.com.br/vaga/1");

        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest([job]), null);

        result.Status.Should().Be(EResultStatus.HasValidation);
        _stored.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://example.com/vaga/1")]
    [InlineData("https://pgempregos.com.br.evil.com/vaga/1")]
    [InlineData("https://sub.pgempregos.com.br/vaga/1")]
    public async Task AJobFromAHostOutsideTheAllowList_IsRejected(string sourceUrl)
    {
        var job = Job(sourceUrl: sourceUrl);

        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest([job]), null);

        result.Status.Should().Be(EResultStatus.HasValidation);
        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task AJobWithOversizedFields_IsRejected()
    {
        var job = Job() with { Description = new string('x', 2001) };

        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest([job]), null);

        result.Status.Should().Be(EResultStatus.HasValidation);
        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task AValidJob_BecomesAPendingSubmissionWithTheImportNote()
    {
        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest([Job()]), null);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().BeEquivalentTo(new ImportCollectedJobSuggestionsResponse(1, 0));
        var stored = _stored.Should().ContainSingle().Subject;
        stored.Status.Should().Be(ESubmissionStatus.Pending);
        stored.Title.Should().Be("Auxiliar de produção");
        stored.CompanyName.Should().Be("Indústria Exemplo");
        stored.City.Should().Be("Ponta Grossa");
        stored.State.Should().Be("PR");
        stored.SourceUrl.Should().Be("https://pgempregos.com.br/vaga/1");
        stored.SourceDescription.Should().Contain("Portal PG Empregos").And.Contain("Campos Gerais");
        stored.History.Should().ContainSingle(h => h.Status == ESubmissionStatus.Pending);
    }

    [Fact]
    public async Task OptionalFieldsThatAreBlank_StayNull()
    {
        var job = Job() with { CompanyName = "  ", City = null, Description = "" };

        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest([job]), null);

        result.Status.Should().Be(EResultStatus.Success);
        var stored = _stored.Should().ContainSingle().Subject;
        stored.CompanyName.Should().BeNull();
        stored.City.Should().BeNull();
        stored.Description.Should().BeNull();
    }

    [Fact]
    public async Task TheSameSourceUrlTwiceInOneBatch_CountsAsDuplicate()
    {
        var jobs = new[]
        {
            Job(sourceUrl: "https://pgempregos.com.br/vaga/1"),
            Job(sourceUrl: "https://pgempregos.com.br/vaga/1#detalhes"),
            Job(sourceUrl: "https://pgempregos.com.br/vaga/2")
        };

        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest(jobs), null);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().BeEquivalentTo(new ImportCollectedJobSuggestionsResponse(2, 1));
        _stored.Should().HaveCount(2);
    }

    [Fact]
    public async Task ASourceUrlAlreadyInTheModerationQueue_CountsAsDuplicate()
    {
        _submissions.FindDuplicatesAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Monburan.Core.ValueObjects.Id?>(),
                Arg.Any<CancellationToken>())
            .Returns(RequestResult<IReadOnlyList<DuplicateCandidate>>.Success(
                [new DuplicateCandidate("submission", Guid.NewGuid(), "Auxiliar de produção", "mesma fonte")]));

        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest([Job()]), null);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().BeEquivalentTo(new ImportCollectedJobSuggestionsResponse(0, 1));
        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task AMatchByAnotherSignal_StillImports_OnlyTheSameSourceDeduplicates()
    {
        _submissions.FindDuplicatesAsync(
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Monburan.Core.ValueObjects.Id?>(),
                Arg.Any<CancellationToken>())
            .Returns(RequestResult<IReadOnlyList<DuplicateCandidate>>.Success(
                [new DuplicateCandidate("job", Guid.NewGuid(), "Auxiliar de produção", "mesma empresa e cargo")]));

        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest([Job()]), null);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().BeEquivalentTo(new ImportCollectedJobSuggestionsResponse(1, 0));
        _stored.Should().ContainSingle();
    }

    [Fact]
    public async Task WhenTheSubmissionCannotBeSaved_TheBatchStopsWithTheFailure()
    {
        _submissions.CreateAsync(Arg.Any<JobSubmission>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<JobSubmission>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(new ImportCollectedJobSuggestionsRequest([Job()]), null);

        result.Status.Should().NotBe(EResultStatus.Success);
        _stored.Should().BeEmpty();
    }

    private static CollectedJobInput Job(
        string title = "Auxiliar de produção",
        string sourceUrl = "https://pgempregos.com.br/vaga/1") =>
        new(
            title,
            CompanyName: "Indústria Exemplo",
            City: "Ponta Grossa",
            State: "PR",
            Description: "Trabalho em linha de produção, escala 6x1.",
            SourceName: "Portal PG Empregos",
            sourceUrl,
            CapturedAt,
            Region: "Campos Gerais");
}
