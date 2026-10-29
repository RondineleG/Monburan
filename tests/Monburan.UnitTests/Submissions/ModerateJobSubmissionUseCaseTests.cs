using FluentAssertions;
using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.UseCases.Submissions;
using Monburan.Core.Validations;
using Monburan.Core.ValueObjects;
using NSubstitute;
using Xunit;

namespace Monburan.UnitTests.Submissions;

public class ModerateJobSubmissionUseCaseTests
{
    private static readonly AdminActor Moderator = new("mod-1", "moderador@monburan.com.br");

    private readonly IJobSubmissionRepository _submissions = Substitute.For<IJobSubmissionRepository>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly IAdminAuditLog _audit = Substitute.For<IAdminAuditLog>();
    private readonly JobSubmission _submission;
    private readonly ModerateJobSubmissionUseCase _useCase;

    public ModerateJobSubmissionUseCaseTests()
    {
        _submission = Pending();
        _submissions.GetByIdAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>())
            .Returns(_ => RequestResult<JobSubmission>.Success(_submission));
        _submissions.UpdateAsync(Arg.Any<JobSubmission>(), Arg.Any<CancellationToken>())
            .Returns(call => RequestResult<JobSubmission>.Success(call.Arg<JobSubmission>()));
        _jobs.CreateAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>())
            .Returns(call => RequestResult<Job>.Success(call.Arg<Job>()));

        _useCase = new ModerateJobSubmissionUseCase(_submissions, _jobs, new JobValidator(), _audit);
    }

    [Fact]
    public async Task Approval_PublishesAnExternalJobWithTheSubmissionCompanyAndTheChosenChannel()
    {
        var result = await _useCase.ExecuteAsync(ApproveWhatsApp());

        result.Status.Should().Be(EResultStatus.Success);
        await _jobs.Received(1).CreateAsync(Arg.Is<Job>(job =>
            job.PublicationType == EPublicationType.External
            && job.AdvertiserId == null
            && job.CompanyName == "Transportes Exemplo"
            && job.SourceUrl == "https://instagram.com/exemplo"
            && job.ApplicationChannel == EApplicationChannel.WhatsApp
            && job.ApplicationContact == "(47) 99999-8888"
            && job.Salary == null), Arg.Any<CancellationToken>());
        _submission.Status.Should().Be(ESubmissionStatus.Approved);
        _submission.ApprovedJobId.Should().NotBeNull();
    }

    [Fact]
    public async Task Approval_RecordsWhoDecidedWhenAndTheHistory()
    {
        await _useCase.ExecuteAsync(ApproveWhatsApp());

        _submission.ModeratedBy.Should().Be("mod-1");
        _submission.ModeratedAt.Should().NotBeNull();
        _submission.History.Should().Contain(h => h.Status == ESubmissionStatus.Approved && h.ActorId == "mod-1");
        await _audit.Received(1).RecordAsync(Arg.Is<AdminAuditRecord>(r =>
            r.Action == AdminAuditActions.SubmissionDecided && r.Actor.Id == "mod-1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approval_WithoutAChannel_DoesNotPublish()
    {
        var request = ApproveWhatsApp();
        request.Channel = null;

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        await _jobs.DidNotReceive().CreateAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
        _submission.Status.Should().Be(ESubmissionStatus.Pending);
    }

    [Fact]
    public async Task Approval_WithoutDeadline_DoesNotPublish()
    {
        var request = ApproveWhatsApp();
        request.ExpirationDate = null;

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        await _jobs.DidNotReceive().CreateAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenTheDecisionCannotBeSaved_TheCreatedJobIsRemoved()
    {
        _submissions.UpdateAsync(Arg.Any<JobSubmission>(), Arg.Any<CancellationToken>())
            .Returns(RequestResult<JobSubmission>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(ApproveWhatsApp());

        result.Status.Should().NotBe(EResultStatus.Success);
        await _jobs.Received(1).DeleteAsync(Arg.Any<Id>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approval_WithAnInvalidChannelDetail_DoesNotPublish()
    {
        var request = ApproveWhatsApp();
        request.ApplicationContact = "99999";

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        await _jobs.DidNotReceive().CreateAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approval_WithoutSalary_LeavesItEmptyInsteadOfEstimating()
    {
        await _useCase.ExecuteAsync(ApproveWhatsApp());

        await _jobs.Received(1).CreateAsync(Arg.Is<Job>(job => job.Salary == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejection_NeedsAReason()
    {
        var result = await _useCase.ExecuteAsync(Decision(ESubmissionDecision.Reject, note: null));

        result.Status.Should().Be(EResultStatus.HasValidation);
        _submission.Status.Should().Be(ESubmissionStatus.Pending);
    }

    [Fact]
    public async Task Rejection_ClosesTheSubmissionWithoutPublishing()
    {
        var result = await _useCase.ExecuteAsync(Decision(ESubmissionDecision.Reject, "Anúncio de terceiros sem fonte."));

        result.Status.Should().Be(EResultStatus.Success);
        _submission.Status.Should().Be(ESubmissionStatus.Rejected);
        await _jobs.DidNotReceive().CreateAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Duplicate_MustPointToExactlyOneExistingItem()
    {
        var none = await _useCase.ExecuteAsync(Decision(ESubmissionDecision.Duplicate, "repete"));
        var both = await _useCase.ExecuteAsync(new ModerateJobSubmissionRequest
        {
            SubmissionId = _submission.Id.Value,
            Actor = Moderator,
            Decision = ESubmissionDecision.Duplicate,
            DuplicateOfJobId = Guid.NewGuid(),
            DuplicateOfSubmissionId = Guid.NewGuid()
        });

        none.Status.Should().Be(EResultStatus.HasValidation);
        both.Status.Should().Be(EResultStatus.HasValidation);
        _submission.Status.Should().Be(ESubmissionStatus.Pending);
    }

    [Fact]
    public async Task Duplicate_LinksTheRepeatedJobAndClosesTheSubmission()
    {
        var jobId = Guid.NewGuid();
        var request = Decision(ESubmissionDecision.Duplicate, "Mesma vaga já publicada.");
        request.DuplicateOfJobId = jobId;

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.Success);
        _submission.Status.Should().Be(ESubmissionStatus.Duplicate);
        _submission.DuplicateOfJobId!.Value.Should().Be(jobId);
        await _jobs.DidNotReceive().CreateAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoreInformation_KeepsTheSubmissionOpenForALaterDecision()
    {
        var asked = await _useCase.ExecuteAsync(Decision(ESubmissionDecision.NeedsMoreInformation, "Qual o endereço do posto?"));
        asked.Status.Should().Be(EResultStatus.Success);
        _submission.Status.Should().Be(ESubmissionStatus.NeedsMoreInformation);

        var approved = await _useCase.ExecuteAsync(ApproveWhatsApp());

        approved.Status.Should().Be(EResultStatus.Success);
        _submission.Status.Should().Be(ESubmissionStatus.Approved);
    }

    [Fact]
    public async Task ADecidedSubmission_CannotBeDecidedAgain()
    {
        await _useCase.ExecuteAsync(Decision(ESubmissionDecision.Reject, "Não atende."));

        var again = await _useCase.ExecuteAsync(ApproveWhatsApp());

        again.Status.Should().Be(EResultStatus.DomainError);
        await _jobs.DidNotReceive().CreateAsync(Arg.Any<Job>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithoutAnIdentifiedModerator_NothingChanges()
    {
        var request = ApproveWhatsApp();
        request.Actor = new AdminActor(string.Empty, "x@y.com");

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.DomainError);
        _submission.Status.Should().Be(ESubmissionStatus.Pending);
    }

    private static JobSubmission Pending() => new()
    {
        Id = Id.Create(Guid.NewGuid()),
        Title = "Motorista de carreta",
        CompanyName = "Transportes Exemplo",
        City = "Joinville",
        State = "SC",
        Description = "Transporte de cargas em rodovias estaduais.",
        SourceUrl = "https://instagram.com/exemplo",
        Status = ESubmissionStatus.Pending,
        TenantId = "default"
    };

    private ModerateJobSubmissionRequest ApproveWhatsApp() => new()
    {
        SubmissionId = _submission.Id.Value,
        Actor = Moderator,
        Decision = ESubmissionDecision.Approve,
        Channel = EApplicationChannel.WhatsApp,
        ApplicationContact = "(47) 99999-8888",
        ExpirationDate = DateTime.UtcNow.AddDays(30)
    };

    private ModerateJobSubmissionRequest Decision(ESubmissionDecision decision, string? note) => new()
    {
        SubmissionId = _submission.Id.Value,
        Actor = Moderator,
        Decision = decision,
        Note = note
    };
}
