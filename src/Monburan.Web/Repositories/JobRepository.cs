using Microsoft.EntityFrameworkCore;

using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Repositories.Base;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class JobRepository : IJobRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public JobRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<Job>> CreateAsync(Job entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Jobs.Add(entity);
        await context.SaveChangesAsync();
        return RequestResult<Job>.Success(entity);
    }

    public async Task<RequestResult<Job>> DeleteAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var job = await context.Jobs.FindAsync(id);
        if (job == null)
            return RequestResult<Job>.EntityNotFound("Job", id.Value, "Vaga não encontrada.");

        job.IsDeleted = true;
        job.DeletedAt = DateTime.UtcNow;
        
        context.Jobs.Update(job);
        await context.SaveChangesAsync();
        return RequestResult<Job>.Success(job);
    }

    public async Task<RequestResult<IEnumerable<Job>>> GetAllAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var jobs = await context.Jobs
            .Include(j => j.Advertiser)
            .Where(j => !j.IsDeleted && j.Status == EJobStatus.Active)
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<IEnumerable<Job>>.Success(jobs);
    }

    public async Task<PagedResponse<Job>> AdminSearchAsync(string? searchTerm, EJobStatus? status, Guid? advertiserId, int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Jobs
            .AsNoTracking()
            .Include(j => j.Advertiser)
            .Where(j => !j.IsDeleted);

        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);

        if (advertiserId.HasValue)
        {
            var advertiserKey = Id.Create(advertiserId.Value);
            query = query.Where(j => j.AdvertiserId == advertiserKey);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(j =>
                j.Title.ToLower().Contains(term)
                || (j.Category != null && j.Category.ToLower().Contains(term))
                || (j.Advertiser != null && j.Advertiser.Name.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync();
        var jobs = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return new PagedResponse<Job> { Data = jobs, TotalCount = totalCount, Page = page, Size = size };
    }

    public async Task<IReadOnlyDictionary<EJobStatus, int>> CountByStatusAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var groups = await context.Jobs
            .AsNoTracking()
            .Where(j => !j.IsDeleted)
            .GroupBy(j => j.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync();

        return groups.ToDictionary(group => group.Status, group => group.Count);
    }

    public async Task<PagedResponse<Job>> GetAllPagedAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Jobs
            .Include(j => j.Advertiser)
            .Where(j => !j.IsDeleted && j.Status == EJobStatus.Active);

        var totalCount = await query.CountAsync();
        var jobs = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return new PagedResponse<Job>
        {
            Data = jobs,
            TotalCount = totalCount,
            Page = page,
            Size = size
        };
    }

    public async Task<RequestResult<Job>> GetByIdAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var job = await context.Jobs
            .Include(j => j.Advertiser)
            .FirstOrDefaultAsync(j => j.Id == id && !j.IsDeleted);

        if (job == null)
            return RequestResult<Job>.EntityNotFound("Job", id.Value, "Vaga não encontrada.");

        return RequestResult<Job>.Success(job);
    }

    public async Task<RequestResult<Job>> UpdateAsync(Job entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // A vaga chega desanexada (lida em outra unidade de trabalho). Gravar com Update()
        // marcaria TODAS as colunas como modificadas, incluindo as que esta edição não
        // tocou — uma edição feita com dados antigos apagaria a moderação da administração
        // e os contadores da vaga. Por isso a gravação passa por uma cópia explícita: só
        // entram as colunas que a edição da vaga de fato controla.
        var tracked = await context.Jobs
            .Include(job => job.Advertiser)
            .FirstOrDefaultAsync(job => job.Id == entity.Id);
        if (tracked is null)
            return RequestResult<Job>.EntityNotFound("Job", entity.Id.Value, "Vaga não encontrada.");

        tracked.Title = entity.Title;
        tracked.Description = entity.Description;
        tracked.Requirements = entity.Requirements;
        tracked.Responsibilities = entity.Responsibilities;
        tracked.Benefits = entity.Benefits;
        tracked.ApplicationInstructions = entity.ApplicationInstructions;
        tracked.NumberOfOpenings = entity.NumberOfOpenings;
        tracked.Observations = entity.Observations;
        tracked.Category = entity.Category;
        tracked.Tags = entity.Tags;
        tracked.ContractType = entity.ContractType;
        tracked.ExpirationDate = entity.ExpirationDate;
        tracked.Status = entity.Status;
        tracked.AdvertiserId = entity.AdvertiserId;

        CopyLocation(tracked.Location, entity.Location);
        CopySalary(tracked.Salary, entity.Salary);

        await context.SaveChangesAsync();
        return RequestResult<Job>.Success(tracked);
    }

    // Local e salário são "owned" (tabelas próprias): só são gravados quando o valor
    // recebido é diferente do que já está no banco.
    private static void CopyLocation(JobLocation? target, JobLocation? source)
    {
        if (target is null || source is null)
            return;

        target.City = source.City;
        target.State = source.State;
        target.Country = source.Country;
        target.Type = source.Type;
        target.IsRemote = source.IsRemote;
        target.ShowAddress = source.ShowAddress;
    }

    private static void CopySalary(SalaryRange? target, SalaryRange? source)
    {
        if (target is null || source is null)
            return;

        target.Min = source.Min;
        target.Max = source.Max;
        target.Period = source.Period;
        target.Currency = source.Currency;
        target.Display = source.Display;
    }

    /// <summary>
    /// Grava a moderação da vaga (aprovada, rejeitada ou escondida). Fica fora da
    /// atualização geral da vaga de propósito: são campos da administração, e uma edição
    /// da empresa não pode sobrescrever o que a administração gravou.
    /// </summary>
    public async Task<RequestResult<Job>> UpdateModerationAsync(Id jobId, ModerationInfo moderation)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var tracked = await context.Jobs.FirstOrDefaultAsync(job => job.Id == jobId);
        if (tracked is null)
            return RequestResult<Job>.EntityNotFound("Job", jobId.Value, "Vaga não encontrada.");

        tracked.Moderation ??= new ModerationInfo();
        tracked.Moderation.Status = moderation.Status;
        tracked.Moderation.Reason = moderation.Reason;
        tracked.Moderation.ModeratorId = moderation.ModeratorId;
        tracked.Moderation.ModerationDate = moderation.ModerationDate;

        await context.SaveChangesAsync();
        return RequestResult<Job>.Success(tracked);
    }

    public async Task<RequestResult<PagedResponse<Job>>> SearchAsync(
        string? searchTerm = null,
        string? city = null,
        string? state = null,
        bool? isRemote = null,
        decimal? salaryMin = null,
        decimal? salaryMax = null,
        string? category = null,
        Guid? advertiserId = null,
        int page = 1,
        int size = 10,
        string? type = null,
        string? sortBy = null,
        bool? sortDescending = null,
        EContractType? contractType = null,
        ESalaryPeriod? salaryPeriod = null,
        int? publishedWithinDays = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Jobs
            .AsNoTracking()
            .Include(j => j.Advertiser)
            .Where(j => !j.IsDeleted);

        // Com advertiserId é a visão do dono (Minhas vagas, /jobs/mine): todas as situações,
        // senão vagas expiradas/pausadas somem da gestão. Sem ele, só vagas ativas (público).
        if (advertiserId.HasValue)
        {
            var advertiserKey = Id.Create(advertiserId.Value);
            query = query.Where(j => j.AdvertiserId == advertiserKey);
        }
        else
        {
            query = query.Where(j => j.Status == EJobStatus.Active);
        }

        // Filtro por busca textual (aplicado no WHERE do SQL).
        // Usa ToLower().Contains() em vez de EF.Functions.ILike para manter a consulta
        // executável no provider InMemory dos testes de API — funciona igual no Npgsql
        // (embora sem pg_trgm/citext). Para buscas maiores, considerar Testcontainers.
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(j =>
                j.Title.ToLower().Contains(term) ||
                j.Description.ToLower().Contains(term) ||
                (j.Category != null && j.Category.ToLower().Contains(term)));
        }

        // Filtro por cidade
        if (!string.IsNullOrWhiteSpace(city))
        {
            var cityLower = city.ToLower();
            query = query.Where(j => j.Location != null && j.Location.City != null && j.Location.City.ToLower().Contains(cityLower));
        }

        // Filtro por estado
        if (!string.IsNullOrWhiteSpace(state))
        {
            var stateUpper = state.ToUpper();
            query = query.Where(j => j.Location != null && j.Location.State == stateUpper);
        }

        // Filtro por remoto
        if (isRemote.HasValue)
        {
            query = query.Where(j => j.Location != null && j.Location.IsRemote == isRemote.Value);
        }

        // Filtro por tipo de trabalho (OnSite, Remote, Hybrid)
        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(j => j.Location != null && j.Location.Type == type);
        }

        // Faixa salarial só conta quando a empresa a exibe: filtrar ou ordenar por um valor oculto revelaria a
        // posição dele (o site e a API já escondem o valor). Sem teto ("a partir de"), o valor de referência é o piso.

        // Filtro por salário mínimo: a vaga paga pelo menos esse valor
        if (salaryMin.HasValue)
        {
            query = query.Where(j => j.Salary != null && j.Salary.Display
                && (j.Salary.Max ?? j.Salary.Min) >= salaryMin.Value);
        }

        // Filtro por salário máximo: o piso da vaga cabe no orçamento informado
        if (salaryMax.HasValue)
        {
            query = query.Where(j => j.Salary != null && j.Salary.Display
                && j.Salary.Min.HasValue && j.Salary.Min <= salaryMax.Value);
        }

        // Filtro por categoria
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(j => j.Category == category);
        }

        // Filtro por tipo de contratação (CLT, PJ, estágio...)
        if (contractType.HasValue)
        {
            query = query.Where(j => j.ContractType == contractType.Value);
        }

        // Filtro por período do salário (hora, dia, mês, ano)
        if (salaryPeriod.HasValue)
        {
            query = query.Where(j => j.Salary != null && j.Salary.Period == salaryPeriod.Value);
        }

        // Filtro por data de publicação: vagas criadas nos últimos N dias
        if (publishedWithinDays is > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-publishedWithinDays.Value);
            query = query.Where(j => j.CreatedAt >= cutoff);
        }

        // Ordenação — sortBy: "createdAt" (default), "title", "salary"
        var descending = sortDescending ?? true;
        query = sortBy?.ToLowerInvariant() switch
        {
            "title" => descending ? query.OrderByDescending(j => j.Title) : query.OrderBy(j => j.Title),
            // Vagas sem salário visível vão sempre para o fim: no PostgreSQL o NULL vem primeiro em ordem
            // decrescente, e "maior salário" listaria antes as vagas que não informam (ou ocultam) o valor.
            "salary" => descending
                ? query.OrderByDescending(j => j.Salary != null && j.Salary.Display && (j.Salary.Max ?? j.Salary.Min) != null)
                    .ThenByDescending(j => j.Salary == null || !j.Salary.Display ? null : j.Salary.Max ?? j.Salary.Min)
                : query.OrderBy(j => j.Salary == null || !j.Salary.Display || (j.Salary.Max ?? j.Salary.Min) == null)
                    .ThenBy(j => j.Salary == null || !j.Salary.Display ? null : j.Salary.Max ?? j.Salary.Min),
            _ => descending ? query.OrderByDescending(j => j.CreatedAt) : query.OrderBy(j => j.CreatedAt),
        };

        // Paginação com OFFSET/FETCH — TotalCount é o total filtrado, não o tamanho da página
        var totalJobs = await query.CountAsync();
        var jobs = await query
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<PagedResponse<Job>>.Success(new PagedResponse<Job>
        {
            Data = jobs,
            TotalCount = totalJobs,
            Page = page,
            Size = size
        });
    }

    public async Task<int> CountActiveByAdvertiserAsync(Guid advertiserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var advertiserKey = Id.Create(advertiserId);
        return await context.Jobs
            .AsNoTracking()
            .CountAsync(j => !j.IsDeleted && j.AdvertiserId == advertiserKey && j.Status == EJobStatus.Active);
    }

    public async Task<RequestResult<IEnumerable<Job>>> ListActiveForRecommendationsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var jobs = await context.Jobs
            .AsNoTracking()
            .Include(j => j.Advertiser)
            .Where(j => !j.IsDeleted && j.Status == EJobStatus.Active && j.ExpirationDate >= now)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

        return RequestResult<IEnumerable<Job>>.Success(jobs);
    }

    public async Task<RequestResult<bool>> IncrementViewsAsync(Guid jobId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var jobKey = Id.Create(jobId);

        // O EF InMemory (provider dos testes de API) não traduz ExecuteUpdate; no banco de verdade,
        // o incremento é atômico, feito numa instrução só
        if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var job = await context.Jobs.FirstOrDefaultAsync(item => item.Id == jobKey);
            if (job is null)
                return RequestResult<bool>.Success(false);

            job.Counters.Views++;
            await context.SaveChangesAsync();
            return RequestResult<bool>.Success(true);
        }

        var updated = await context.Jobs
            .Where(job => job.Id == jobKey)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.Counters.Views, job => job.Counters.Views + 1));

        return RequestResult<bool>.Success(updated > 0);
    }

    /// <summary>
    /// Incrementa o contador de candidaturas de forma atômica. <c>false</c> quando a vaga não existe.
    /// Contar no código (ler, somar, gravar a vaga inteira) perdia a contagem quando duas
    /// candidaturas entravam ao mesmo tempo e ainda sobrescrevia a moderação da vaga.
    /// </summary>
    public async Task<RequestResult<bool>> IncrementApplicationsAsync(Guid jobId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var jobKey = Id.Create(jobId);

        // O EF InMemory (provider dos testes de API) não traduz ExecuteUpdate; no banco de verdade,
        // o incremento é atômico, feito numa instrução só
        if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var job = await context.Jobs.FirstOrDefaultAsync(item => item.Id == jobKey);
            if (job is null)
                return RequestResult<bool>.Success(false);

            job.Counters.Applications++;
            await context.SaveChangesAsync();
            return RequestResult<bool>.Success(true);
        }

        var updated = await context.Jobs
            .Where(job => job.Id == jobKey)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.Counters.Applications, job => job.Counters.Applications + 1));

        return RequestResult<bool>.Success(updated > 0);
    }
}
