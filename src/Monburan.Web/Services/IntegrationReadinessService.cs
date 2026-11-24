using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;

namespace Monburan.Web.Services;

/// <summary>
/// Monta o resumo mínimo de prontidão do candidato para integrações externas (HiMarshall):
/// perfil de carreira, plano e sinais de completude — sem dados sensíveis além do necessário.
/// </summary>
public interface IIntegrationReadinessService
{
    /// <summary>Resolve o candidato dono do usuário (mesmo vínculo por e-mail usado no JWT).</summary>
    Task<Candidate?> ResolveCandidateAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Resumo consentido: quem é, o que procura e o que já preencheu.</summary>
    Task<IntegrationReadinessSummary?> BuildSummaryAsync(Guid candidateId, CancellationToken cancellationToken = default);
}

public sealed record IntegrationReadinessSummary(
    Guid CandidateId,
    string Name,
    string Email,
    string? ActivePlan,
    bool HasInternationalJobs,
    string? DesiredRole,
    string? Category,
    string? City,
    string? State,
    string? Mode,
    string? ContractType,
    decimal? MinimumSalary,
    IReadOnlyList<string>? PreferredCountries,
    int ResumeCount,
    DateTime GeneratedAt);

public sealed class IntegrationReadinessService(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    ICareerProfileRepository profiles,
    ISubscriptionRepository subscriptions) : IIntegrationReadinessService
{
    public async Task<Candidate?> ResolveCandidateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var email = await context.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(email))
            return null;

        return await context.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email == email && !c.IsDeleted, cancellationToken);
    }

    public async Task<IntegrationReadinessSummary?> BuildSummaryAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var candidateKey = Id.Create(candidateId);
        var profileResult = await profiles.GetByCandidateIdAsync(candidateId, cancellationToken);
        var profile = profileResult.Data;

        var subscription = await subscriptions.GetActiveByCandidateIdAsync(candidateKey, cancellationToken);
        var plan = subscription.Data?.Plan;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var candidate = await context.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == candidateKey && !c.IsDeleted, cancellationToken);
        if (candidate is null)
            return null;

        var resumeCount = await context.Resumes
            .CountAsync(r => r.CandidateId == candidateKey && !r.IsDeleted, cancellationToken);

        return new IntegrationReadinessSummary(
            candidateId,
            candidate.Name,
            candidate.Email,
            candidate.ActivePlan,
            plan?.HasInternationalJobs == true,
            profile?.DesiredRole,
            profile?.Category,
            profile?.City,
            profile?.State,
            profile?.Mode,
            profile?.ContractType?.ToString(),
            profile?.MinimumSalary,
            PreferredCountryCodes.Parse(profile?.PreferredCountries),
            resumeCount,
            DateTime.UtcNow);
    }
}
