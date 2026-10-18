using FluentAssertions;

using Microsoft.Extensions.Options;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class MemoryAiUsageLimiterTests
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly AiOptions _options = new() { MaxRequestsPerHour = 3 };

    private MemoryAiUsageLimiter Limiter() => new(new StaticOptionsMonitor<AiOptions>(_options), _time);

    [Fact]
    public void TryAcquire_AllowsUpToTheLimit_ThenTellsHowLongToWait()
    {
        var limiter = Limiter();

        limiter.TryAcquire("ana", out _).Should().BeTrue();
        _time.Advance(TimeSpan.FromMinutes(10));
        limiter.TryAcquire("ana", out _).Should().BeTrue();
        _time.Advance(TimeSpan.FromMinutes(10));
        limiter.TryAcquire("ana", out _).Should().BeTrue();

        _time.Advance(TimeSpan.FromMinutes(10));
        limiter.TryAcquire("ana", out var retryAfter).Should().BeFalse();

        // a primeira chamada foi há 30 minutos: libera daqui a 30
        retryAfter.Should().BeCloseTo(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TryAcquire_SlidesTheWindow()
    {
        var limiter = Limiter();
        for (var index = 0; index < 3; index++)
            limiter.TryAcquire("ana", out _).Should().BeTrue();
        limiter.TryAcquire("ana", out _).Should().BeFalse();

        _time.Advance(TimeSpan.FromHours(1));

        limiter.TryAcquire("ana", out _).Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_CountsEachKeySeparately()
    {
        var limiter = Limiter();
        for (var index = 0; index < 3; index++)
            limiter.TryAcquire("ana", out _);

        limiter.TryAcquire("ana", out _).Should().BeFalse();
        limiter.TryAcquire("bia", out _).Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_FollowsTheConfiguredLimit_AndAlwaysAllowsAtLeastOne()
    {
        _options.MaxRequestsPerHour = 0;
        var limiter = Limiter();

        limiter.TryAcquire("ana", out _).Should().BeTrue();
        limiter.TryAcquire("ana", out _).Should().BeFalse();
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public ManualTimeProvider(DateTimeOffset now) => _now = now;

        public void Advance(TimeSpan time) => _now += time;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}

/// <summary>IOptionsMonitor com valor fixo, para testes.</summary>
internal sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue => value;

    public T Get(string? name) => value;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
