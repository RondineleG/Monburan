using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Resumes;
using Monburan.Core.ValueObjects;
using Monburan.Web.Services;

using NSubstitute;

namespace Monburan.UnitTests.Services;

public class ApplicationLookupServiceTests
{
    private readonly IApplicationRepository _applications = Substitute.For<IApplicationRepository>();
    private readonly ICurrentUserHelper _user = Substitute.For<ICurrentUserHelper>();

    private ApplicationLookupService Sut() => new(_applications, _user, Microsoft.Extensions.Logging.Abstractions.NullLogger<ApplicationLookupService>.Instance);

    [Fact]
    public async Task GetAsync_NoCandidate_ReturnsNullWithoutQuery()
    {
        _user.GetCandidateId().Returns((Guid?)null);
        var result = await Sut().GetAsync(Guid.NewGuid());
        Assert.Equal(EResultStatus.Success, result.Status);
        Assert.Null(result.Data);
        await _applications.DidNotReceive().FindByCandidateAndJobAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetAsync_Applied_ReturnsMappedApplication()
    {
        var candidateId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        _user.GetCandidateId().Returns(candidateId);
        _applications.FindByCandidateAndJobAsync(candidateId, jobId).Returns(
            RequestResult<Application?>.Success(new Application
            {
                CandidateId = Id.Create(candidateId),
                JobId = Id.Create(jobId),
                Status = EApplicationStatus.Viewed,
                ResumeId = Guid.NewGuid(),
                AppliedAt = DateTime.UtcNow.AddDays(-2)
            }));

        var result = await Sut().GetAsync(jobId);

        Assert.Equal(EResultStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal("Viewed", result.Data!.Status);
        Assert.Equal(jobId, result.Data.JobId);
        Assert.Equal(candidateId, result.Data.CandidateId);
    }

    [Fact]
    public async Task GetAsync_NoApplication_ReturnsNull()
    {
        var candidateId = Guid.NewGuid();
        _user.GetCandidateId().Returns(candidateId);
        _applications.FindByCandidateAndJobAsync(candidateId, Arg.Any<Guid>())
            .Returns(RequestResult<Application?>.Success(null));

        var result = await Sut().GetAsync(Guid.NewGuid());
        Assert.Equal(EResultStatus.Success, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetAsync_RepositoryFailure_PropagatesError()
    {
        var candidateId = Guid.NewGuid();
        _user.GetCandidateId().Returns(candidateId);
        _applications.FindByCandidateAndJobAsync(candidateId, Arg.Any<Guid>())
            .Returns(RequestResult<Application?>.WithError("falha de banco"));

        var result = await Sut().GetAsync(Guid.NewGuid());
        Assert.NotEqual(EResultStatus.Success, result.Status);
        Assert.Null(result.Data);
    }
}

public class ResumeViewerServiceTests
{
    private readonly IBaseRepository<Resume> _resumes = Substitute.For<IBaseRepository<Resume>>();
    private readonly IApplicationRepository _applications = Substitute.For<IApplicationRepository>();
    private readonly IBaseRepository<Candidate> _candidates = Substitute.For<IBaseRepository<Candidate>>();
    private readonly ICurrentUserHelper _user = Substitute.For<ICurrentUserHelper>();

    private ResumeViewerService Sut() =>
        new(new GetResumeByIdUseCase(_resumes), _applications, _candidates, _user,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ResumeViewerService>.Instance);

    private static Resume ResumeOf(Guid candidateId) => new()
    {
        CandidateId = Id.Create(candidateId),
        Title = "CV",
        Summary = "Resumo"
    };

    private void ResumeFound(Guid candidateId)
    {
        var resume = ResumeOf(candidateId);
        _resumes.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Resume>.Success(resume));
        _candidates.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Candidate>.Success(new Candidate
            {
                Name = "Ana",
                Email = "ana@x.com",
                Phone = "1199",
                LinkedIn = "https://linkedin.com/in/ana",
                DateOfBirth = new DateTime(1990, 1, 1)
            }));
    }

    [Fact]
    public async Task GetAsync_Owner_IsOwnerWithContact()
    {
        var candidateId = Guid.NewGuid();
        ResumeFound(candidateId);
        _user.GetCandidateId().Returns(candidateId);
        _user.GetAdvertiserId().Returns((Guid?)null);

        var result = await Sut().GetAsync(Guid.NewGuid());

        Assert.Equal(EResultStatus.Success, result.Status);
        Assert.True(result.Data!.IsOwner);
        Assert.Equal("ana@x.com", result.Data.Email);
    }

    [Fact]
    public async Task GetAsync_AdvertiserWithSentResume_CanView()
    {
        var candidateId = Guid.NewGuid();
        var advertiserId = Guid.NewGuid();
        ResumeFound(candidateId);
        _user.GetCandidateId().Returns((Guid?)null);
        _user.GetAdvertiserId().Returns(advertiserId);
        _applications.CanAdvertiserViewResumeAsync(advertiserId, Arg.Any<Guid>(), candidateId, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await Sut().GetAsync(Guid.NewGuid());

        Assert.Equal(EResultStatus.Success, result.Status);
        Assert.False(result.Data!.IsOwner);
        Assert.Equal("Ana", result.Data.Name);
    }

    [Fact]
    public async Task GetAsync_OtherAdvertiser_NotFound()
    {
        var candidateId = Guid.NewGuid();
        ResumeFound(candidateId);
        _user.GetCandidateId().Returns((Guid?)null);
        _user.GetAdvertiserId().Returns(Guid.NewGuid());
        _applications.CanAdvertiserViewResumeAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await Sut().GetAsync(Guid.NewGuid());
        Assert.Equal(EResultStatus.EntityNotFound, result.Status);
    }

    [Fact]
    public async Task GetAsync_UnknownResume_NotFound()
    {
        _resumes.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Resume>.EntityNotFound("Resume", Guid.NewGuid(), "não"));

        var result = await Sut().GetAsync(Guid.NewGuid());
        Assert.Equal(EResultStatus.EntityNotFound, result.Status);
    }

    [Fact]
    public async Task GetAsync_ResumeRepositoryError_Propagates()
    {
        _resumes.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Resume>.WithError("falha"));

        var result = await Sut().GetAsync(Guid.NewGuid());
        Assert.NotEqual(EResultStatus.Success, result.Status);
    }
}

public class LookupViewerExceptionSafetyTests
{
    private readonly IApplicationRepository _applications = Substitute.For<IApplicationRepository>();
    private readonly ICurrentUserHelper _user = Substitute.For<ICurrentUserHelper>();

    [Fact]
    public async Task Lookup_RepositoryThrows_ReturnsSafeError()
    {
        var candidateId = Guid.NewGuid();
        _user.GetCandidateId().Returns(candidateId);
        _applications.FindByCandidateAndJobAsync(candidateId, Arg.Any<Guid>())
            .Returns<RequestResult<Application?>>(_ => throw new InvalidOperationException("banco caiu"));

        var sut = new ApplicationLookupService(_applications, _user,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ApplicationLookupService>.Instance);
        var result = await sut.GetAsync(Guid.NewGuid());

        Assert.NotEqual(EResultStatus.Success, result.Status);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Lookup_Cancellation_Propagates()
    {
        var candidateId = Guid.NewGuid();
        _user.GetCandidateId().Returns(candidateId);
        _applications.FindByCandidateAndJobAsync(candidateId, Arg.Any<Guid>())
            .Returns<RequestResult<Application?>>(_ => throw new OperationCanceledException());

        var sut = new ApplicationLookupService(_applications, _user,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ApplicationLookupService>.Instance);
        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Viewer_PermissionQueryThrows_SafeError()
    {
        var resumes = Substitute.For<IBaseRepository<Resume>>();
        var candidates = Substitute.For<IBaseRepository<Candidate>>();
        var candidateId = Guid.NewGuid();
        var advertiserId = Guid.NewGuid();
        resumes.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Resume>.Success(new Resume { CandidateId = Id.Create(candidateId) }));
        _user.GetCandidateId().Returns((Guid?)null);
        _user.GetAdvertiserId().Returns(advertiserId);
        _applications.CanAdvertiserViewResumeAsync(advertiserId, Arg.Any<Guid>(), candidateId, Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new InvalidOperationException("timeout"));

        var sut = new ResumeViewerService(new GetResumeByIdUseCase(resumes), _applications, candidates, _user,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ResumeViewerService>.Instance);
        var result = await sut.GetAsync(Guid.NewGuid());

        Assert.NotEqual(EResultStatus.Success, result.Status);
        Assert.NotEqual(EResultStatus.EntityNotFound, result.Status);
    }

    [Fact]
    public async Task Viewer_PermissionQueryCancelled_Propagates()
    {
        var resumes = Substitute.For<IBaseRepository<Resume>>();
        var candidates = Substitute.For<IBaseRepository<Candidate>>();
        var candidateId = Guid.NewGuid();
        resumes.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<Resume>.Success(new Resume { CandidateId = Id.Create(candidateId) }));
        _user.GetCandidateId().Returns((Guid?)null);
        _user.GetAdvertiserId().Returns(Guid.NewGuid());
        _applications.CanAdvertiserViewResumeAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new OperationCanceledException());

        var sut = new ResumeViewerService(new GetResumeByIdUseCase(resumes), _applications, candidates, _user,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ResumeViewerService>.Instance);
        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.GetAsync(Guid.NewGuid()));
    }
}
