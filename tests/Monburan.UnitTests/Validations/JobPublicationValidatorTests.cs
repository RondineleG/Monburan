using FluentAssertions;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Validations;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.Validations;

/// <summary>
/// Regras de publicação: vaga direta (anunciante, candidatura na Monburan) versus vaga externa
/// (empresa contratante e fonte informadas, candidatura em canal externo) e exigências de cada canal.
/// </summary>
public class JobPublicationValidatorTests
{
    private readonly JobValidator _validator = new();

    [Fact]
    public void DirectJob_WithAdvertiserAndInternalChannel_IsValid()
    {
        var job = DirectJob();

        _validator.Validate(job).IsValid.Should().BeTrue();
    }

    [Fact]
    public void DirectJob_WithoutAdvertiser_IsRejected()
    {
        var job = DirectJob();
        job.AdvertiserId = null;

        var result = _validator.Validate(job);

        result.Errors.Should().Contain(e => e.Field == "AdvertiserId");
    }

    [Fact]
    public void DirectJob_WithExternalChannel_IsRejected()
    {
        var job = DirectJob();
        job.ApplicationChannel = EApplicationChannel.WhatsApp;
        job.ApplicationContact = "47999998888";

        var result = _validator.Validate(job);

        result.Errors.Should().Contain(e => e.Field == "ApplicationChannel");
    }

    [Fact]
    public void ExternalJob_WithCompanySourceAndUrl_IsValidWithoutAdvertiserOrSalary()
    {
        var job = ExternalJob();
        job.ApplicationChannel = EApplicationChannel.ExternalUrl;
        job.ApplicationUrl = "https://empresa.exemplo.com.br/vagas/1";

        var result = _validator.Validate(job);

        result.IsValid.Should().BeTrue();
        job.Salary.Should().BeNull("salário é opcional e nunca é estimado");
    }

    [Fact]
    public void ExternalJob_WithAdvertiser_IsRejected()
    {
        var job = ExternalJob();
        job.AdvertiserId = Id.Create(Guid.NewGuid());
        job.ApplicationChannel = EApplicationChannel.Email;
        job.ApplicationContact = "rh@empresa.exemplo.com.br";

        var result = _validator.Validate(job);

        result.Errors.Should().Contain(e => e.Field == "AdvertiserId");
    }

    [Fact]
    public void ExternalJob_WithoutCompany_IsRejected()
    {
        var job = ExternalJob();
        job.CompanyName = null;
        job.ApplicationChannel = EApplicationChannel.Email;
        job.ApplicationContact = "rh@empresa.exemplo.com.br";

        _validator.Validate(job).Errors.Should().Contain(e => e.Field == "CompanyName");
    }

    [Fact]
    public void ExternalJob_WithoutSource_IsRejected()
    {
        var job = ExternalJob();
        job.SourceName = null;
        job.SourceUrl = null;
        job.ApplicationChannel = EApplicationChannel.Email;
        job.ApplicationContact = "rh@empresa.exemplo.com.br";

        _validator.Validate(job).Errors.Should().Contain(e => e.Field == "SourceName");
    }

    [Fact]
    public void ExternalJob_WithInternalChannel_IsRejected()
    {
        var job = ExternalJob();
        job.ApplicationChannel = EApplicationChannel.InternalPlatform;

        _validator.Validate(job).Errors.Should().Contain(e => e.Field == "ApplicationChannel");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("site sem protocolo.com.br")]
    [InlineData("ftp://empresa.exemplo.com.br/vagas")]
    public void ExternalUrlChannel_RequiresHttpOrHttpsAddress(string? url)
    {
        var job = ExternalJob();
        job.ApplicationChannel = EApplicationChannel.ExternalUrl;
        job.ApplicationUrl = url;

        _validator.Validate(job).Errors.Should().Contain(e => e.Field == "ApplicationUrl");
    }

    [Theory]
    [InlineData("47 9999-8888", false)]
    [InlineData("(47) 99999-8888", true)]
    [InlineData("47999998888", true)]
    [InlineData(null, false)]
    public void WhatsAppChannel_RequiresPhoneWithAreaCode(string? contact, bool valid)
    {
        var job = ExternalJob();
        job.ApplicationChannel = EApplicationChannel.WhatsApp;
        job.ApplicationContact = contact;

        var hasError = _validator.Validate(job).Errors.Any(e => e.Field == "ApplicationContact");

        hasError.Should().Be(!valid);
    }

    [Theory]
    [InlineData("rh@empresa.exemplo.com.br", true)]
    [InlineData("rh-at-empresa", false)]
    [InlineData(null, false)]
    public void EmailChannel_RequiresValidAddress(string? contact, bool valid)
    {
        var job = ExternalJob();
        job.ApplicationChannel = EApplicationChannel.Email;
        job.ApplicationContact = contact;

        var hasError = _validator.Validate(job).Errors.Any(e => e.Field == "ApplicationContact");

        hasError.Should().Be(!valid);
    }

    [Fact]
    public void OnSiteChannel_RequiresInstructions()
    {
        var job = ExternalJob();
        job.ApplicationChannel = EApplicationChannel.OnSite;
        job.ApplicationInstructions = null;

        _validator.Validate(job).Errors.Should().Contain(e => e.Field == "ApplicationInstructions");

        job.ApplicationInstructions = "Agência do Trabalhador, Rua das Flores, 100, térreo. Levar RG e CTPS.";
        _validator.Validate(job).Errors.Should().NotContain(e => e.Field == "ApplicationInstructions");
    }

    [Fact]
    public void DefaultsKeepExistingDirectJobsCompatible()
    {
        var job = new Job();

        job.PublicationType.Should().Be(EPublicationType.Direct);
        job.ApplicationChannel.Should().Be(EApplicationChannel.InternalPlatform);
    }

    private static Job DirectJob() => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        AdvertiserId = Id.Create(Guid.NewGuid()),
        Title = "Auxiliar administrativo",
        Description = "Atendimento, arquivo e rotinas do escritório.",
        ExpirationDate = DateTime.UtcNow.AddDays(30),
        Status = EJobStatus.Active,
        PublicationType = EPublicationType.Direct,
        ApplicationChannel = EApplicationChannel.InternalPlatform,
        TenantId = "default"
    };

    private static Job ExternalJob() => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        AdvertiserId = null,
        Title = "Motorista de carreta",
        Description = "Transporte de cargas em rodovias estaduais e federais.",
        ExpirationDate = DateTime.UtcNow.AddDays(30),
        Status = EJobStatus.Active,
        PublicationType = EPublicationType.External,
        CompanyName = "Transportes Exemplo Ltda",
        SourceName = "Página da empresa no Instagram",
        SourceUrl = "https://instagram.com/exemplo/p/123",
        TenantId = "default"
    };
}
