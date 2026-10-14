using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Repositories;
using Xunit;

namespace Monburan.IntegrationTests.Repositories;

public class JobRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly JobRepository _repository;
    private readonly ServiceProvider _serviceProvider;

    public JobRepositoryTests()
    {
        var services = new ServiceCollection();

        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        _repository = new JobRepository(
            _serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());

        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task CreateAsync_WithValidJob_ReturnsSuccess()
    {
        // Arrange
        var advertiser = new Advertiser
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Test Company",
            Email = "test@company.com",
            TenantId = "default"
        };

        _context.Advertisers.Add(advertiser);
        await _context.SaveChangesAsync();

        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = advertiser.Id,
            Title = "Senior Developer",
            Description = "Test description with enough characters",
            Requirements = "Test requirements",
            Responsibilities = "Test responsibilities",
            Benefits = "Test benefits",
            Category = "Technology",
            Tags = "C#, .NET",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        // Act
        var result = await _repository.CreateAsync(job);

        // Assert
        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(job.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingJob_ReturnsJob()
    {
        // Arrange
        var advertiser = new Advertiser
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Test Company",
            Email = "test@company.com",
            TenantId = "default"
        };

        _context.Advertisers.Add(advertiser);
        await _context.SaveChangesAsync();

        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = advertiser.Id,
            Title = "Senior Developer",
            Description = "Test description with enough characters",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(job.Id);

        // Assert
        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(job.Id);
        result.Data.Title.Should().Be("Senior Developer");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingJob_ReturnsNotFound()
    {
        // Arrange
        var nonExistingId = Id.Create(Guid.NewGuid());

        // Act
        var result = await _repository.GetByIdAsync(nonExistingId);

        // Assert
        result.Status.Should().Be(EResultStatus.EntityNotFound);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetAllPagedAsync_WithJobs_ReturnsPagedResponse()
    {
        // Arrange
        var advertiser = new Advertiser
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Test Company",
            Email = "test@company.com",
            TenantId = "default"
        };

        _context.Advertisers.Add(advertiser);
        await _context.SaveChangesAsync();

        for (int i = 0; i < 5; i++)
        {
            var job = new Job
            {
                Id = Id.Create(Guid.NewGuid()),
                AdvertiserId = advertiser.Id,
                Title = $"Job {i}",
                Description = $"Test description {i} with enough characters",
                ExpirationDate = DateTime.UtcNow.AddDays(30),
                Status = EJobStatus.Active,
                TenantId = "default"
            };

            _context.Jobs.Add(job);
        }

        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllPagedAsync(1, 10);

        // Assert
        result.Data.Should().NotBeNull();
        result.Data.Should().HaveCount(5);
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(1);
        result.Size.Should().Be(10);
    }

    [Fact]
    public async Task UpdateAsync_WithValidJob_ReturnsSuccess()
    {
        // Arrange
        var advertiser = new Advertiser
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Test Company",
            Email = "test@company.com",
            TenantId = "default"
        };

        _context.Advertisers.Add(advertiser);
        await _context.SaveChangesAsync();

        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = advertiser.Id,
            Title = "Senior Developer",
            Description = "Test description with enough characters",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();

        job.Title = "Updated Title";

        // Act
        var result = await _repository.UpdateAsync(job);

        // Assert
        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().NotBeNull();
        result.Data!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task DeleteAsync_WithExistingJob_ReturnsSuccess()
    {
        // Arrange
        var advertiser = new Advertiser
        {
            Id = Id.Create(Guid.NewGuid()),
            Name = "Test Company",
            Email = "test@company.com",
            TenantId = "default"
        };

        _context.Advertisers.Add(advertiser);
        await _context.SaveChangesAsync();

        var job = new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = advertiser.Id,
            Title = "Senior Developer",
            Description = "Test description with enough characters",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = EJobStatus.Active,
            TenantId = "default"
        };

        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(job.Id);

        // Assert
        result.Status.Should().Be(EResultStatus.Success);
        result.Data.Should().NotBeNull();
        result.Data!.IsDeleted.Should().BeTrue();
        result.Data.DeletedAt.Should().NotBeNull();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        _serviceProvider.Dispose();
    }
}
