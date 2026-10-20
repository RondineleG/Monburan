using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.UnitTests.UseCases.Admin;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Vagas em memória com as regras do repositório real que importam aqui (exclusão lógica, filtros da administração).</summary>
internal sealed class FakeJobRepository : IJobRepository
{
    private readonly List<Job> _jobs = [];

    public int UpdateCalls { get; private set; }

    public IReadOnlyList<Job> All => _jobs;

    public Job Add(Job job)
    {
        _jobs.Add(job);
        return job;
    }

    public Task<RequestResult<Job>> CreateAsync(Job entity, CancellationToken cancellationToken = default)
    {
        _jobs.Add(entity);
        return Task.FromResult(RequestResult<Job>.Success(entity));
    }

    public Task<RequestResult<Job>> DeleteAsync(Id id, CancellationToken cancellationToken = default)
    {
        var job = _jobs.FirstOrDefault(item => item.Id == id && !item.IsDeleted);
        if (job is null)
            return Task.FromResult(RequestResult<Job>.EntityNotFound("Job", id.Value, "Vaga não encontrada."));

        job.IsDeleted = true;
        return Task.FromResult(RequestResult<Job>.Success(job));
    }

    public Task<RequestResult<IEnumerable<Job>>> GetAllAsync(int page, int size, CancellationToken cancellationToken = default) =>
        Task.FromResult(RequestResult<IEnumerable<Job>>.Success(_jobs.Where(job => !job.IsDeleted).ToList()));

    public Task<RequestResult<Job>> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
    {
        var job = _jobs.FirstOrDefault(item => item.Id == id && !item.IsDeleted);
        return Task.FromResult(job is null
            ? RequestResult<Job>.EntityNotFound("Job", id.Value, "Vaga não encontrada.")
            : RequestResult<Job>.Success(job));
    }

    public Task<RequestResult<Job>> UpdateAsync(Job entity, CancellationToken cancellationToken = default)
    {
        UpdateCalls++;
        return Task.FromResult(RequestResult<Job>.Success(entity));
    }

    public Task<RequestResult<PagedResponse<Job>>> SearchAsync(
        string? searchTerm = null, string? city = null, string? state = null, bool? isRemote = null, decimal? salaryMin = null,
        decimal? salaryMax = null, string? category = null, Guid? advertiserId = null, int page = 1, int size = 10,
        string? type = null, string? sortBy = null, bool? sortDescending = null, EContractType? contractType = null,
        ESalaryPeriod? salaryPeriod = null, int? publishedWithinDays = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<PagedResponse<Job>> GetAllPagedAsync(int page, int size, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<int> CountActiveByAdvertiserAsync(Guid advertiserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<PagedResponse<Job>> AdminSearchAsync(string? searchTerm, EJobStatus? status, Guid? advertiserId, int page, int size, CancellationToken cancellationToken = default)
    {
        var query = _jobs.Where(job => !job.IsDeleted);
        if (status.HasValue)
            query = query.Where(job => job.Status == status.Value);
        if (advertiserId.HasValue)
            query = query.Where(job => job.AdvertiserId.Value == advertiserId.Value);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLowerInvariant();
            query = query.Where(job => job.Title.ToLowerInvariant().Contains(term)
                || (job.Advertiser?.Name.ToLowerInvariant().Contains(term) ?? false));
        }

        var all = query.OrderByDescending(job => job.CreatedAt).ToList();
        return Task.FromResult(new PagedResponse<Job>
        {
            Data = all.Skip((page - 1) * size).Take(size).ToList(), TotalCount = all.Count, Page = page, Size = size
        });
    }

    public Task<IReadOnlyDictionary<EJobStatus, int>> CountByStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<EJobStatus, int>>(
            _jobs.Where(job => !job.IsDeleted).GroupBy(job => job.Status).ToDictionary(group => group.Key, group => group.Count()));

    public Task<RequestResult<IEnumerable<Job>>> ListActiveForRecommendationsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(RequestResult<IEnumerable<Job>>.Success(
            _jobs.Where(job => !job.IsDeleted && job.Status == EJobStatus.Active).ToList()));

    public Task<RequestResult<bool>> IncrementViewsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = _jobs.FirstOrDefault(item => item.Id.Value == jobId);
        if (job is null)
            return Task.FromResult(RequestResult<bool>.Success(false));

        job.Counters.Views++;
        return Task.FromResult(RequestResult<bool>.Success(true));
    }

    public Task<RequestResult<bool>> IncrementApplicationsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = _jobs.FirstOrDefault(item => item.Id.Value == jobId);
        if (job is null)
            return Task.FromResult(RequestResult<bool>.Success(false));

        job.Counters.Applications++;
        return Task.FromResult(RequestResult<bool>.Success(true));
    }

    public Task<RequestResult<Job>> UpdateModerationAsync(Id jobId, ModerationInfo moderation, CancellationToken cancellationToken = default)
    {
        var job = _jobs.FirstOrDefault(item => item.Id == jobId && !item.IsDeleted);
        if (job is null)
            return Task.FromResult(RequestResult<Job>.EntityNotFound("Job", jobId.Value, "Vaga não encontrada."));

        job.Moderation ??= new ModerationInfo();
        job.Moderation.Status = moderation.Status;
        job.Moderation.Reason = moderation.Reason;
        job.Moderation.ModeratorId = moderation.ModeratorId;
        job.Moderation.ModerationDate = moderation.ModerationDate;
        return Task.FromResult(RequestResult<Job>.Success(job));
    }
}

internal sealed class FakeAdvertiserRepository : IBaseRepository<Advertiser>
{
    private readonly List<Advertiser> _advertisers = [];

    public Advertiser Add(Advertiser advertiser)
    {
        _advertisers.Add(advertiser);
        return advertiser;
    }

    public Task<RequestResult<Advertiser>> CreateAsync(Advertiser entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<RequestResult<Advertiser>> DeleteAsync(Id id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<RequestResult<IEnumerable<Advertiser>>> GetAllAsync(int page, int size, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<RequestResult<Advertiser>> GetByIdAsync(Id id, CancellationToken cancellationToken = default)
    {
        var found = _advertisers.FirstOrDefault(item => item.Id == id);
        return Task.FromResult(found is null
            ? RequestResult<Advertiser>.EntityNotFound("Advertiser", id.Value, "Empresa não encontrada.")
            : RequestResult<Advertiser>.Success(found));
    }

    public Task<RequestResult<Advertiser>> UpdateAsync(Advertiser entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class FakeNotificationRepository : IBaseRepository<Notification>
{
    public List<Notification> Created { get; } = [];

    public bool Fail { get; set; }

    public Task<RequestResult<Notification>> CreateAsync(Notification entity, CancellationToken cancellationToken = default)
    {
        if (Fail)
            return Task.FromResult(RequestResult<Notification>.WithError("banco fora do ar"));

        Created.Add(entity);
        return Task.FromResult(RequestResult<Notification>.Success(entity));
    }

    public Task<RequestResult<Notification>> DeleteAsync(Id id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<RequestResult<IEnumerable<Notification>>> GetAllAsync(int page, int size, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<RequestResult<Notification>> GetByIdAsync(Id id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<RequestResult<Notification>> UpdateAsync(Notification entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class FakeAuditLog : IAdminAuditLog
{
    public List<AdminAuditRecord> Records { get; } = [];

    public Task RecordAsync(AdminAuditRecord record, CancellationToken cancellationToken = default)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }

    public Task<PagedResponse<AdminAuditEntry>> ListAsync(AdminAuditFilter filter, int page, int size, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
