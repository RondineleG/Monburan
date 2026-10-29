using FluentAssertions;
using Monburan.Core.DTOs.Response;
using Monburan.Web.Services;
using Xunit;

namespace Monburan.UnitTests.Web;

public class JobChannelTests
{
    [Fact]
    public void DirectJob_UsesTheInternalCallToActionAndNoNotice()
    {
        var job = new JobResponse { PublicationType = JobChannel.Direct, ApplicationChannel = JobChannel.InternalPlatform, AdvertiserName = "Empresa Direta" };

        JobChannel.PublicationLabel(job).Should().Be("Vaga direta");
        JobChannel.CtaLabel(job).Should().Be("Candidatar-se na Monburan");
        JobChannel.Notice(job).Should().BeNull();
        JobChannel.ExternalHref(job).Should().BeNull();
        JobChannel.CompanyName(job).Should().Be("Empresa Direta");
    }

    [Theory]
    [InlineData(JobChannel.ExternalUrl, "Candidatar-se no site da empresa")]
    [InlineData(JobChannel.WhatsApp, "Enviar mensagem pelo WhatsApp")]
    [InlineData(JobChannel.Email, "Enviar currículo por e-mail")]
    [InlineData(JobChannel.OnSite, "Ver instruções presenciais")]
    public void ExternalChannels_HaveTheirOwnCallToAction(string channel, string label)
    {
        var job = new JobResponse { PublicationType = JobChannel.External, ApplicationChannel = channel };

        JobChannel.CtaLabel(job).Should().Be(label);
        JobChannel.Notice(job).Should().Contain("outro canal");
    }

    [Fact]
    public void ExternalJob_ShowsTheContractingCompanyNotTheAdvertiser()
    {
        var job = new JobResponse { PublicationType = JobChannel.External, CompanyName = "Transportes Exemplo", AdvertiserName = null };

        JobChannel.PublicationLabel(job).Should().Be("Vaga externa");
        JobChannel.CompanyName(job).Should().Be("Transportes Exemplo");
    }

    [Fact]
    public void ExternalUrl_UsesTheSourceAddress()
    {
        var job = new JobResponse { PublicationType = JobChannel.External, ApplicationChannel = JobChannel.ExternalUrl, ApplicationUrl = "https://empresa.exemplo.com.br/vagas/9" };

        JobChannel.ExternalHref(job).Should().Be("https://empresa.exemplo.com.br/vagas/9");
    }

    [Theory]
    [InlineData("(47) 99999-8888", "https://wa.me/5547999998888")]
    [InlineData("5547999998888", "https://wa.me/5547999998888")]
    public void WhatsApp_BuildsAWaMeLinkWithCountryCode(string contact, string expected)
    {
        var job = new JobResponse { PublicationType = JobChannel.External, ApplicationChannel = JobChannel.WhatsApp, ApplicationContact = contact };

        JobChannel.ExternalHref(job).Should().Be(expected);
    }

    [Fact]
    public void Email_BuildsAMailtoLink()
    {
        var job = new JobResponse { PublicationType = JobChannel.External, ApplicationChannel = JobChannel.Email, ApplicationContact = "rh@empresa.exemplo.com.br" };

        JobChannel.ExternalHref(job).Should().Be("mailto:rh@empresa.exemplo.com.br");
    }

    [Fact]
    public void OnSite_HasNoLinkBecauseTheInstructionsAreShown()
    {
        var job = new JobResponse { PublicationType = JobChannel.External, ApplicationChannel = JobChannel.OnSite, ApplicationInstructions = "Agência do Trabalhador" };

        JobChannel.ExternalHref(job).Should().BeNull();
    }
}
