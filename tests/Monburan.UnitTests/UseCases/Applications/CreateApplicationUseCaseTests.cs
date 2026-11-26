using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Applications;
using Monburan.Core.UseCases.Notifications;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.UseCases.Applications;

public class CreateApplicationUseCaseTests
{
    private readonly IBaseRepository<Application> _applications = Substitute.For<IBaseRepository<Application>>();
    private readonly IBaseRepository<Resume> _resumes = Substitute.For<IBaseRepository<Resume>>();
    private readonly IJobRepository _jobs = Substitute.For<IJobRepository>();
    private readonly IApplicationStatusHistoryRepository _history = Substitute.For<IApplicationStatusHistoryRepository>();
    private readonly Guid _candidateId = Guid.NewGuid();
    private readonly Guid _jobId = Guid.NewGuid();
    private readonly CreateApplicationUseCase _useCase;

    public CreateApplicationUseCaseTests()
    {
        _jobs.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(ActiveJob()));
        _applications.CreateAsync(Arg.Any<Application>())
            .Returns(call => RequestResult<Application>.Success(call.Arg<Application>()));
        _history.CreateAsync(Arg.Any<ApplicationStatusHistory>())
            .Returns(call => RequestResult<ApplicationStatusHistory>.Success(call.Arg<ApplicationStatusHistory>()));

        var notifications = new NotifyOnApplicationCreatedUseCase(
            Substitute.For<IBaseRepository<Application>>(),
            Substitute.For<IBaseRepository<Job>>(),
            Substitute.For<IBaseRepository<Advertiser>>(),
            new SendEmailNotificationUseCase(Substitute.For<IEmailSender>(), Substitute.For<ILogger<SendEmailNotificationUseCase>>()),
            new CreateNotificationUseCase(Substitute.For<IBaseRepository<Notification>>()),
            Substitute.For<ILogger<NotifyOnApplicationCreatedUseCase>>());

        _useCase = new CreateApplicationUseCase(
            _applications,
            _jobs,
            _resumes,
            _history,
            notifications,
            Substitute.For<ILogger<CreateApplicationUseCase>>());
    }

    private Job ActiveJob() => new()
    {
        Id = Id.Create(_jobId),
        AdvertiserId = Id.Create(Guid.NewGuid()),
        Title = "Vaga",
        Description = "Descrição",
        Status = EJobStatus.Active,
        ExpirationDate = DateTime.UtcNow.AddDays(10),
        TenantId = "default"
    };

    private Job ExternalJob() => new()
    {
        Id = Id.Create(_jobId),
        AdvertiserId = null,
        PublicationType = EPublicationType.External,
        ApplicationChannel = EApplicationChannel.ExternalUrl,
        ApplicationUrl = "https://empresa.exemplo.com.br/vagas/1",
        CompanyName = "Empresa Exemplo",
        SourceName = "Site da agência",
        Title = "Vaga externa",
        Description = "Descrição",
        Status = EJobStatus.Active,
        ExpirationDate = DateTime.UtcNow.AddDays(10),
        TenantId = "default"
    };

    private CreateApplicationRequest Request() => new()
    {
        CandidateId = _candidateId,
        JobId = _jobId,
        Notes = "Tenho interesse."
    };

    [Fact]
    public async Task ExecuteAsync_WhenTheJobIsExternal_RefusesTheApplicationInsideMonburan()
    {
        _jobs.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(ExternalJob()));

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.DomainError);
        await _applications.DidNotReceive().CreateAsync(Arg.Any<Application>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheApplicationIsCreated_WritesSentToTheHistory()
    {
        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.Success);
        await _history.Received(1).CreateAsync(Arg.Is<ApplicationStatusHistory>(history =>
            history.ApplicationId.Value == result.Data!.Id
            && history.Status == EApplicationStatus.Sent
            && history.TenantId == "default"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheHistoryWriteFails_StillCreatesTheApplication()
    {
        _history.CreateAsync(Arg.Any<ApplicationStatusHistory>())
            .Returns(RequestResult<ApplicationStatusHistory>.WithError("banco fora do ar"));

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.Success);
        await _applications.Received(1).CreateAsync(Arg.Any<Application>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheHistoryWriteThrows_StillCreatesTheApplication()
    {
        _history.CreateAsync(Arg.Any<ApplicationStatusHistory>())
            .Returns(Task.FromException<RequestResult<ApplicationStatusHistory>>(new InvalidOperationException("tabela ausente")));

        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.Success);
    }

    [Fact]
    public async Task ExecuteAsync_CountsTheApplicationAtomically_WithoutRewritingTheJob()
    {
        // Contar no código (ler a vaga, somar, gravar a vaga inteira) perdia a contagem
        // quando duas candidaturas entravam ao mesmo tempo e ainda sobrescrevia a moderação
        // que a administração tinha gravado.
        var result = await _useCase.ExecuteAsync(Request());

        result.Status.Should().Be(EResultStatus.Success);
        await _jobs.Received(1).IncrementApplicationsAsync(_jobId);
        await _jobs.DidNotReceive().UpdateAsync(Arg.Any<Job>());
    }
    [Fact]
    public async Task ExecuteAsync_WhenResumeIsAttached_PersistsImmutableSnapshot()
    {
        var resumeId = Guid.NewGuid();
        var resume = new Resume
        {
            Id = Id.Create(resumeId),
            CandidateId = Id.Create(_candidateId),
            Title = "Currículo original",
            Summary = "Resumo antes da candidatura",
            IsActive = true,
            Skills =
            [
                new ResumeSkill { Skill = "Atendimento", Level = "Avançado", YearsOfExperience = 4 }
            ],
            ExperienceHistory =
            [
                new Experience { Company = "Empresa A", Position = "Auxiliar", StartDate = new DateTime(2022, 1, 1), IsCurrent = true }
            ]
        };
        _resumes.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Resume>.Success(resume));
        var request = Request();
        request.ResumeId = resumeId;
        request.CaptureResumeSnapshot = true;

        var result = await _useCase.ExecuteAsync(request);

        resume.Title = "Currículo editado depois";
        resume.Summary = "Resumo editado depois";

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.ResumeSnapshot.Should().NotBeNull();
        result.Data.ResumeSnapshot!.ResumeId.Should().Be(resumeId);
        result.Data.ResumeSnapshot.Title.Should().Be("Currículo original");
        result.Data.ResumeSnapshot.Summary.Should().Be("Resumo antes da candidatura");
        result.Data.ResumeSnapshot.Skills.Should().ContainSingle(s => s.Skill == "Atendimento");
        result.Data.ResumeSnapshot.ExperienceHistory.Should().ContainSingle(e => e.Company == "Empresa A");
        await _applications.Received(1).CreateAsync(Arg.Is<Application>(a =>
            a.ResumeSnapshotJson != null && a.ResumeSnapshotJson.Contains("Resumo antes da candidatura")));
    }

    [Fact]
    public async Task ExecuteAsync_WhenResumeBelongsToAnotherCandidate_RefusesTheApplication()
    {
        var resumeId = Guid.NewGuid();
        _resumes.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Resume>.Success(new Resume
        {
            Id = Id.Create(resumeId),
            CandidateId = Id.Create(Guid.NewGuid()),
            Title = "Currículo de outra pessoa"
        }));
        var request = Request();
        request.ResumeId = resumeId;

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.HasValidation);
        await _applications.DidNotReceive().CreateAsync(Arg.Any<Application>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSnapshotFeatureIsOff_DoesNotPersistSnapshot()
    {
        var resumeId = Guid.NewGuid();
        _resumes.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Resume>.Success(new Resume
        {
            Id = Id.Create(resumeId),
            CandidateId = Id.Create(_candidateId),
            Title = "Currículo original",
            Summary = "Resumo"
        }));
        var request = Request();
        request.ResumeId = resumeId;

        var result = await _useCase.ExecuteAsync(request);

        result.Status.Should().Be(EResultStatus.Success);
        result.Data!.ResumeSnapshot.Should().BeNull();
        await _applications.Received(1).CreateAsync(Arg.Is<Application>(a => a.ResumeSnapshotJson == null));
    }

}
