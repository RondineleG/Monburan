using FluentAssertions;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.UseCases.ResumeShares;
using Monburan.Core.ValueObjects;
using NSubstitute;
using Xunit;

namespace Monburan.UnitTests.ResumeShares;

public class ResumeShareTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeProvider Clock = new FixedClock(Now);

    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly Guid _resumeId = Guid.NewGuid();
    private readonly IResumeRepository _resumes = Substitute.For<IResumeRepository>();
    private readonly IResumeShareRepository _shares = Substitute.For<IResumeShareRepository>();
    private readonly List<ResumeShare> _stored = [];

    public ResumeShareTests()
    {
        _resumes.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(_ => RequestResult<Resume>.Success(Resume()));
        _shares.CreateAsync(Arg.Any<ResumeShare>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _stored.Add(call.Arg<ResumeShare>());
                return RequestResult<ResumeShare>.Success(call.Arg<ResumeShare>());
            });
        _shares.ListByResumeAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(_ => RequestResult<IReadOnlyList<ResumeShare>>.Success(_stored.ToList()));
        _shares.UpdateAsync(Arg.Any<ResumeShare>(), Arg.Any<CancellationToken>())
            .Returns(call => RequestResult<ResumeShare>.Success(call.Arg<ResumeShare>()));
    }

    [Fact]
    public void Token_IsLongRandomAndUrlSafe_AndOnlyItsHashIsStored()
    {
        var token = ResumeShareRules.NewToken();

        token.Length.Should().BeGreaterThanOrEqualTo(43, "256 bits em base64url");
        token.Should().NotContain("+").And.NotContain("/").And.NotContain("=");
        ResumeShareRules.NewToken().Should().NotBe(token);
        ResumeShareRules.Hash(token).Should().HaveLength(64).And.NotContain(token);
    }

    [Fact]
    public async Task Create_ReturnsTheTokenOnceAndStoresOnlyItsHash()
    {
        var result = await Create(ShowSummaryOnly());

        result.Status.Should().Be(EResultStatus.Success);
        var stored = _stored.Single();
        stored.TokenHash.Should().Be(ResumeShareRules.Hash(result.Data!.Token));
        stored.TokenHash.Should().NotBe(result.Data.Token);
    }

    [Fact]
    public async Task Create_ForAResumeOfAnotherCandidate_IsRefusedAsNotFound()
    {
        var result = await Create(ShowSummaryOnly(), candidate: Guid.NewGuid());

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WithAPastOrTooLongExpiry_IsRefused()
    {
        var past = await Create(ShowSummaryOnly() with { ExpiresAt = Now.AddDays(-1) });
        var tooLong = await Create(ShowSummaryOnly() with { ExpiresAt = Now.AddYears(2) });

        past.Status.Should().Be(EResultStatus.HasValidation);
        tooLong.Status.Should().Be(EResultStatus.HasValidation);
        _stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_RefusesAMSixthActiveLink()
    {
        for (var i = 0; i < ResumeShareRules.MaxActiveSharesPerResume; i++)
            (await Create(ShowSummaryOnly())).Status.Should().Be(EResultStatus.Success);

        var sixth = await Create(ShowSummaryOnly());

        sixth.Status.Should().Be(EResultStatus.DomainError);
    }

    [Fact]
    public async Task Revoke_TakesEffectImmediately_AndKeepsTheResume()
    {
        var created = await Create(ShowSummaryOnly());
        var share = _stored.Single();
        share.Resume = Resume();
        _shares.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(RequestResult<ResumeShare>.Success(share));

        var revoke = new RevokeResumeShareUseCase(_shares, Clock);
        var result = await revoke.ExecuteAsync(_candidateId, share.Id.Value);

        result.Status.Should().Be(EResultStatus.Success);
        share.RevokedAt.Should().Be(Now);
        share.IsAvailableAt(Now).Should().BeFalse();
        created.Status.Should().Be(EResultStatus.Success);
    }

    [Fact]
    public async Task Revoke_ByAnotherCandidate_IsRefused()
    {
        await Create(ShowSummaryOnly());
        var share = _stored.Single();
        _shares.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>()).Returns(RequestResult<ResumeShare>.Success(share));

        var result = await new RevokeResumeShareUseCase(_shares, Clock).ExecuteAsync(Guid.NewGuid(), share.Id.Value);

        result.Status.Should().Be(EResultStatus.EntityNotFound);
        share.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task PublicView_ShowsOnlyTheFieldsTheCandidateChose()
    {
        await Create(new ResumeShareOptions("Vaga", ShowName: false, ShowSummary: true, ShowExperience: false, ShowEducation: false,
            ShowSkills: true, ShowLanguages: false, ExpiresAt: null));
        var share = _stored.Single();
        var view = ResumeShareRules.Project(Resume(), share);

        view.Summary.Should().Be("Resumo profissional");
        view.Skills.Should().Equal("C#");
        view.Name.Should().BeNull("o nome só aparece se o candidato liberar");
        view.Experiences.Should().BeEmpty();
        view.Education.Should().BeEmpty();
        view.Languages.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicLookup_WithTheRightToken_ReturnsTheProjection()
    {
        var created = await Create(ShowSummaryOnly());
        _stored.Single().Resume = Resume();
        _shares.GetByTokenHashAsync(ResumeShareRules.Hash(created.Data!.Token), Arg.Any<CancellationToken>())
            .Returns(RequestResult<ResumeShare>.Success(_stored.Single()));

        var result = await new GetPublicResumeUseCase(_shares, Clock).ExecuteAsync(created.Data.Token);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Summary.Should().Be("Resumo profissional");
    }

    [Fact]
    public async Task PublicLookup_ForRevokedOrExpiredOrUnknownToken_AnswersTheSameWay()
    {
        var created = await Create(ShowSummaryOnly());
        var share = _stored.Single();
        share.RevokedAt = Now.AddMinutes(-5);
        _shares.GetByTokenHashAsync(ResumeShareRules.Hash(created.Data!.Token), Arg.Any<CancellationToken>())
            .Returns(RequestResult<ResumeShare>.Success(share));
        _shares.GetByTokenHashAsync(ResumeShareRules.Hash("token-que-nao-existe"), Arg.Any<CancellationToken>())
            .Returns(RequestResult<ResumeShare>.EntityNotFound("ResumeShare", "x", "x"));
        var useCase = new GetPublicResumeUseCase(_shares, Clock);

        var revoked = await useCase.ExecuteAsync(created.Data.Token);
        var unknown = await useCase.ExecuteAsync("token-que-nao-existe");

        revoked.Status.Should().Be(EResultStatus.EntityNotFound);
        unknown.Status.Should().Be(EResultStatus.EntityNotFound);
        revoked.Message.Should().Be(unknown.Message, "não revela se o link existiu");
    }

    [Fact]
    public async Task ListShowsTheSituationOfEachLink()
    {
        await Create(ShowSummaryOnly());
        await Create(ShowSummaryOnly() with { ExpiresAt = Now.AddDays(3) });
        _stored[1].ExpiresAt = Now.AddMinutes(-1);
        _stored[0].RevokedAt = Now.AddMinutes(-1);

        var result = await new ListResumeSharesUseCase(_resumes, _shares, Clock).ExecuteAsync(_candidateId, _resumeId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Select(s => s.Status).Should().BeEquivalentTo("Revogado", "Expirado");
    }

    private Task<RequestResult<CreatedResumeShare>> Create(ResumeShareOptions options, Guid? candidate = null) =>
        new CreateResumeShareUseCase(_resumes, _shares, Clock).ExecuteAsync(candidate ?? _candidateId, _resumeId, options);

    private static ResumeShareOptions ShowSummaryOnly() => new("Teste", ShowName: false, ShowSummary: true, ShowExperience: false,
        ShowEducation: false, ShowSkills: false, ShowLanguages: false, ExpiresAt: null);

    private Resume Resume()
    {
        var resume = new Resume
        {
            Id = Id.Create(_resumeId),
            CandidateId = Id.Create(_candidateId),
            Title = "Desenvolvedor",
            Summary = "Resumo profissional",
            Candidate = new Candidate { Id = Id.Create(_candidateId), Name = "Pessoa Teste", Email = "pessoa@teste.com", TenantId = "default" },
            TenantId = "default"
        };
        resume.Skills.Add(new ResumeSkill { Skill = "C#", Level = "Avançado", TenantId = "default", Id = Id.Create(Guid.NewGuid()), ResumeId = resume.Id });
        resume.ExperienceHistory.Add(new Experience { Company = "Empresa", Position = "Dev", StartDate = Now.AddYears(-2) });
        return resume;
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
