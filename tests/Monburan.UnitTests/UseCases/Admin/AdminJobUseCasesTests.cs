using FluentAssertions;

using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Services;
using Monburan.Core.UseCases.Admin;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.ValueObjects;

namespace Monburan.UnitTests.UseCases.Admin;

public class AdminChangeJobStatusUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly AdminActor Admin = new("admin-1", "admin@monburan.com.br");

    private readonly FakeJobRepository _jobs = new();
    private readonly FakeAdvertiserRepository _advertisers = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeAuditLog _audit = new();
    private readonly Advertiser _advertiser;

    public AdminChangeJobStatusUseCaseTests()
    {
        _advertiser = _advertisers.Add(new Advertiser
        {
            Id = Id.Create(Guid.NewGuid()), TenantId = "default", Name = "Empresa Alfa", Email = "alfa@exemplo.com", Status = EAdvertiserStatus.Active
        });
    }

    private AdminChangeJobStatusUseCase UseCase() =>
        new(_jobs, _advertisers, new CreateNotificationUseCase(_notifications), _audit, new FixedTimeProvider(Now));

    private Job NewJob(EJobStatus status = EJobStatus.Active, int daysToExpire = 20, string? reason = null) => _jobs.Add(new Job
    {
        Id = Id.Create(Guid.NewGuid()),
        TenantId = "default",
        AdvertiserId = _advertiser.Id,
        Advertiser = _advertiser,
        Title = "Desenvolvedor Pleno",
        Description = "Descrição.",
        Status = status,
        ExpirationDate = Now.UtcDateTime.AddDays(daysToExpire),
        Moderation = new ModerationInfo { Status = EModerationStatus.Approved, Reason = reason }
    });

    private static AdminChangeJobStatusRequest Request(Job job, EJobStatus status, string? reason = null, DateTime? renew = null) =>
        new(job.Id.Value, status, reason, renew, Admin);

    [Theory]
    [InlineData(EJobStatus.Hidden)]
    [InlineData(EJobStatus.Rejected)]
    public async Task HidingOrRejecting_NeedsAReasonTheCompanyCanRead(EJobStatus status)
    {
        var job = NewJob();

        foreach (var reason in new[] { null, "  ", "abc" })
        {
            var result = await UseCase().ExecuteAsync(Request(job, status, reason));
            result.Status.Should().Be(EResultStatus.HasValidation);
        }

        (await UseCase().ExecuteAsync(Request(job, status, new string('x', 501)))).Status.Should().Be(EResultStatus.HasValidation);
        job.Status.Should().Be(EJobStatus.Active);
        _jobs.UpdateCalls.Should().Be(0);
        _notifications.Created.Should().BeEmpty();
        _audit.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Hide_TakesTheJobDown_RecordsTheModeration_NotifiesTheCompany_AndIsAudited()
    {
        var job = NewJob();

        var result = await UseCase().ExecuteAsync(Request(job, EJobStatus.Hidden, "  Salário abaixo do mínimo legal  "));

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.Status.Should().Be("Hidden");
        result.Data.ModerationReason.Should().Be("Salário abaixo do mínimo legal");
        job.Status.Should().Be(EJobStatus.Hidden);
        job.Moderation!.Status.Should().Be(EModerationStatus.Rejected);
        job.Moderation.Reason.Should().Be("Salário abaixo do mínimo legal");
        job.Moderation.ModeratorId.Should().Be("admin-1");
        job.Moderation.ModerationDate.Should().Be(Now.UtcDateTime);

        var notification = _notifications.Created.Should().ContainSingle().Which;
        notification.RecipientId.Should().Be(_advertiser.Id.Value.ToString());
        notification.Title.Should().Contain("ocultada");
        notification.Message.Should().Contain("Desenvolvedor Pleno").And.Contain("Salário abaixo do mínimo legal");

        var audit = _audit.Records.Should().ContainSingle().Which;
        audit.Action.Should().Be(AdminAuditActions.JobStatusChanged);
        audit.Actor.Email.Should().Be("admin@monburan.com.br");
        audit.TargetType.Should().Be("job");
        audit.TargetLabel.Should().Be("Desenvolvedor Pleno");
        audit.Details.Should().Be("Ativa → Oculta. Motivo: Salário abaixo do mínimo legal");
    }

    [Fact]
    public async Task Reject_MarksTheModerationAsRejected()
    {
        var job = NewJob();

        var result = await UseCase().ExecuteAsync(Request(job, EJobStatus.Rejected, "Anúncio de pirâmide financeira"));

        result.Status.Should().Be(EResultStatus.Success);
        job.Status.Should().Be(EJobStatus.Rejected);
        job.Moderation!.Status.Should().Be(EModerationStatus.Rejected);
        _notifications.Created.Single().Title.Should().Contain("reprovada");
    }

    [Fact]
    public async Task Activate_ApprovesTheJob_ClearsTheReason_AndTellsTheCompany()
    {
        var job = NewJob(EJobStatus.Hidden, reason: "Texto enganoso");

        var result = await UseCase().ExecuteAsync(Request(job, EJobStatus.Active));

        result.Status.Should().Be(EResultStatus.Success);
        job.Status.Should().Be(EJobStatus.Active);
        job.Moderation!.Status.Should().Be(EModerationStatus.Approved);
        job.Moderation.Reason.Should().BeNull();
        result.Data!.ModerationReason.Should().BeNull();
        _notifications.Created.Single().Title.Should().Contain("reativada");
        _audit.Records.Single().Details.Should().Be("Oculta → Ativa");
    }

    [Fact]
    public async Task Pause_NeedsNoReason_ButKeepsOneWhenGiven()
    {
        var job = NewJob();

        (await UseCase().ExecuteAsync(Request(job, EJobStatus.Paused))).Status.Should().Be(EResultStatus.Success);
        job.Status.Should().Be(EJobStatus.Paused);
        job.Moderation!.Status.Should().Be(EModerationStatus.Approved, "pausar não é uma decisão de moderação sobre o conteúdo");

        var other = NewJob();
        await UseCase().ExecuteAsync(Request(other, EJobStatus.Paused, "Em investigação"));
        other.Moderation!.Reason.Should().Be("Em investigação");
        _notifications.Created.Last().Message.Should().Contain("Em investigação");
    }

    [Fact]
    public async Task Activate_AnExpiredJob_NeedsANewValidity()
    {
        var job = NewJob(EJobStatus.Expired, daysToExpire: -3);

        (await UseCase().ExecuteAsync(Request(job, EJobStatus.Active))).Message.Should().Contain("validade da vaga já passou");
        (await UseCase().ExecuteAsync(Request(job, EJobStatus.Active, renew: Now.UtcDateTime.AddDays(-1)))).Status.Should().Be(EResultStatus.DomainError);
        (await UseCase().ExecuteAsync(Request(job, EJobStatus.Active, renew: Now.UtcDateTime.AddDays(400)))).Status.Should().Be(EResultStatus.DomainError);
        job.Status.Should().Be(EJobStatus.Expired);

        var until = new DateTime(2026, 11, 20, 15, 30, 0, DateTimeKind.Unspecified);
        var result = await UseCase().ExecuteAsync(Request(job, EJobStatus.Active, renew: until));

        result.Status.Should().Be(EResultStatus.Success, result.Message);
        job.Status.Should().Be(EJobStatus.Active);
        job.ExpirationDate.Should().Be(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));
        job.ExpirationDate.Kind.Should().Be(DateTimeKind.Utc, "o Npgsql só grava DateTime em UTC em colunas timestamptz");
    }

    [Fact]
    public async Task Activate_AJobThatIsStillWithinItsValidity_NeedsNoRenewal()
    {
        var job = NewJob(EJobStatus.Paused, daysToExpire: 5);

        var result = await UseCase().ExecuteAsync(Request(job, EJobStatus.Active));

        result.Status.Should().Be(EResultStatus.Success);
        job.ExpirationDate.Should().Be(Now.UtcDateTime.AddDays(5));
    }

    [Fact]
    public async Task Activate_ACompanyWithAnInactiveAccount_IsRefused()
    {
        _advertiser.Status = EAdvertiserStatus.Blocked;
        var job = NewJob(EJobStatus.Hidden);

        var result = await UseCase().ExecuteAsync(Request(job, EJobStatus.Active));

        result.Status.Should().Be(EResultStatus.DomainError);
        result.Message.Should().Contain("conta da empresa está inativa");
        job.Status.Should().Be(EJobStatus.Hidden);
    }

    [Fact]
    public async Task ASituationTheJobAlreadyHas_AnUnknownJob_AndAnInvalidSituation_AreRefused()
    {
        var job = NewJob();

        (await UseCase().ExecuteAsync(Request(job, EJobStatus.Active))).Message.Should().Contain("já está ativa");
        (await UseCase().ExecuteAsync(new AdminChangeJobStatusRequest(Guid.NewGuid(), EJobStatus.Paused, null, null, Admin))).Status
            .Should().Be(EResultStatus.EntityNotFound);
        (await UseCase().ExecuteAsync(Request(job, EJobStatus.Expired))).Status.Should().Be(EResultStatus.HasValidation);
        (await UseCase().ExecuteAsync(Request(job, (EJobStatus)99))).Status.Should().Be(EResultStatus.HasValidation);
    }

    [Fact]
    public async Task ADeletedJob_IsNotFound()
    {
        var job = NewJob();
        job.IsDeleted = true;

        (await UseCase().ExecuteAsync(Request(job, EJobStatus.Paused))).Status.Should().Be(EResultStatus.EntityNotFound);
    }

    [Fact]
    public async Task AFailureToNotifyTheCompany_DoesNotUndoTheModeration()
    {
        _notifications.Fail = true;
        var job = NewJob();

        var result = await UseCase().ExecuteAsync(Request(job, EJobStatus.Hidden, "Conteúdo duplicado"));

        result.Status.Should().Be(EResultStatus.Success);
        job.Status.Should().Be(EJobStatus.Hidden);
        _audit.Records.Should().ContainSingle();
    }
}

public class AdminDeleteJobUseCaseTests
{
    private static readonly AdminActor Admin = new("admin-1", "admin@monburan.com.br");

    private readonly FakeJobRepository _jobs = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeAuditLog _audit = new();

    private AdminDeleteJobUseCase UseCase() => new(_jobs, new CreateNotificationUseCase(_notifications), _audit);

    private Job NewJob() => _jobs.Add(new Job
    {
        Id = Id.Create(Guid.NewGuid()), TenantId = "default", AdvertiserId = Id.Create(Guid.NewGuid()),
        Title = "Vaga a remover", Description = "Descrição.", Status = EJobStatus.Active, ExpirationDate = DateTime.UtcNow.AddDays(10)
    });

    [Fact]
    public async Task NeedsAReason()
    {
        var job = NewJob();

        (await UseCase().ExecuteAsync(new AdminDeleteJobRequest(job.Id.Value, " ", Admin))).Status.Should().Be(EResultStatus.HasValidation);
        (await UseCase().ExecuteAsync(new AdminDeleteJobRequest(job.Id.Value, new string('x', 501), Admin))).Status.Should().Be(EResultStatus.HasValidation);

        job.IsDeleted.Should().BeFalse();
        _audit.Records.Should().BeEmpty();
    }

    [Fact]
    public async Task Deletes_NotifiesTheCompany_AndIsAudited()
    {
        var job = NewJob();

        var result = await UseCase().ExecuteAsync(new AdminDeleteJobRequest(job.Id.Value, "Oferta fraudulenta", Admin));

        result.Status.Should().Be(EResultStatus.Success);
        job.IsDeleted.Should().BeTrue();
        _notifications.Created.Should().ContainSingle().Which.Message.Should().Contain("Vaga a remover").And.Contain("Oferta fraudulenta");
        var audit = _audit.Records.Should().ContainSingle().Which;
        audit.Action.Should().Be(AdminAuditActions.JobDeleted);
        audit.TargetLabel.Should().Be("Vaga a remover");
        audit.Details.Should().Be("Motivo: Oferta fraudulenta");
    }

    [Fact]
    public async Task AnUnknownJob_IsNotFound()
    {
        (await UseCase().ExecuteAsync(new AdminDeleteJobRequest(Guid.NewGuid(), "motivo válido", Admin))).Status.Should().Be(EResultStatus.EntityNotFound);
    }
}

public class AdminListJobsUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeJobRepository _jobs = new();
    private readonly Advertiser _alfa = new() { Id = Id.Create(Guid.NewGuid()), Name = "Alfa Tecnologia", Email = "alfa@exemplo.com" };
    private readonly Advertiser _beta = new() { Id = Id.Create(Guid.NewGuid()), Name = "Beta Marketing", Email = "beta@exemplo.com" };

    private AdminListJobsUseCase UseCase() => new(_jobs, new FixedTimeProvider(Now));

    private Job Add(Advertiser advertiser, string title, EJobStatus status, int daysToExpire = 20) => _jobs.Add(new Job
    {
        Id = Id.Create(Guid.NewGuid()), TenantId = "default", AdvertiserId = advertiser.Id, Advertiser = advertiser, Title = title,
        Description = "Descrição.", Status = status, ExpirationDate = Now.UtcDateTime.AddDays(daysToExpire),
        Location = new JobLocation { City = "Recife", State = "PE", IsRemote = true }, Counters = new JobCounters { Views = 7, Applications = 3 }
    });

    public AdminListJobsUseCaseTests()
    {
        Add(_alfa, "Dev .NET", EJobStatus.Active);
        Add(_alfa, "Dev Java", EJobStatus.Hidden);
        Add(_beta, "Analista de SEO", EJobStatus.Paused);
        Add(_beta, "Designer vencido", EJobStatus.Active, daysToExpire: -4);
    }

    [Fact]
    public async Task ListsEveryJobOfEveryCompany_WithTheCountsForAllSituations()
    {
        var result = await UseCase().ExecuteAsync(new AdminListJobsRequest());

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.TotalCount.Should().Be(4);
        result.Data.StatusCounts.Should().Equal(new Dictionary<string, int>
        {
            ["Active"] = 2, ["Expired"] = 0, ["Hidden"] = 1, ["Rejected"] = 0, ["Paused"] = 1
        });
    }

    [Fact]
    public async Task FiltersByStatusSearchAndCompany_IgnoringCase()
    {
        (await UseCase().ExecuteAsync(new AdminListJobsRequest { Status = "hidden" })).Data!.Items.Should().ContainSingle().Which.Title.Should().Be("Dev Java");
        (await UseCase().ExecuteAsync(new AdminListJobsRequest { SearchTerm = "SEO" })).Data!.Items.Should().ContainSingle().Which.Title.Should().Be("Analista de SEO");
        (await UseCase().ExecuteAsync(new AdminListJobsRequest { SearchTerm = "beta" })).Data!.TotalCount.Should().Be(2);
        (await UseCase().ExecuteAsync(new AdminListJobsRequest { AdvertiserId = _alfa.Id.Value })).Data!.TotalCount.Should().Be(2);
        (await UseCase().ExecuteAsync(new AdminListJobsRequest { Status = "naoexiste" })).Data!.TotalCount.Should().Be(4, "situação desconhecida não filtra");
    }

    [Fact]
    public async Task MapsTheCompanyTheLocationTheCountersAndThePastValidity()
    {
        var item = (await UseCase().ExecuteAsync(new AdminListJobsRequest { SearchTerm = "vencido" })).Data!.Items.Single();

        item.AdvertiserName.Should().Be("Beta Marketing");
        item.AdvertiserEmail.Should().Be("beta@exemplo.com");
        item.City.Should().Be("Recife");
        item.IsRemote.Should().BeTrue();
        item.Views.Should().Be(7);
        item.Applications.Should().Be(3);
        item.IsPastExpiration.Should().BeTrue();
        (await UseCase().ExecuteAsync(new AdminListJobsRequest { SearchTerm = "Dev .NET" })).Data!.Items.Single().IsPastExpiration.Should().BeFalse();
    }

    [Fact]
    public async Task ClampsStrangePagination()
    {
        var result = await UseCase().ExecuteAsync(new AdminListJobsRequest { Page = -3, Size = 0 });

        result.Data!.Page.Should().Be(1);
        result.Data.Size.Should().Be(20);
        (await UseCase().ExecuteAsync(new AdminListJobsRequest { Size = 1 })).Data!.TotalPages.Should().Be(4);
    }
}

public class JobModerationTests
{
    [Theory]
    [InlineData(EJobStatus.Hidden, "Texto enganoso", "Texto enganoso")]
    [InlineData(EJobStatus.Rejected, "Fora das regras", "Fora das regras")]
    [InlineData(EJobStatus.Active, "sobra de moderação antiga", null)]
    [InlineData(EJobStatus.Paused, "Em análise", null)]
    [InlineData(EJobStatus.Hidden, "  ", null)]
    public void TheReasonOnlyShowsWhileTheJobIsHiddenOrRejected(EJobStatus status, string reason, string? expected)
    {
        var job = new Job { Status = status, Moderation = new ModerationInfo { Reason = reason } };

        JobModeration.VisibleReason(job).Should().Be(expected);
    }

    [Fact]
    public void ThereIsNoReasonWithoutModerationInfo() =>
        JobModeration.VisibleReason(new Job { Status = EJobStatus.Hidden, Moderation = null }).Should().BeNull();
}
