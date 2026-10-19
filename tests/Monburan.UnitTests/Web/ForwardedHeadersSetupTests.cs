using System.Net;

using FluentAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

/// <summary>Até onde o app confia nos cabeçalhos X-Forwarded-*: só no proxy local ou da rede interna.</summary>
public class ForwardedHeadersSetupTests
{
    private static async Task<HttpContext> RunAsync(string peer, string? forwardedFor = null, string? forwardedProto = null)
    {
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Apply(options);
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));

        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        if (forwardedFor is not null)
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        if (forwardedProto is not null)
            context.Request.Headers["X-Forwarded-Proto"] = forwardedProto;

        await middleware.Invoke(context);
        return context;
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("172.18.0.1")] // ponte do Docker: é por onde o nginx do host chega ao contêiner
    [InlineData("10.1.2.3")]
    [InlineData("192.168.0.20")]
    public async Task TrustsTheHeadersOfALocalOrInternalProxy(string peer)
    {
        var context = await RunAsync(peer, "203.0.113.9", "https");

        context.Request.Scheme.Should().Be("https");
        context.Request.IsHttps.Should().BeTrue();
        context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("203.0.113.9"));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("203.0.113.50")]
    [InlineData("2606:4700:4700::1111")]
    public async Task IgnoresTheHeadersOfAPublicAddress_SoNobodyCanForgeTheIpOrTheScheme(string peer)
    {
        var context = await RunAsync(peer, "10.9.9.9", "https");

        context.Request.Scheme.Should().Be("http");
        context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse(peer));
    }

    [Fact]
    public async Task OnlyTheLastForwardedAddressCounts_WhatTheClientPrependedIsIgnored()
    {
        // O nginx acrescenta o IP que viu ao fim do que o cliente mandou: o cliente não escolhe o IP que o app usa
        var context = await RunAsync("172.18.0.1", "198.51.100.7, 203.0.113.9", "https");

        context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("203.0.113.9"));
    }

    [Fact]
    public async Task LeavesTheRequestAloneWithoutTheHeaders()
    {
        var context = await RunAsync("172.18.0.1");

        context.Request.Scheme.Should().Be("http");
        context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("172.18.0.1"));
    }

    [Fact]
    public async Task DoesNotRewriteTheHost()
    {
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Apply(options);

        options.ForwardedHeaders.Should().HaveFlag(ForwardedHeaders.XForwardedFor).And.HaveFlag(ForwardedHeaders.XForwardedProto);
        options.ForwardedHeaders.Should().NotHaveFlag(ForwardedHeaders.XForwardedHost);
        options.ForwardLimit.Should().Be(1);
        await Task.CompletedTask;
    }
}
