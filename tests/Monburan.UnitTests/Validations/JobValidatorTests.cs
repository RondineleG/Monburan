using FluentAssertions;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Validations;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.Validations;

public class JobValidatorTests
{
    private readonly JobValidator _validator;

    public JobValidatorTests()
    {
        _validator = new JobValidator();
    }

    [Fact]
    public void Validate_WithValidJob_ReturnsSuccess()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "Senior Developer",
            Description = "Test description with at least 10 characters",
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
            Counters = new JobCounters()
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithNullAdvertiserId_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = null!,
            Title = "Senior Developer",
            Description = "Test description with at least 10 characters",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "AdvertiserId");
    }

    [Fact]
    public void Validate_WithEmptyTitle_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "",
            Description = "Test description with at least 10 characters",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Title");
    }

    [Fact]
    public void Validate_WithTitleTooShort_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "AB",
            Description = "Test description with at least 10 characters",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Title");
    }

    [Fact]
    public void Validate_WithTitleTooLong_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = new string('A', 201),
            Description = "Test description with at least 10 characters",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Title");
    }

    [Fact]
    public void Validate_WithEmptyDescription_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "Senior Developer",
            Description = "",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Description");
    }

    [Fact]
    public void Validate_WithDescriptionTooShort_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "Senior Developer",
            Description = "Short",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Description");
    }

    [Fact]
    public void Validate_WithDescriptionTooLong_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "Senior Developer",
            Description = new string('A', 2001),
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Description");
    }

    [Fact]
    public void Validate_WithPastExpirationDate_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "Senior Developer",
            Description = "Test description with at least 10 characters",
            ExpirationDate = DateTime.UtcNow.AddDays(-1),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "ExpirationDate");
    }

    [Fact]
    public void Validate_WithSalaryMinGreaterThanMax_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "Senior Developer",
            Description = "Test description with at least 10 characters",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default",
            Salary = new SalaryRange
            {
                Min = 8000,
                Max = 5000,
                Currency = "BRL",
                Display = true
            }
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Salary");
    }

    [Fact]
    public void Validate_WithRequirementsTooLong_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "Senior Developer",
            Description = "Test description with at least 10 characters",
            Requirements = new string('A', 2001),
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Requirements");
    }

    [Fact]
    public void Validate_WithResponsibilitiesTooLong_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "Senior Developer",
            Description = "Test description with at least 10 characters",
            Responsibilities = new string('A', 2001),
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Responsibilities");
    }

    [Fact]
    public void Validate_WithBenefitsTooLong_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "Senior Developer",
            Description = "Test description with at least 10 characters",
            Benefits = new string('A', 2001),
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Benefits");
    }

    [Fact]
    public void Validate_WithCategoryTooLong_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "Senior Developer",
            Description = "Test description with at least 10 characters",
            Category = new string('A', 101),
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Category");
    }

    [Fact]
    public void Validate_WithTagsTooLong_ReturnsError()
    {
        // Arrange
        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(Guid.NewGuid()),
            Title = "Senior Developer",
            Description = "Test description with at least 10 characters",
            Tags = new string('A', 501),
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = _validator.Validate(job);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "Tags");
    }
}
