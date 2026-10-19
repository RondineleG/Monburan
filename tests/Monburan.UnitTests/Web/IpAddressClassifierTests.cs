using System.Net;

using FluentAssertions;

using Monburan.Web.Services;

namespace Monburan.UnitTests.Web;

/// <summary>Defesa contra SSRF: um provedor de IA nunca pode levar o servidor a conectar na rede interna.</summary>
public class IpAddressClassifierTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("104.18.7.1")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.1")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.1")]
    [InlineData("198.17.255.255")]
    [InlineData("198.20.0.1")]
    [InlineData("203.0.114.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("2001:4860:4860::8888")]
    [InlineData("::ffff:8.8.8.8")]
    public void IsPublic_AcceptsPublicAddresses(string address) =>
        IpAddressClassifier.IsPublic(IPAddress.Parse(address)).Should().BeTrue();

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("127.0.0.1")]
    [InlineData("127.255.255.254")]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.255")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.1")]
    [InlineData("192.168.255.255")]
    [InlineData("169.254.169.254")] // metadados da nuvem
    [InlineData("169.254.0.1")]
    [InlineData("100.64.0.1")] // CGNAT
    [InlineData("100.127.255.255")]
    [InlineData("192.0.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("198.18.0.1")]
    [InlineData("198.19.255.255")]
    [InlineData("198.51.100.7")]
    [InlineData("203.0.113.9")]
    [InlineData("224.0.0.1")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fec0::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456:789a::1")]
    [InlineData("ff02::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2002:c0a8:101::1")] // 6to4 de 192.168.1.1
    [InlineData("2001:0:4136:e378:8000:63bf:3fff:fdd2")] // Teredo
    [InlineData("64:ff9b::7f00:1")] // NAT64 de 127.0.0.1
    [InlineData("100::1")]
    [InlineData("::127.0.0.1")] // IPv4 embutido no formato antigo
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:10.1.2.3")]
    [InlineData("::ffff:169.254.169.254")]
    public void IsPublic_RejectsInternalAndReservedAddresses(string address) =>
        IpAddressClassifier.IsPublic(IPAddress.Parse(address)).Should().BeFalse();
}
