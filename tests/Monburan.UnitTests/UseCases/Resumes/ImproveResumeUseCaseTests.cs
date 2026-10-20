using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Resumes;
using Monburan.Core.ValueObjects;

using NSubstitute;

namespace Monburan.UnitTests.UseCases.Resumes;

public class ImproveResumeUseCaseTests
{
    private const string ValidJson = """
        {"summary": "Resumo reescrito.", "experiences": [{"company": "Alfa", "position": "Pleno", "suggestions": ["Frase forte."]}],
         "skillsToAdd": ["Git"], "missingSections": ["Projetos"], "tips": ["Seja objetivo."]}
        """;

    private readonly IBaseRepository<Resume> _resumes = Substitute.For<IBaseRepository<Resume>>();
    private readonly IAiTextClient _ai = Substitute.For<IAiTextClient>();
    private readonly IAiUsageLimiter _limiter = Substitute.For<IAiUsageLimiter>();
    private readonly ISubscriptionRepository _subscriptions = Substitute.For<ISubscriptionRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly Guid _resumeId = Guid.NewGuid();
    private readonly ImproveResumeUseCase _useCase;

    public ImproveResumeUseCaseTests()
    {
        _ai.IsConfigured.Returns(true);
        _ai.ModelName.Returns("modelo-de-teste");
        _ai.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AiCompletion(ValidJson, "modelo-do-provedor", 569, 746));

        _limiter.TryAcquire(Arg.Any<string>(), out Arg.Any<TimeSpan>()).Returns(call =>
        {
            call[1] = TimeSpan.Zero;
            return true;
        });

        _resumes.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(RequestResult<Resume>.Success(ResumeOf(_candidateId)));
        _subscriptions.GetActiveByCandidateIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Subscription?>.Success(PremiumSubscription()));

        _useCase = new ImproveResumeUseCase(new GetResumeByIdUseCase(_resumes), _ai, _limiter, _subscriptions);
    }

    private Subscription PremiumSubscription()
    {
        var plan = new SubscriptionPlan { Id = Id.Create(Guid.NewGuid()), Name = "Premium", HasAiAssistant = true };
        return new Subscription
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(_candidateId),
            PlanId = plan.Id,
            Plan = plan,
            Status = ESubscriptionStatus.Active
        };
    }

    private ImproveResumeRequest Request(bool consent = true, Guid? candidateId = null) => new()
    {
        ResumeId = _resumeId,
        CandidateId = candidateId ?? _candidateId,
        AcceptsExternalProcessing = consent
    };

    private Resume ResumeOf(Guid candidateId) => new()
    {
        Id = Id.Create(_resumeId),
        CandidateId = Id.Create(candidateId),
        Title = "Desenvolvedor",
        Summary = "Contato ana@exemplo.com. Programo em C#.",
        ExperienceHistory = [new Experience { Company = "Alfa", Position = "Pleno", StartDate = new DateTime(2021, 3, 1), IsCurrent = true }],
        Skills = [new ResumeSkill { Skill = "C#", Level = "4", YearsOfExperience = 5 }]
    };

    [Fact]
    public async Task ExecuteAsync_WithoutConsent_ReturnsValidationAndNeverCallsTheProvider()
    {
        var result = await _useCase.ExecuteAsync(Request(consent: false));

        result.Status.Should().Be(EResultStatus.HasValidation);
        result.ValidationResult.Errors.Should().ContainSingle(error => error.Field == "AcceptsExternalProcessing");
        await _ai.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
        await _resumes.DidNotReceiveWithAnyArgs().GetByIdAsync(default!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAiIsNotConfigured_ReturnsDomainError()
    {
        _ai.IsConfigured.Returns(false);

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("não estão ativas");
        await _ai.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutPremiumPlan_ReturnsDomainErrorAndNeverCallsTheProvider()
    {
        _subscriptions.GetActiveByCandidateIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Subscription?>.Success(null));

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("Premium");
        await _ai.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_WithPlanWithoutAiAssistant_ReturnsDomainError()
    {
        var freePlan = new SubscriptionPlan { Id = Id.Create(Guid.NewGuid()), Name = "Free", HasAiAssistant = false };
        _subscriptions.GetActiveByCandidateIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Subscription?>.Success(new Subscription
            {
                Id = Id.Create(Guid.NewGuid()),
                CandidateId = Id.Create(_candidateId),
                PlanId = freePlan.Id,
                Plan = freePlan,
                Status = ESubscriptionStatus.Active
            }));

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("Premium");
        await _ai.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResumeDoesNotExist_ReturnsNotFound()
    {
        _resumes.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(RequestResult<Resume>.EntityNotFound("Resume", _resumeId, "x"));

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        await _ai.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResumeBelongsToSomeoneElse_BehavesAsNotFound()
    {
        var result = await _useCase.ExecuteAsync(Request(candidateId: Guid.NewGuid()));

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        _limiter.DidNotReceiveWithAnyArgs().TryAcquire(default!, out _);
        await _ai.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResumeHasOnlyATitle_AsksToFillItIn()
    {
        _resumes.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(RequestResult<Resume>.Success(
            new Resume { Id = Id.Create(_resumeId), CandidateId = Id.Create(_candidateId), Title = "Só título" }));

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("Preencha");
        await _ai.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheLimitIsReached_ExplainsHowLongToWait()
    {
        _limiter.TryAcquire(Arg.Any<string>(), out Arg.Any<TimeSpan>()).Returns(call =>
        {
            call[1] = TimeSpan.FromMinutes(6.2);
            return false;
        });

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("7 minutos");
        await _ai.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default);
    }

    [Fact]
    public async Task ExecuteAsync_LimitsPerCandidate()
    {
        await _useCase.ExecuteAsync(Request());

        _limiter.Received(1).TryAcquire($"resume-ai:{_candidateId}", out _);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsTheSuggestionsAndNamesTheModel()
    {
        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Summary.Should().Be("Resumo reescrito.");
        result.Data.Experiences.Should().ContainSingle();
        result.Data.SkillsToAdd.Should().Equal("Git");
        result.Data.Model.Should().Be("modelo-do-provedor");
        result.Data.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ExecuteAsync_SendsTheScrubbedResumeAsJsonRequest_WithTheCallersToken()
    {
        using var cancellation = new CancellationTokenSource();

        await _useCase.ExecuteAsync(Request(), cancellation.Token);

        await _ai.Received(1).CompleteAsync(
            Arg.Is<AiCompletionRequest>(request =>
                request.JsonObject
                && request.Messages.Count == 2
                && request.Messages[1].Content.Contains("[e-mail]")
                && !request.Messages[1].Content.Contains("ana@exemplo.com")),
            cancellation.Token);
    }

    [Theory]
    [InlineData(AiFailureKind.Timeout, "demorou demais")]
    [InlineData(AiFailureKind.RateLimited, "muito ocupado")]
    [InlineData(AiFailureKind.ProviderError, "Não foi possível gerar")]
    [InlineData(AiFailureKind.Unauthorized, "Não foi possível gerar")]
    [InlineData(AiFailureKind.InvalidResponse, "Não foi possível gerar")]
    [InlineData(AiFailureKind.Truncated, "Não foi possível gerar")]
    public async Task ExecuteAsync_MapsProviderFailuresToSafeMessages(AiFailureKind kind, string expected)
    {
        _ai.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns<AiCompletion>(_ => throw new AiClientException(kind, "chave tp-segredo-123 recusada pelo servidor interno.example"));

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain(expected);
        result.Message.Should().NotContain("segredo").And.NotContain("interno.example");
    }

    [Theory]
    [InlineData("Desculpe, não consegui.")]
    [InlineData("{ \"foo\": 1 }")]
    public async Task ExecuteAsync_WhenTheReplyIsNotUsable_ReturnsASafeError(string content)
    {
        _ai.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AiCompletion(content, null, null, null));

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("Não foi possível gerar");
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToTheConfiguredModelName()
    {
        _ai.CompleteAsync(Arg.Any<AiCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AiCompletion(ValidJson, null, null, null));

        var result = await _useCase.ExecuteAsync(Request());

        result.Data!.Model.Should().Be("modelo-de-teste");
    }
}
