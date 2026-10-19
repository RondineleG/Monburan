using FluentAssertions;
using NSubstitute;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Jobs;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.Jobs;

public class GetJobByIdUseCaseTests
{
    private readonly IBaseRepository<Job> _mockRepository;
    private readonly ISavedJobRepository _mockSavedJobs;
    private readonly GetJobByIdUseCase _useCase;

    public GetJobByIdUseCaseTests()
    {
        _mockRepository = Substitute.For<IBaseRepository<Job>>();
        _mockSavedJobs = Substitute.For<ISavedJobRepository>();
        _useCase = new GetJobByIdUseCase(_mockRepository, _mockSavedJobs);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobExists_ReturnsJobResponse()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var job = new Job
        {
            Id = Id.Create(jobId),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "Senior Developer",
            Description = "Test description",
            Requirements = "Test requirements",
            Responsibilities = "Test responsibilities",
            Benefits = "Test benefits",
            Category = "Technology",
            Tags = "C#, .NET",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default",
            Location = new JobLocation
            {
                City = "São Paulo",
                State = "SP",
                Country = "BR",
                Type = "Remote",
                IsRemote = true,
                ShowAddress = false
            },
            Salary = new SalaryRange
            {
                Min = 5000,
                Max = 8000,
                Currency = "BRL",
                Display = true
            },
            Moderation = new ModerationInfo { Status = EModerationStatus.Approved },
            Boost = new JobBoost { IsActive = false },
            Highlight = new JobHighlight { IsActive = false },
            Counters = new JobCounters { Views = 100, Applications = 5 }
        };

        _mockRepository.GetByIdAsync(Arg.Any<Id>())
            .Returns(RequestResult<Job>.Success(job));

        // Act
        var result = await _useCase.ExecuteAsync(jobId);

        // Assert
        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().NotBeNull();
        result.Data!.Title.Should().Be("Senior Developer");
        result.Data.Id.Should().Be(jobId);
        result.Data.City.Should().Be("São Paulo");
        result.Data.SalaryMin.Should().Be(5000);
        result.Data.SalaryMax.Should().Be(8000);
        result.Data.Views.Should().Be(100);
        result.Data.Applications.Should().Be(5);

        await _mockRepository.Received(1).GetByIdAsync(Arg.Any<Id>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobDoesNotExist_ReturnsError()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _mockRepository.GetByIdAsync(Arg.Any<Id>())
            .Returns(RequestResult<Job>.WithError("Job not found"));

        // Act
        var result = await _useCase.ExecuteAsync(jobId);

        // Assert
        result.Status.Should().Be(EResultStatus.HasError);
        result.Data.Should().BeNull();

        await _mockRepository.Received(1).GetByIdAsync(Arg.Any<Id>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryReturnsSuccessWithNullData_ThrowsNullReferenceException()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _mockRepository.GetByIdAsync(Arg.Any<Id>())
            .Returns(RequestResult<Job>.Success(null!));

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() => _useCase.ExecuteAsync(jobId));

        await _mockRepository.Received(1).GetByIdAsync(Arg.Any<Id>());
    }

    private static Job JobOf(Guid jobId) => new()
    {
        Id = Id.Create(jobId),
        AdvertiserId = Id.Create(Guid.NewGuid()),
        Title = "Senior Developer",
        Description = "Test description",
        ExpirationDate = DateTime.UtcNow.AddDays(30),
        Status = EJobStatus.Active,
        TenantId = "default"
    };

    [Fact]
    public async Task ExecuteAsync_WithAuthenticatedCandidateThatSavedTheJob_MarksIsSaved()
    {
        var jobId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        _mockRepository.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(JobOf(jobId)));
        _mockSavedJobs.ListSavedJobIdsAsync(candidateId, Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(RequestResult<IReadOnlyCollection<Guid>>.Success([jobId]));

        var result = await _useCase.ExecuteAsync(jobId, candidateId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.IsSaved.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheSavedLookupFails_StillReturnsTheJob()
    {
        var jobId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        _mockRepository.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(JobOf(jobId)));
        _mockSavedJobs.ListSavedJobIdsAsync(candidateId, Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(RequestResult<IReadOnlyCollection<Guid>>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(jobId, candidateId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.IsSaved.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutCandidate_NeverQueriesSavedJobs()
    {
        var jobId = Guid.NewGuid();
        _mockRepository.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(JobOf(jobId)));

        var result = await _useCase.ExecuteAsync(jobId);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.IsSaved.Should().BeFalse();
        await _mockSavedJobs.DidNotReceiveWithAnyArgs().ListSavedJobIdsAsync(default, default!);
    }
}
