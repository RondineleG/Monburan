using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.JobAlerts;
using Monburan.Core.UseCases.Jobs;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.UseCases.Subscriptions;
using Monburan.Core.Validations;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.Jobs;

public class CreateJobUseCaseTests
{
    private readonly IBaseRepository<Job> _mockRepository;
    private readonly IEntityValidator<Job> _mockValidator;
    private readonly ISubscriptionRepository _mockSubscriptionRepository;
    private readonly IBaseRepository<SubscriptionPlan> _mockPlanRepository;
    private readonly IJobRepository _mockJobQueries;
    private readonly IJobAlertRepository _mockAlerts;
    private readonly CreateJobUseCase _useCase;

    public CreateJobUseCaseTests()
    {
        _mockRepository = Substitute.For<IBaseRepository<Job>>();
        _mockValidator = Substitute.For<IEntityValidator<Job>>();

        // Sem assinatura ativa → plano Free (5 vagas ativas), nenhuma vaga publicada ainda
        _mockSubscriptionRepository = Substitute.For<ISubscriptionRepository>();
        _mockSubscriptionRepository.GetActiveByAdvertiserIdAsync(Arg.Any<Id>())
            .Returns(RequestResult<Subscription?>.Success(null));
        _mockPlanRepository = Substitute.For<IBaseRepository<SubscriptionPlan>>();
        _mockPlanRepository.GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(RequestResult<IEnumerable<SubscriptionPlan>>.Success(
                [new SubscriptionPlan { Name = CheckJobLimitUseCase.FreePlanName, MaxJobs = 5, IsActive = true }]));
        _mockJobQueries = Substitute.For<IJobRepository>();
        _mockJobQueries.CountActiveByAdvertiserAsync(Arg.Any<Guid>()).Returns(0);

        // Sem alertas ativos por padrão: a criação da vaga não notifica ninguém
        _mockAlerts = Substitute.For<IJobAlertRepository>();
        _mockAlerts.ListActiveAsync().Returns(RequestResult<IEnumerable<JobAlert>>.Success([]));

        var notifyAlerts = new NotifyJobAlertsOnJobCreatedUseCase(
            _mockAlerts,
            new CreateNotificationUseCase(Substitute.For<IBaseRepository<Notification>>()),
            Substitute.For<ILogger<NotifyJobAlertsOnJobCreatedUseCase>>());

        _useCase = new CreateJobUseCase(
            _mockRepository,
            _mockValidator,
            new CheckJobLimitUseCase(_mockSubscriptionRepository, _mockPlanRepository, _mockJobQueries),
            notifyAlerts,
            Substitute.For<ILogger<CreateJobUseCase>>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestIsValid_ReturnsSuccess()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            AdvertiserId = Guid.NewGuid(),
            Title = "Senior Developer",
            Description = "Test description",
            Requirements = "Test requirements",
            Responsibilities = "Test responsibilities",
            Benefits = "Test benefits",
            Category = "Technology",
            Tags = "C#, .NET",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            City = "São Paulo",
            State = "SP",
            Country = "BR",
            Type = "Remote",
            IsRemote = true,
            ShowAddress = false,
            SalaryMin = 5000,
            SalaryMax = 8000,
            SalaryCurrency = "BRL",
            SalaryDisplay = true
        };

        _mockValidator.Validate(Arg.Any<Job>())
            .Returns(ValidationResult.Success());

        var createdJob = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(request.AdvertiserId),
            Title = request.Title,
            Description = request.Description,
            Requirements = request.Requirements,
            Responsibilities = request.Responsibilities,
            Benefits = request.Benefits,
            Category = request.Category,
            Tags = request.Tags,
            ExpirationDate = request.ExpirationDate,
            Status = EJobStatus.Active,
            TenantId = "default",
            Location = new JobLocation
            {
                City = request.City,
                State = request.State,
                Country = request.Country,
                Type = request.Type,
                IsRemote = request.IsRemote,
                ShowAddress = request.ShowAddress
            },
            Salary = new SalaryRange
            {
                Min = request.SalaryMin,
                Max = request.SalaryMax,
                Currency = request.SalaryCurrency,
                Display = request.SalaryDisplay
            },
            Moderation = new ModerationInfo { Status = EModerationStatus.Pending },
            Boost = new JobBoost { IsActive = false },
            Highlight = new JobHighlight { IsActive = false },
            Counters = new JobCounters()
        };

        _mockRepository.CreateAsync(Arg.Any<Job>())
            .Returns(RequestResult<Job>.Success(createdJob));

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().NotBeNull();
        result.Data!.Title.Should().Be("Senior Developer");
        result.Data.City.Should().Be("São Paulo");
        result.Data.SalaryMin.Should().Be(5000);
        result.Data.SalaryMax.Should().Be(8000);

        _mockValidator.Received(1).Validate(Arg.Any<Job>());
        await _mockRepository.Received(1).CreateAsync(Arg.Any<Job>());
        await _mockAlerts.Received(1).ListActiveAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_ReturnsValidationError()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            AdvertiserId = Guid.NewGuid(),
            Title = "", // Invalid title
            Description = "Test description",
            Requirements = "Test requirements",
            Responsibilities = "Test responsibilities",
            Benefits = "Test benefits",
            Category = "Technology",
            Tags = "C#, .NET",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            City = "São Paulo",
            State = "SP",
            Country = "BR",
            Type = "Remote",
            IsRemote = true,
            ShowAddress = false,
            SalaryMin = 5000,
            SalaryMax = 8000,
            SalaryCurrency = "BRL",
            SalaryDisplay = true
        };

        var validationResult = ValidationResult.Failure("Title is required", "Title");

        _mockValidator.Validate(Arg.Any<Job>())
            .Returns(validationResult);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        result.Status.Should().Be(EResultStatus.NoContent);
        result.Data.Should().BeNull();
        result.ValidationResult.Errors.Should().HaveCount(1);
        result.ValidationResult.Errors.First().Field.Should().Be("Title");
        result.ValidationResult.Errors.First().Message.Should().Be("Title is required");

        _mockValidator.Received(1).Validate(Arg.Any<Job>());
        await _mockRepository.DidNotReceive().CreateAsync(Arg.Any<Job>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlanJobLimitReached_ReturnsDomainErrorWithoutCreating()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            AdvertiserId = Guid.NewGuid(),
            Title = "Senior Developer",
            Description = "Test description",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Type = "Remote",
            IsRemote = true
        };

        _mockValidator.Validate(Arg.Any<Job>())
            .Returns(ValidationResult.Success());
        _mockJobQueries.CountActiveByAdvertiserAsync(request.AdvertiserId).Returns(5);

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("5 vagas ativas");
        await _mockRepository.DidNotReceive().CreateAsync(Arg.Any<Job>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlanIsUnlimited_IgnoresActiveJobCount()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            AdvertiserId = Guid.NewGuid(),
            Title = "Senior Developer",
            Description = "Test description",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Type = "OnSite"
        };

        var enterprise = new SubscriptionPlan { Name = "Enterprise", MaxJobs = -1, IsActive = true };
        _mockSubscriptionRepository.GetActiveByAdvertiserIdAsync(Arg.Any<Id>())
            .Returns(RequestResult<Subscription?>.Success(new Subscription { PlanId = enterprise.Id, Plan = enterprise }));
        _mockJobQueries.CountActiveByAdvertiserAsync(request.AdvertiserId).Returns(500);
        _mockValidator.Validate(Arg.Any<Job>())
            .Returns(ValidationResult.Success());
        _mockRepository.CreateAsync(Arg.Any<Job>())
            .Returns(call => RequestResult<Job>.Success(call.Arg<Job>()));

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        result.Status.Should().Be(EResultStatus.Success);
        await _mockRepository.Received(1).CreateAsync(Arg.Any<Job>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryFails_ReturnsError()
    {
        // Arrange
        var request = new CreateJobRequest
        {
            AdvertiserId = Guid.NewGuid(),
            Title = "Senior Developer",
            Description = "Test description",
            Requirements = "Test requirements",
            Responsibilities = "Test responsibilities",
            Benefits = "Test benefits",
            Category = "Technology",
            Tags = "C#, .NET",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            City = "São Paulo",
            State = "SP",
            Country = "BR",
            Type = "Remote",
            IsRemote = true,
            ShowAddress = false,
            SalaryMin = 5000,
            SalaryMax = 8000,
            SalaryCurrency = "BRL",
            SalaryDisplay = true
        };

        _mockValidator.Validate(Arg.Any<Job>())
            .Returns(ValidationResult.Success());

        _mockRepository.CreateAsync(Arg.Any<Job>())
            .Returns(RequestResult<Job>.WithError("Database error"));

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        result.Status.Should().Be(EResultStatus.HasError);
        result.Data.Should().BeNull();

        _mockValidator.Received(1).Validate(Arg.Any<Job>());
        await _mockRepository.Received(1).CreateAsync(Arg.Any<Job>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheAlertEvaluationFails_StillCreatesTheJob()
    {
        var request = new CreateJobRequest
        {
            AdvertiserId = Guid.NewGuid(),
            Title = "Senior Developer",
            Description = "Test description",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Type = "Remote",
            IsRemote = true
        };

        _mockValidator.Validate(Arg.Any<Job>())
            .Returns(ValidationResult.Success());
        _mockRepository.CreateAsync(Arg.Any<Job>())
            .Returns(call => RequestResult<Job>.Success(call.Arg<Job>()));
        _mockAlerts.ListActiveAsync().Returns(RequestResult<IEnumerable<JobAlert>>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.Success);
        await _mockRepository.Received(1).CreateAsync(Arg.Any<Job>());
    }
}
