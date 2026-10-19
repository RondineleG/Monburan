using FluentAssertions;
using NSubstitute;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Candidates;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.Candidates;

public class GetCandidateProgressUseCaseTests
{
    private readonly ICandidateRepository _candidates = Substitute.For<ICandidateRepository>();
    private readonly IResumeRepository _resumes = Substitute.For<IResumeRepository>();
    private readonly ICareerProfileRepository _profiles = Substitute.For<ICareerProfileRepository>();
    private readonly IApplicationRepository _applications = Substitute.For<IApplicationRepository>();
    private readonly ISavedJobRepository _savedJobs = Substitute.For<ISavedJobRepository>();
    private readonly IJobAlertRepository _jobAlerts = Substitute.For<IJobAlertRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly GetCandidateProgressUseCase _useCase;

    public GetCandidateProgressUseCaseTests()
    {
        _candidates.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Candidate>.Success(new Candidate
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Maria Souza",
            Email = "maria@teste.com",
            TenantId = "tenant-1"
        }));
        _resumes.CountByCandidateAsync(Arg.Any<Guid>()).Returns(RequestResult<int>.Success(0));
        _profiles.GetByCandidateIdAsync(Arg.Any<Guid>()).Returns(RequestResult<CareerProfile?>.Success(null));
        _applications.CountByStatusForCandidateAsync(Arg.Any<Guid>())
            .Returns(RequestResult<IReadOnlyDictionary<EApplicationStatus, int>>.Success(
                new Dictionary<EApplicationStatus, int>()));
        _savedJobs.CountByCandidateAsync(Arg.Any<Guid>()).Returns(RequestResult<int>.Success(0));
        _jobAlerts.CountActiveByCandidateAsync(Arg.Any<Guid>()).Returns(RequestResult<int>.Success(0));

        _useCase = new GetCandidateProgressUseCase(_candidates, _resumes, _profiles, _applications, _savedJobs, _jobAlerts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCandidateDoesNotExist_ReturnsNotFound()
    {
        _candidates.GetByIdAsync(Arg.Any<Id>())
            .Returns(RequestResult<Candidate>.EntityNotFound("Candidate", _candidateId, "Candidato não encontrado."));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.EntityNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WithOnlyTheName_CountsOneOfSixItems()
    {
        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Checklist.Should().HaveCount(6);
        result.Data.Checklist.Single(item => item.Key == "name").Done.Should().BeTrue();
        result.Data.Checklist.Count(item => item.Done).Should().Be(1);
        result.Data.CompletenessPercent.Should().Be(17);
    }

    [Fact]
    public async Task ExecuteAsync_WithEverythingFilled_ReportsOneHundredPercent()
    {
        _candidates.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Candidate>.Success(new Candidate
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Maria Souza",
            Phone = "11999990000",
            LinkedIn = "linkedin.com/in/maria",
            AddressId = Id.Create(Guid.NewGuid()),
            Email = "maria@teste.com",
            TenantId = "tenant-1"
        }));
        _resumes.CountByCandidateAsync(Arg.Any<Guid>()).Returns(RequestResult<int>.Success(2));
        _profiles.GetByCandidateIdAsync(Arg.Any<Guid>()).Returns(RequestResult<CareerProfile?>.Success(new CareerProfile
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(_candidateId),
            DesiredRole = "Desenvolvedor",
            TenantId = "tenant-1"
        }));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.CompletenessPercent.Should().Be(100);
        result.Data.Checklist.Should().OnlyContain(item => item.Done);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCareerProfileHasOnlyTheCity_CountsTheCareerProfileItem()
    {
        _profiles.GetByCandidateIdAsync(Arg.Any<Guid>()).Returns(RequestResult<CareerProfile?>.Success(new CareerProfile
        {
            Id = Id.Create(Guid.NewGuid()),
            CandidateId = Id.Create(_candidateId),
            City = "São Paulo",
            TenantId = "tenant-1"
        }));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Data!.Checklist.Single(item => item.Key == "careerProfile").Done.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_MapsTheCountsAndTheActionUrls()
    {
        _applications.CountByStatusForCandidateAsync(Arg.Any<Guid>())
            .Returns(RequestResult<IReadOnlyDictionary<EApplicationStatus, int>>.Success(
                new Dictionary<EApplicationStatus, int>
                {
                    [EApplicationStatus.Sent] = 3,
                    [EApplicationStatus.Viewed] = 2,
                    [EApplicationStatus.Selected] = 1
                }));
        _savedJobs.CountByCandidateAsync(Arg.Any<Guid>()).Returns(RequestResult<int>.Success(4));
        _jobAlerts.CountActiveByCandidateAsync(Arg.Any<Guid>()).Returns(RequestResult<int>.Success(2));

        var result = await _useCase.ExecuteAsync(_candidateId);

        result.Data!.Applications.Total.Should().Be(6);
        result.Data.Applications.Sent.Should().Be(3);
        result.Data.Applications.Viewed.Should().Be(2);
        result.Data.Applications.Selected.Should().Be(1);
        result.Data.Applications.Rejected.Should().Be(0);
        result.Data.Applications.Archived.Should().Be(0);
        result.Data.SavedJobs.Should().Be(4);
        result.Data.ActiveAlerts.Should().Be(2);
        result.Data.Checklist.Single(item => item.Key == "resume").ActionUrl.Should().Be("/resumes");
        result.Data.Checklist.Single(item => item.Key == "careerProfile").ActionUrl.Should().Be("/career-profile");
        result.Data.Checklist.Where(item => item.ActionUrl == "/profile/candidate")
            .Should().HaveCount(4);
    }
}
