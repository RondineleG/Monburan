using FluentAssertions;
using NSubstitute;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Validations;
using Monburan.Core.Mappers;
using Monburan.Core.Requests;
using Monburan.Core.UseCases.Jobs;
using Monburan.Core.UseCases.Subscriptions;
using Monburan.Core.Validations;
using Monburan.Core.ValueObjects;

namespace Monburan.UnitTests.Mappers;

/// <summary>
/// O mapeamento da vaga é único: a mesma vaga devolvida por rotas diferentes (detalhe, edição, listagem) precisa
/// sair igual. Sem isso, o site e o app desenham cartões diferentes conforme a operação que originou a vaga.
/// </summary>
public class JobResponseMapperTests
{
    private readonly IBaseRepository<Job> _jobRepository;
    private readonly IJobRepository _jobQueries;
    private readonly ISavedJobRepository _savedJobs;
    private readonly IEntityValidator<Job> _validator;

    public JobResponseMapperTests()
    {
        _jobRepository = Substitute.For<IBaseRepository<Job>>();
        _jobQueries = Substitute.For<IJobRepository>();
        _savedJobs = Substitute.For<ISavedJobRepository>();
        _validator = Substitute.For<IEntityValidator<Job>>();
        _validator.ValidateUpdate(Arg.Any<Job>()).Returns(ValidationResult.Success());
    }

    [Fact]
    public async Task TheSameJob_ReturnsTheSameCompanyAndModerationReason_OnDetailAndOnUpdate()
    {
        var job = JobWithCompany(EJobStatus.Hidden);
        _jobRepository.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(job));
        _jobRepository.UpdateAsync(Arg.Any<Job>()).Returns(call => RequestResult<Job>.Success(call.Arg<Job>()));

        var detail = await new GetJobByIdUseCase(_jobRepository, _savedJobs).ExecuteAsync(job.Id.Value);
        var update = await new UpdateJobUseCase(_jobRepository, _validator).ExecuteAsync(UpdateRequestFor(job));

        detail.Status.Should().Be(EResultStatus.Success);
        update.Status.Should().Be(EResultStatus.Success);

        // A empresa e o motivo da moderação não podem depender da operação que originou a vaga
        detail.Data!.AdvertiserName.Should().Be("Tech Solutions");
        detail.Data.AdvertiserLogoUrl.Should().Be("https://cdn.monburan.com.br/tech.png");
        detail.Data.ModerationReason.Should().Be("Remuneração não informada de forma clara");

        // A resposta inteira é a mesma, campo a campo
        update.Data.Should().BeEquivalentTo(detail.Data);
    }

    [Fact]
    public async Task TheUpdate_KeepsTheCompany_WhenTheRepositoryReturnsAJobWithoutTheNavigation()
    {
        var job = JobWithCompany(EJobStatus.Active);
        _jobRepository.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(job));

        // O repositório pode devolver outra instância da vaga, gravada sem a empresa carregada
        _jobRepository.UpdateAsync(Arg.Any<Job>()).Returns(call =>
        {
            var saved = call.Arg<Job>();
            return RequestResult<Job>.Success(new Job
            {
                Id = saved.Id,
                AdvertiserId = saved.AdvertiserId,
                Title = saved.Title,
                Description = saved.Description,
                ExpirationDate = saved.ExpirationDate,
                Status = saved.Status,
                TenantId = saved.TenantId,
                Counters = saved.Counters
            });
        });

        var update = await new UpdateJobUseCase(_jobRepository, _validator).ExecuteAsync(UpdateRequestFor(job));

        update.Status.Should().Be(EResultStatus.Success);
        update.Data!.AdvertiserName.Should().Be("Tech Solutions");
        update.Data.AdvertiserLogoUrl.Should().Be("https://cdn.monburan.com.br/tech.png");
    }

    [Fact]
    public async Task TheSameJob_ReturnsTheSameCompanyAndModerationReason_OnListingAndOnDetail()
    {
        var job = JobWithCompany(EJobStatus.Rejected);
        _jobRepository.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(job));
        _jobQueries.GetAllPagedAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(new PagedResponse<Job>
        {
            Data = [job],
            TotalCount = 1,
            Page = 1,
            Size = 20
        });

        var detail = await new GetJobByIdUseCase(_jobRepository, _savedJobs).ExecuteAsync(job.Id.Value);
        var listing = await new ListJobsUseCase(_jobQueries, _savedJobs).ExecuteAsync(new ListJobsRequest());

        var listed = listing.Data!.Jobs.Single();
        listed.AdvertiserName.Should().Be(detail.Data!.AdvertiserName);
        listed.AdvertiserLogoUrl.Should().Be(detail.Data.AdvertiserLogoUrl);
        listed.ModerationReason.Should().Be(detail.Data.ModerationReason);
        listed.ModerationReason.Should().Be("Remuneração não informada de forma clara");
    }

    [Fact]
    public async Task TheSameJob_ReturnsTheSameCompany_OnStatusChangeAndOnDetail()
    {
        var job = JobWithCompany(EJobStatus.Active);
        _jobRepository.GetByIdAsync(Arg.Any<Id>()).Returns(RequestResult<Job>.Success(job));
        _jobRepository.UpdateAsync(Arg.Any<Job>()).Returns(call => RequestResult<Job>.Success(call.Arg<Job>()));

        var detail = await new GetJobByIdUseCase(_jobRepository, _savedJobs).ExecuteAsync(job.Id.Value);
        var status = await new ChangeJobStatusUseCase(_jobRepository, JobLimitUseCase())
            .ExecuteAsync(new ChangeJobStatusRequest(job.Id.Value, job.AdvertiserId.Value, EJobStatus.Paused));

        status.Status.Should().Be(EResultStatus.Success);
        status.Data!.AdvertiserName.Should().Be(detail.Data!.AdvertiserName);
        status.Data.AdvertiserLogoUrl.Should().Be(detail.Data.AdvertiserLogoUrl);

        // Vaga pausada não está oculta nem rejeitada: o motivo da moderação segue a regra de visibilidade
        status.Data.ModerationReason.Should().BeNull();
        detail.Data.ModerationReason.Should().BeNull();
    }

    [Fact]
    public void AJobWithoutTheCompanyLoaded_DoesNotBreakTheMapping()
    {
        var job = JobWithCompany(EJobStatus.Active);
        job.Advertiser = null;

        var response = JobResponseMapper.ToResponse(job);

        response.AdvertiserName.Should().BeNull();
        response.AdvertiserLogoUrl.Should().BeNull();
        response.AdvertiserId.Should().Be(job.AdvertiserId.Value);
    }

    [Fact]
    public void TheModerationReason_OnlyShowsOnAJobHiddenOrRejected()
    {
        var hidden = JobWithCompany(EJobStatus.Hidden);
        var rejected = JobWithCompany(EJobStatus.Rejected);
        var active = JobWithCompany(EJobStatus.Active);

        JobResponseMapper.ToResponse(hidden).ModerationReason.Should().Be("Remuneração não informada de forma clara");
        JobResponseMapper.ToResponse(rejected).ModerationReason.Should().Be("Remuneração não informada de forma clara");
        JobResponseMapper.ToResponse(active).ModerationReason.Should().BeNull();
    }

    [Fact]
    public void AJobWithoutAModerationReason_KeepsTheReasonEmpty()
    {
        var job = JobWithCompany(EJobStatus.Hidden);
        job.Moderation = null;

        JobResponseMapper.ToResponse(job).ModerationReason.Should().BeNull();
    }

    [Fact]
    public void MappingANullJob_Throws()
    {
        var act = () => JobResponseMapper.ToResponse(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // Plano Free sem assinatura ativa: o limite de vagas nunca é o que decide o caso
    private CheckJobLimitUseCase JobLimitUseCase()
    {
        var subscriptions = Substitute.For<ISubscriptionRepository>();
        subscriptions.GetActiveByAdvertiserIdAsync(Arg.Any<Id>()).Returns(RequestResult<Subscription?>.Success(null));

        var plans = Substitute.For<IBaseRepository<SubscriptionPlan>>();
        plans.GetAllAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(RequestResult<IEnumerable<SubscriptionPlan>>.Success(
            [new SubscriptionPlan { Name = CheckJobLimitUseCase.FreePlanName, MaxJobs = 5, IsActive = true }]));

        return new CheckJobLimitUseCase(subscriptions, plans, _jobQueries);
    }

    private static Job JobWithCompany(EJobStatus status)
    {
        var advertiserId = Guid.NewGuid();
        return new Job
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = Id.Create(advertiserId),
            Title = "Senior Developer",
            Description = "Vaga de teste",
            ExpirationDate = DateTime.UtcNow.AddDays(30),
            Status = status,
            TenantId = "default",
            Advertiser = new Advertiser
            {
                Id = Id.Create(advertiserId),
                Name = "Tech Solutions",
                LogoUrl = "https://cdn.monburan.com.br/tech.png"
            },
            Moderation = new ModerationInfo
            {
                Status = EModerationStatus.Rejected,
                Reason = "Remuneração não informada de forma clara"
            },
            Counters = new JobCounters { Views = 3, Applications = 1 }
        };
    }

    private static UpdateJobRequest UpdateRequestFor(Job job) => new()
    {
        Id = job.Id.Value,
        Title = job.Title,
        Description = job.Description,
        NumberOfOpenings = job.NumberOfOpenings,
        ExpirationDate = job.ExpirationDate,
        Type = "OnSite",
        Country = "BR",
        SalaryCurrency = "BRL",
        SalaryDisplay = true
    };
}
