using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Repositories;

public class ResumeRepository : IResumeRepository
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public ResumeRepository(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<RequestResult<Resume>> CreateAsync(Resume entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Resumes.Add(entity);
        await context.SaveChangesAsync();
        return RequestResult<Resume>.Success(entity);
    }

    public async Task<RequestResult<Resume>> DeleteAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var resume = await context.Resumes.FindAsync(id);
        if (resume == null)
            return RequestResult<Resume>.EntityNotFound("Resume", id.Value, "Currículo não encontrado.");

        resume.IsDeleted = true;
        resume.DeletedAt = DateTime.UtcNow;

        context.Resumes.Update(resume);
        await context.SaveChangesAsync();
        return RequestResult<Resume>.Success(resume);
    }

    public async Task<RequestResult<IEnumerable<Resume>>> GetAllAsync(int page, int size)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var resumes = await context.Resumes
            .Include(r => r.Candidate)
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return RequestResult<IEnumerable<Resume>>.Success(resumes);
    }

    public async Task<RequestResult<Resume>> GetByIdAsync(Id id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        // Habilidades são entidade própria (não owned): sem Include nunca apareciam no detalhe/edição
        var resume = await context.Resumes
            .AsNoTracking()
            .Include(r => r.Candidate)
            .Include(r => r.Skills)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);

        if (resume == null)
            return RequestResult<Resume>.EntityNotFound("Resume", id.Value, "Currículo não encontrado.");

        return RequestResult<Resume>.Success(resume);
    }

    public async Task<RequestResult<Resume>> UpdateAsync(Resume entity)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Formação, experiência e idiomas são coleções owned com chave sombra: Update() num
        // grafo desanexado falhava ("shadow key property 'Education.Id' is unknown") e editar
        // o currículo com essas seções dava erro. Aplica as mudanças na versão rastreada.
        var tracked = await context.Resumes
            .Include(r => r.Skills)
            .FirstOrDefaultAsync(r => r.Id == entity.Id && !r.IsDeleted);
        if (tracked is null)
            return RequestResult<Resume>.EntityNotFound("Resume", entity.Id.Value, "Currículo não encontrado.");

        tracked.CandidateId = entity.CandidateId;
        tracked.Title = entity.Title;
        tracked.Summary = entity.Summary;
        tracked.IsActive = entity.IsActive;

        ReplaceOwned(tracked.EducationHistory, entity.EducationHistory);
        ReplaceOwned(tracked.ExperienceHistory, entity.ExperienceHistory);
        ReplaceOwned(tracked.Languages, entity.Languages);
        MergeSkills(context, tracked, entity.Skills);

        await context.SaveChangesAsync();
        return RequestResult<Resume>.Success(tracked);
    }

    private static void ReplaceOwned<T>(ICollection<T> current, IEnumerable<T> updated)
    {
        var items = updated.ToList();
        current.Clear();
        foreach (var item in items)
            current.Add(item);
    }

    // ResumeSkill tem chave (ResumeId, Skill): remover e incluir a mesma habilidade no mesmo
    // SaveChanges colide no rastreamento — então atualiza as existentes e só inclui as novas
    private static void MergeSkills(ApplicationDbContext context, Resume tracked, IEnumerable<ResumeSkill> updated)
    {
        var desired = updated
            .Where(s => !string.IsNullOrWhiteSpace(s.Skill))
            .GroupBy(s => s.Skill.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .ToList();

        foreach (var existing in tracked.Skills.ToList())
        {
            if (!desired.Any(d => string.Equals(d.Skill.Trim(), existing.Skill, StringComparison.OrdinalIgnoreCase)))
                context.Remove(existing);
        }

        foreach (var skill in desired)
        {
            var match = tracked.Skills.FirstOrDefault(s => string.Equals(s.Skill, skill.Skill.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                tracked.Skills.Add(new ResumeSkill
                {
                    ResumeId = tracked.Id,
                    Skill = skill.Skill.Trim(),
                    Level = skill.Level,
                    YearsOfExperience = skill.YearsOfExperience,
                    TenantId = tracked.TenantId
                });
            }
            else
            {
                match.Level = skill.Level;
                match.YearsOfExperience = skill.YearsOfExperience;
            }
        }
    }

    public async Task<RequestResult<IEnumerable<Resume>>> ListByCandidateAsync(Guid candidateId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var candidateKey = Id.Create(candidateId);
        var resumes = await context.Resumes
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.CandidateId == candidateKey)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return RequestResult<IEnumerable<Resume>>.Success(resumes);
    }

    public async Task<RequestResult<Resume?>> GetActiveWithSkillsAsync(Guid candidateId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var candidateKey = Id.Create(candidateId);
        var resume = await context.Resumes
            .AsNoTracking()
            .Include(r => r.Skills)
            .FirstOrDefaultAsync(r => !r.IsDeleted && r.IsActive && r.CandidateId == candidateKey);

        return RequestResult<Resume?>.Success(resume);
    }
}
