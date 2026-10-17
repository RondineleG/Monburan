namespace Monburan.Web.Services;

/// <summary>
/// Mantém o perfil de candidato/anunciante consistente com a conta do Identity.
/// O vínculo conta ↔ perfil é o e-mail (<see cref="ApplicationUserClaimsPrincipalFactory"/>):
/// se os dois divergem, o login deixa de emitir as claims de perfil.
/// </summary>
public sealed class AccountProfileService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<AccountProfileService> _logger;

    public AccountProfileService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<AccountProfileService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>Reflete no perfil a confirmação de e-mail feita no Identity.</summary>
    public async Task MarkEmailVerifiedAsync(string email)
    {
        var normalized = Normalize(email);
        await using var context = await _contextFactory.CreateDbContextAsync();

        var candidates = await context.Candidates
            .Where(c => !c.IsDeleted && c.Email.ToLower() == normalized)
            .ToListAsync();
        var advertisers = await context.Advertisers
            .Where(a => !a.IsDeleted && a.Email.ToLower() == normalized)
            .ToListAsync();

        foreach (var candidate in candidates)
            candidate.IsEmailVerified = true;
        foreach (var advertiser in advertisers)
            advertiser.IsEmailVerified = true;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Outro perfil (que não o do e-mail atual) já usa <paramref name="newEmail"/>?
    /// Os perfis têm índice único por e-mail — a troca falharia depois de alterar a conta.
    /// </summary>
    public async Task<bool> IsEmailUsedByAnotherProfileAsync(string newEmail, string? currentEmail)
    {
        var target = Normalize(newEmail);
        var current = currentEmail is null ? null : Normalize(currentEmail);
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Candidates.AnyAsync(c => c.Email.ToLower() == target && c.Email.ToLower() != current)
            || await context.Advertisers.AnyAsync(a => a.Email.ToLower() == target && a.Email.ToLower() != current);
    }

    /// <summary>Acompanha a troca de e-mail da conta, preservando o vínculo com o perfil.</summary>
    public async Task ChangeEmailAsync(string oldEmail, string newEmail)
    {
        var old = Normalize(oldEmail);
        var updated = newEmail.Trim();
        await using var context = await _contextFactory.CreateDbContextAsync();

        var candidates = await context.Candidates
            .Where(c => !c.IsDeleted && c.Email.ToLower() == old)
            .ToListAsync();
        var advertisers = await context.Advertisers
            .Where(a => !a.IsDeleted && a.Email.ToLower() == old)
            .ToListAsync();

        foreach (var candidate in candidates)
        {
            candidate.Email = updated;
            candidate.IsEmailVerified = true;
        }

        foreach (var advertiser in advertisers)
        {
            advertiser.Email = updated;
            advertiser.IsEmailVerified = true;
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Exclusão de conta (LGPD): remove os dados pessoais do perfil e o desativa.
    /// O e-mail é liberado para um novo cadastro; histórico de candidaturas fica anônimo.
    /// </summary>
    public async Task AnonymizeProfilesAsync(string email)
    {
        var normalized = Normalize(email);
        var now = DateTime.UtcNow;
        await using var context = await _contextFactory.CreateDbContextAsync();

        var candidates = await context.Candidates
            .Where(c => c.Email.ToLower() == normalized)
            .ToListAsync();
        foreach (var candidate in candidates)
        {
            var candidateId = candidate.Id;
            candidate.Name = "Conta excluída";
            candidate.Email = $"excluido-{candidateId.Value:N}@monburan.invalid";
            candidate.Phone = null;
            candidate.LinkedIn = null;
            candidate.DateOfBirth = null;
            candidate.IsEmailVerified = false;
            candidate.IsPhoneVerified = false;
            candidate.IsDeleted = true;
            candidate.DeletedAt = now;

            var resumes = await context.Resumes
                .Where(r => r.CandidateId == candidateId && !r.IsDeleted)
                .ToListAsync();
            foreach (var resume in resumes)
            {
                resume.Summary = null;
                resume.IsDeleted = true;
                resume.DeletedAt = now;
            }
        }

        var advertisers = await context.Advertisers
            .Where(a => a.Email.ToLower() == normalized)
            .ToListAsync();
        foreach (var advertiser in advertisers)
        {
            var advertiserId = advertiser.Id;
            advertiser.Email = $"excluido-{advertiserId.Value:N}@monburan.invalid";
            advertiser.Phone = null;
            advertiser.TaxId = null;
            advertiser.IsEmailVerified = false;
            advertiser.IsPhoneVerified = false;
            advertiser.IsDeleted = true;
            advertiser.DeletedAt = now;

            // Vagas de uma empresa sem conta não podem continuar recebendo candidaturas
            var jobs = await context.Jobs
                .Where(j => j.AdvertiserId == advertiserId && !j.IsDeleted)
                .ToListAsync();
            foreach (var job in jobs)
            {
                job.IsDeleted = true;
                job.DeletedAt = now;
            }
        }

        await context.SaveChangesAsync();
        _logger.LogInformation(
            "Perfis anonimizados na exclusão de conta: {Candidates} candidato(s), {Advertisers} anunciante(s)",
            candidates.Count, advertisers.Count);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
