using System.Net;
using System.Threading.RateLimiting;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

using Monburan.Web.Middleware;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

public class RateLimitingTests
{
    private static PartitionedRateLimiter<HttpContext> Limiter(int perSecond = 1000, int authPerMinute = 3)
    {
        var options = new RateLimiterOptions();
        var security = new SecurityOptions
        {
            RateLimit = { RequestsPerSecondPerIp = perSecond, AuthRequestsPerMinutePerIp = authPerMinute }
        };
        RateLimiting.Configure(options, new StaticOptionsMonitor<SecurityOptions>(security));
        return options.GlobalLimiter!;
    }

    private static HttpContext Request(string method, string path, string ip)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }

    private static bool Allowed(PartitionedRateLimiter<HttpContext> limiter, HttpContext context)
    {
        using var lease = limiter.AttemptAcquire(context);
        return lease.IsAcquired;
    }

    [Theory]
    [InlineData("POST", "/Account/Login", true)]
    [InlineData("POST", "/account/login", true)]
    [InlineData("POST", "/Account/Login/", true)]
    [InlineData("POST", "/Account/Register", true)]
    [InlineData("POST", "/Account/ForgotPassword", true)]
    [InlineData("POST", "/Account/ResetPassword", true)]
    [InlineData("POST", "/Account/ResendEmailConfirmation", true)]
    [InlineData("POST", "/Account/LoginWith2fa", true)]
    [InlineData("POST", "/Account/LoginWithRecoveryCode", true)]
    [InlineData("POST", "/api/auth/login", true)]
    [InlineData("POST", "/api/auth/register", true)]
    [InlineData("POST", "/api/auth/refresh", true)]
    [InlineData("POST", "/api/auth/forgot-password", true)]
    [InlineData("GET", "/Account/Login", false)]
    [InlineData("GET", "/api/auth/login", false)]
    [InlineData("POST", "/Account/Manage/ChangePassword", false)]
    [InlineData("POST", "/api/v1/jobs", false)]
    [InlineData("POST", "/", false)]
    public void IsSensitive_OnlyCoversTheSubmissionsOfLoginRegistrationAndRecovery(string method, string path, bool expected) =>
        RateLimiting.IsSensitive(path, method).Should().Be(expected);

    [Theory]
    [InlineData("203.0.113.9", "203.0.113.9")]
    [InlineData("::ffff:203.0.113.9", "203.0.113.9")]
    [InlineData("2001:db8:1234:5678:aaaa:bbbb:cccc:dddd", "2001:db8:1234:5678::/64")]
    [InlineData("2001:db8:1234:5678:1:2:3:4", "2001:db8:1234:5678::/64")]
    public void ClientKey_IsTheIPv4OrTheIPv6Block(string address, string expected) =>
        RateLimiting.ClientKey(IPAddress.Parse(address)).Should().Be(expected);

    [Fact]
    public void ClientKey_HasAFallbackWhenThereIsNoAddress() => RateLimiting.ClientKey(null).Should().Be("desconhecido");

    [Fact]
    public void Login_IsLimitedPerIp_AfterTheConfiguredAttempts()
    {
        var limiter = Limiter(authPerMinute: 3);

        for (var attempt = 1; attempt <= 3; attempt++)
            Allowed(limiter, Request("POST", "/Account/Login", "203.0.113.1")).Should().BeTrue($"tentativa {attempt}");

        Allowed(limiter, Request("POST", "/Account/Login", "203.0.113.1")).Should().BeFalse();
    }

    [Fact]
    public void TheSensitiveRoutesShareOneBudgetPerIp()
    {
        var limiter = Limiter(authPerMinute: 3);

        Allowed(limiter, Request("POST", "/Account/Login", "203.0.113.2")).Should().BeTrue();
        Allowed(limiter, Request("POST", "/Account/Register", "203.0.113.2")).Should().BeTrue();
        Allowed(limiter, Request("POST", "/api/auth/forgot-password", "203.0.113.2")).Should().BeTrue();

        Allowed(limiter, Request("POST", "/api/auth/login", "203.0.113.2")).Should().BeFalse();
    }

    [Fact]
    public void AnotherIpIsNotAffected()
    {
        var limiter = Limiter(authPerMinute: 2);
        Allowed(limiter, Request("POST", "/Account/Login", "203.0.113.3")).Should().BeTrue();
        Allowed(limiter, Request("POST", "/Account/Login", "203.0.113.3")).Should().BeTrue();
        Allowed(limiter, Request("POST", "/Account/Login", "203.0.113.3")).Should().BeFalse();

        Allowed(limiter, Request("POST", "/Account/Login", "203.0.113.4")).Should().BeTrue();
    }

    [Fact]
    public void ReadingPagesIsNeverCountedAgainstTheLoginBudget()
    {
        var limiter = Limiter(authPerMinute: 1);
        Allowed(limiter, Request("POST", "/Account/Login", "203.0.113.5")).Should().BeTrue();
        Allowed(limiter, Request("POST", "/Account/Login", "203.0.113.5")).Should().BeFalse();

        for (var index = 0; index < 20; index++)
            Allowed(limiter, Request("GET", "/Account/Login", "203.0.113.5")).Should().BeTrue();
    }

    [Fact]
    public void TheGeneralBucketIsPerIp_AndEmptiesAtTwiceTheRate()
    {
        var limiter = Limiter(perSecond: 5, authPerMinute: 100);
        var allowed = Enumerable.Range(0, 30).Count(_ => Allowed(limiter, Request("GET", "/jobs", "203.0.113.6")));

        allowed.Should().Be(10, "a rajada aceita é o dobro por segundo");
        Allowed(limiter, Request("GET", "/jobs", "203.0.113.7")).Should().BeTrue("outro IP tem o próprio balde");
    }

    [Fact]
    public void HealthIsNeverLimited()
    {
        var limiter = Limiter(perSecond: 1, authPerMinute: 100);

        Enumerable.Range(0, 50).All(_ => Allowed(limiter, Request("GET", "/health", "203.0.113.8"))).Should().BeTrue();
    }

    [Fact]
    public void ARequestWithoutAnAddressStillGetsALimit()
    {
        var limiter = Limiter(authPerMinute: 1);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/Account/Login";

        Allowed(limiter, context).Should().BeTrue();
        Allowed(limiter, context).Should().BeFalse();
    }
}
