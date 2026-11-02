using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Monburan.Web.Data;
using Monburan.Web.Services;
using NSubstitute;
using Xunit;

namespace Monburan.UnitTests.Web;

public class FeatureFlagServiceFailureCacheTests
{
    [Fact]
    public async Task WhenTheDatabaseIsDown_TheFailureIsCached_SoRequestsDoNotAllWaitForTheConnectionTimeout()
    {
        var factory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns<Task<ApplicationDbContext>>(_ => throw new InvalidOperationException("banco fora do ar"));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new FeatureFlagService(factory, cache, NullLogger<FeatureFlagService>.Instance);

        for (var i = 0; i < 20; i++)
            (await service.IsEnabledAsync("qualquer-flag", null)).Should().BeFalse();

        await factory.Received(1).CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalidate_DropsTheCachedFailure_SoTheNextReadTriesTheDatabaseAgain()
    {
        var factory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns<Task<ApplicationDbContext>>(_ => throw new InvalidOperationException("banco fora do ar"));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new FeatureFlagService(factory, cache, NullLogger<FeatureFlagService>.Instance);

        await service.IsEnabledAsync("qualquer-flag", null);
        service.Invalidate();
        await service.IsEnabledAsync("qualquer-flag", null);

        await factory.Received(2).CreateDbContextAsync(Arg.Any<CancellationToken>());
    }
}
