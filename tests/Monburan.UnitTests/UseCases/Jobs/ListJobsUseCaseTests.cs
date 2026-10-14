using FluentAssertions;
using NSubstitute;
using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Jobs;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.Jobs;

public class ListJobsUseCaseTests
{
    private readonly IJobRepository _mockRepository;
    private readonly ListJobsUseCase _useCase;

    public ListJobsUseCaseTests()
    {
        _mockRepository = Substitute.For<IJobRepository>();
        _useCase = new ListJobsUseCase(_mockRepository);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobsExist_ReturnsPagedResponse()
    {
        // Arrange
        var request = new ListJobsRequest { Page = 1, Size = 10 };

        var jobs = new List<Job>
        {
            new Job
            {
                Id = Id.Create(Guid.NewGuid()),
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
            },
            new Job
            {
                Id = Id.Create(Guid.NewGuid()),
                AdvertiserId = Id.Create(Guid.NewGuid()),
                Title = "Junior Developer",
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
                    City = "Rio de Janeiro",
                    State = "RJ",
                    Country = "BR",
                    Type = "OnSite",
                    IsRemote = false,
                    ShowAddress = true
                },
                Salary = new SalaryRange
                {
                    Min = 3000,
                    Max = 5000,
                    Currency = "BRL",
                    Display = true
                },
                Moderation = new ModerationInfo { Status = EModerationStatus.Approved },
                Boost = new JobBoost { IsActive = false },
                Highlight = new JobHighlight { IsActive = false },
                Counters = new JobCounters { Views = 50, Applications = 2 }
            }
        };

        var pagedResult = new PagedResponse<Job>
        {
            Data = jobs,
            TotalCount = 2,
            Page = 1,
            Size = 10
        };

        _mockRepository.GetAllPagedAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(pagedResult);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().NotBeNull();
        result.Data!.Jobs.Should().HaveCount(2);
        result.Data.TotalCount.Should().Be(2);
        result.Data.Page.Should().Be(1);
        result.Data.Size.Should().Be(10);
        result.Data.Jobs.First().Title.Should().Be("Senior Developer");
        result.Data.Jobs.Last().Title.Should().Be("Junior Developer");

        await _mockRepository.Received(1).GetAllPagedAsync(1, 10);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoJobsExist_ReturnsEmptyList()
    {
        // Arrange
        var request = new ListJobsRequest { Page = 1, Size = 10 };

        var pagedResult = new PagedResponse<Job>
        {
            Data = new List<Job>(),
            TotalCount = 0,
            Page = 1,
            Size = 10
        };

        _mockRepository.GetAllPagedAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(pagedResult);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().NotBeNull();
        result.Data!.Jobs.Should().BeEmpty();
        result.Data.TotalCount.Should().Be(0);

        await _mockRepository.Received(1).GetAllPagedAsync(1, 10);
    }

    [Fact]
    public async Task ExecuteAsync_WithPaginationParameters_ReturnsCorrectPage()
    {
        // Arrange
        var request = new ListJobsRequest { Page = 2, Size = 5 };

        var jobs = new List<Job>
        {
            new Job
            {
                Id = Id.Create(Guid.NewGuid()),
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
            }
        };

        var pagedResult = new PagedResponse<Job>
        {
            Data = jobs,
            TotalCount = 15,
            Page = 2,
            Size = 5
        };

        _mockRepository.GetAllPagedAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(pagedResult);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().NotBeNull();
        result.Data!.Page.Should().Be(2);
        result.Data.Size.Should().Be(5);
        result.Data.TotalCount.Should().Be(15);

        await _mockRepository.Received(1).GetAllPagedAsync(2, 5);
    }
}
