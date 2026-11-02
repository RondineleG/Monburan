using FluentAssertions;
using Monburan.Web.Services;
using Xunit;

namespace Monburan.UnitTests.Web;

public class SubmissionRateLimiterTests
{
    [Fact]
    public void AllowsUpToTheLimit_ThenRefusesWithARetryTime()
    {
        using var limiter = new SubmissionRateLimiter(3, TimeSpan.FromHours(1));

        for (var i = 0; i < 3; i++)
            limiter.TryAcquire("a", out _).Should().BeTrue();

        limiter.TryAcquire("a", out var retryAfter).Should().BeFalse();
        retryAfter.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void EachSenderHasItsOwnCounter()
    {
        using var limiter = new SubmissionRateLimiter(1, TimeSpan.FromHours(1));

        limiter.TryAcquire("a", out _).Should().BeTrue();
        limiter.TryAcquire("a", out _).Should().BeFalse();
        limiter.TryAcquire("b", out _).Should().BeTrue();
    }

    [Fact]
    public void IsExhausted_DoesNotConsumeAPermit()
    {
        using var limiter = new SubmissionRateLimiter(1, TimeSpan.FromHours(1));

        limiter.IsExhausted("a").Should().BeFalse();
        limiter.IsExhausted("a").Should().BeFalse();
        limiter.TryAcquire("a", out _).Should().BeTrue();
        limiter.IsExhausted("a").Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentCalls_NeverExceedTheLimit()
    {
        using var limiter = new SubmissionRateLimiter(5, TimeSpan.FromHours(1));

        var results = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(index => Task.Run(() => limiter.TryAcquire("a", out _))));

        results.Count(r => r).Should().Be(5);
    }
}
