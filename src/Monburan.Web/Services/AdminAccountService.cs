using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Web.Services;

public sealed record AdminAccountFilter(string? SearchTerm = null, string? Type = null, string? Status = null);

public sealed record AdminAccountRow(
    Guid Id,
    string Email,
    string Name,
    string? ProfileType,
    string? PlanName,
    bool IsAdmin,
    bool IsConfiguredAdmin,
    bool IsActive,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    bool IsLockedOut,
    DateTimeOffset? LockoutEnd,
    DateTime? CreatedAt,
    DateTime? LastLoginAt,
    DateTime? DeactivatedAt,
    string? DeactivatedBy,
    string? DeactivationReason);

public sealed record AdminAccountCounts(int Total, int Active, int Inactive, int Locked);

/// <summary>Plano oferecido no dropdown da tela de contas (filtrado pela audiência do perfil).</summary>
public sealed record AdminPlanOption(Guid Id, string Name, decimal Price, ESubscriptionPeriod Period);

public sealed record AdminAccountPage(IReadOnlyList<AdminAccountRow> Items, int TotalCount, int Page, int Size, AdminAccountCounts Counts)
{
    public int TotalPages => Size <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)Size);
}

/// <summary>
/// Gestão das contas pela administração: listar e buscar, inativar e reativar, desbloquear, confirmar e-mail, encerrar
/// sessões, redefinir o 2FA e conceder ou retirar o papel de administrador. Toda ação é auditada e respeita as travas:
/// ninguém inativa a si mesmo, o último administrador ativo não sai e a inativação tem efeito imediato (sessões e API).
/// </summary>
public sealed class AdminAccountService
{
    public const string AdminRole = "Admin";

    /// <summary>Motivo gravado nas vagas escondidas porque a conta da empresa foi inativada; é o que permite reexibi-las depois.</summary>
    public const string JobsHiddenReason = "Conta da empresa inativada pela administração.";

    private const int ReasonMinLength = 5;
    private const int ReasonMaxLength = 500;

    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IAdminAuditLog _audit;
    private readonly ActiveAccountChecker _activeAccounts;
    private readonly AccountProfileService _profiles;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _time;

    public AdminAccountService(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        IDbContextFactory<ApplicationDbContext> contexts,
        IAdminAuditLog audit,
        ActiveAccountChecker activeAccounts,
        AccountProfileService profiles,
        ISubscriptionRepository subscriptions,
        IConfiguration configuration,
        TimeProvider time)
    {
        _users = users;
        _roles = roles;
        _contexts = contexts;
        _audit = audit;
        _activeAccounts = activeAccounts;
        _profiles = profiles;
        _subscriptions = subscriptions;
        _configuration = configuration;
        _time = time;
    }

    public async Task<AdminAccountPage> ListAsync(AdminAccountFilter filter, int page, int size, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        size = size is < 1 or > 100 ? 20 : size;
        var now = _time.GetUtcNow();
        var adminEmails = AdminEmails.Read(_configuration);

        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        var all = context.Users.AsNoTracking();
        var query = all;

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(user =>
                (user.Email != null && user.Email.ToLower().Contains(term))
                || (user.FirstName + " " + user.LastName).ToLower().Contains(term));
        }

        query = filter.Status?.ToLowerInvariant() switch
        {
            "active" => query.Where(user => user.IsActive),
            "inactive" => query.Where(user => !user.IsActive),
            "locked" => query.Where(user => user.LockoutEnd != null && user.LockoutEnd > now),
            "unconfirmed" => query.Where(user => !user.EmailConfirmed),
            _ => query
        };

        query = filter.Type?.ToLowerInvariant() switch
        {
            "candidate" => query.Where(user => user.Email != null
                && context.Candidates.Any(candidate => !candidate.IsDeleted && candidate.Email.ToLower() == user.Email.ToLower())),
            "advertiser" => query.Where(user => user.Email != null
                && context.Advertisers.Any(advertiser => !advertiser.IsDeleted && advertiser.Email.ToLower() == user.Email.ToLower())),
            "admin" => query.Where(user =>
                context.UserRoles.Any(link => link.UserId == user.Id && context.Roles.Any(role => role.Id == link.RoleId && role.Name == AdminRole))
                || (user.EmailConfirmed && user.Email != null && adminEmails.Contains(user.Email.ToLower()))),
            _ => query
        };

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(user => user.CreatedAt)
            .ThenBy(user => user.Email)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        var rows = await BuildRowsAsync(context, users, now, adminEmails, cancellationToken);

        var counts = new AdminAccountCounts(
            await all.CountAsync(cancellationToken),
            await all.CountAsync(user => user.IsActive, cancellationToken),
            await all.CountAsync(user => !user.IsActive, cancellationToken),
            await all.CountAsync(user => user.LockoutEnd != null && user.LockoutEnd > now, cancellationToken));

        return new AdminAccountPage(rows, total, page, size, counts);
    }

    /// <summary>Uma conta pelo id, no mesmo formato da lista (a tela de gestão a usa para se atualizar depois de cada ação).</summary>
    public async Task<AdminAccountRow?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
            return null;

        var rows = await BuildRowsAsync(context, [user], _time.GetUtcNow(), AdminEmails.Read(_configuration), cancellationToken);
        return rows[0];
    }

    private static async Task<List<AdminAccountRow>> BuildRowsAsync(
        ApplicationDbContext context,
        IReadOnlyList<ApplicationUser> users,
        DateTimeOffset now,
        IReadOnlyCollection<string> adminEmails,
        CancellationToken cancellationToken)
    {
        var emails = users.Where(user => user.Email != null).Select(user => user.Email!.ToLower()).ToList();
        var ids = users.Select(user => user.Id).ToList();

        var candidateNames = await context.Candidates.AsNoTracking()
            .Where(candidate => !candidate.IsDeleted && emails.Contains(candidate.Email.ToLower()))
            .Select(candidate => new { Email = candidate.Email.ToLower(), candidate.Name, candidate.ActivePlan })
            .ToListAsync(cancellationToken);
        var advertiserNames = await context.Advertisers.AsNoTracking()
            .Where(advertiser => !advertiser.IsDeleted && emails.Contains(advertiser.Email.ToLower()))
            .Select(advertiser => new { Email = advertiser.Email.ToLower(), advertiser.Name, advertiser.ActivePlan })
            .ToListAsync(cancellationToken);
        var adminIds = await (
            from link in context.UserRoles
            join role in context.Roles on link.RoleId equals role.Id
            where role.Name == AdminRole && ids.Contains(link.UserId)
            select link.UserId).ToListAsync(cancellationToken);

        return users.Select(user =>
        {
            var email = user.Email ?? string.Empty;
            var key = email.ToLowerInvariant();
            var candidate = candidateNames.FirstOrDefault(item => item.Email == key);
            var advertiser = advertiserNames.FirstOrDefault(item => item.Email == key);
            var configured = AdminEmails.IsConfiguredAdmin(user, adminEmails);
            var fullName = $"{user.FirstName} {user.LastName}".Trim();

            return new AdminAccountRow(
                user.Id,
                email,
                candidate?.Name ?? advertiser?.Name ?? (fullName.Length > 0 ? fullName : email),
                candidate is not null ? "candidate" : advertiser is not null ? "advertiser" : null,
                candidate?.ActivePlan ?? advertiser?.ActivePlan,
                adminIds.Contains(user.Id) || configured,
                configured,
                user.IsActive,
                user.EmailConfirmed,
                user.TwoFactorEnabled,
                user.LockoutEnd is { } end && end > now,
                user.LockoutEnd,
                user.CreatedAt,
                user.LastLoginAt,
                user.DeactivatedAt,
                user.DeactivatedBy,
                user.DeactivationReason);
        }).ToList();
    }

    public async Task<RequestResult> DeactivateAsync(Guid userId, string? reason, AdminActor actor)
    {
        var cleaned = reason?.Trim() ?? string.Empty;
        if (cleaned.Length < ReasonMinLength)
            return RequestResult.WithValidationError($"Informe o motivo da inativação (pelo menos {ReasonMinLength} caracteres).", "reason");
        if (cleaned.Length > ReasonMaxLength)
            return RequestResult.WithValidationError($"O motivo deve ter no máximo {ReasonMaxLength} caracteres.", "reason");

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound(userId);
        if (IsSelf(user, actor))
            return RequestResult.WithDomainError("Você não pode inativar a própria conta.");
        if (!user.IsActive)
            return RequestResult.WithDomainError("Esta conta já está inativa.");
        if (await IsAdminAsync(user) && await CountActiveAdminsAsync() <= 1)
            return RequestResult.WithDomainError("Esta é a última conta de administrador ativa. Dê o papel a outra pessoa antes de inativá-la.");

        user.IsActive = false;
        user.DeactivatedAt = _time.GetUtcNow().UtcDateTime;
        user.DeactivatedBy = actor.Email;
        user.DeactivationReason = cleaned;
        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded)
            return Failed(update);

        // Troca o carimbo de segurança: as sessões abertas caem na próxima revalidação e os tokens de renovação deixam de valer
        await _users.UpdateSecurityStampAsync(user);
        await ApplyProfilesAsync(user.Email, blocked: true, actor);
        _activeAccounts.Invalidate(user.Id.ToString());

        await RecordAsync(AdminAuditActions.AccountDeactivated, actor, user, cleaned);
        return RequestResult.Success();
    }

    public async Task<RequestResult> ActivateAsync(Guid userId, AdminActor actor)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound(userId);
        if (user.IsActive)
            return RequestResult.WithDomainError("Esta conta já está ativa.");

        var previousReason = user.DeactivationReason;
        user.IsActive = true;
        user.DeactivatedAt = null;
        user.DeactivatedBy = null;
        user.DeactivationReason = null;
        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded)
            return Failed(update);

        await ApplyProfilesAsync(user.Email, blocked: false, actor);
        _activeAccounts.Invalidate(user.Id.ToString());

        await RecordAsync(AdminAuditActions.AccountActivated, actor, user,
            string.IsNullOrWhiteSpace(previousReason) ? null : $"estava inativa: {previousReason}");
        return RequestResult.Success();
    }

    public async Task<RequestResult> UnlockAsync(Guid userId, AdminActor actor)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound(userId);
        if (!await _users.IsLockedOutAsync(user))
            return RequestResult.WithDomainError("Esta conta não está bloqueada por tentativas de login.");

        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);

        await RecordAsync(AdminAuditActions.AccountUnlocked, actor, user);
        return RequestResult.Success();
    }

    public async Task<RequestResult> ConfirmEmailAsync(Guid userId, AdminActor actor)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound(userId);
        if (user.EmailConfirmed)
            return RequestResult.WithDomainError("O e-mail desta conta já está confirmado.");

        user.EmailConfirmed = true;
        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded)
            return Failed(update);

        if (!string.IsNullOrWhiteSpace(user.Email))
            await _profiles.MarkEmailVerifiedAsync(user.Email);

        await RecordAsync(AdminAuditActions.AccountConfirmedByAdmin, actor, user);
        return RequestResult.Success();
    }

    public async Task<RequestResult> RevokeSessionsAsync(Guid userId, AdminActor actor)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound(userId);

        await _users.UpdateSecurityStampAsync(user);

        await RecordAsync(AdminAuditActions.AccountSessionsRevoked, actor, user);
        return RequestResult.Success();
    }

    public async Task<RequestResult> ResetTwoFactorAsync(Guid userId, AdminActor actor)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound(userId);
        if (IsSelf(user, actor))
            return RequestResult.WithDomainError("Para a sua própria conta use a página de segurança, em Minha conta.");
        if (!user.TwoFactorEnabled)
            return RequestResult.WithDomainError("Esta conta não usa a verificação em duas etapas.");

        await _users.SetTwoFactorEnabledAsync(user, false);
        await _users.ResetAuthenticatorKeyAsync(user);
        await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, 0); // apaga os códigos de recuperação
        await _users.UpdateSecurityStampAsync(user);

        await RecordAsync(AdminAuditActions.AccountTwoFactorReset, actor, user, "a pessoa precisará configurar o autenticador de novo");
        return RequestResult.Success();
    }

    public async Task<RequestResult> SetAdminAsync(Guid userId, bool grant, AdminActor actor)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound(userId);

        var configured = AdminEmails.IsConfiguredAdmin(user, AdminEmails.Read(_configuration));
        var hasRole = await _users.IsInRoleAsync(user, AdminRole);

        if (grant)
        {
            if (hasRole || configured)
                return RequestResult.WithDomainError("Esta conta já é de administrador.");
            if (!user.IsActive)
                return RequestResult.WithDomainError("Reative a conta antes de torná-la administradora.");
            if (!user.EmailConfirmed)
                return RequestResult.WithDomainError("Confirme o e-mail da conta antes de torná-la administradora.");

            if (!await _roles.RoleExistsAsync(AdminRole))
                await _roles.CreateAsync(new IdentityRole<Guid>(AdminRole));

            var added = await _users.AddToRoleAsync(user, AdminRole);
            if (!added.Succeeded)
                return Failed(added);

            await _users.UpdateSecurityStampAsync(user); // a sessão passa a trazer o papel
            await RecordAsync(AdminAuditActions.AccountAdminGranted, actor, user);
            return RequestResult.Success();
        }

        if (IsSelf(user, actor))
            return RequestResult.WithDomainError("Você não pode remover o próprio papel de administrador.");
        if (configured)
            return RequestResult.WithDomainError("Este administrador está definido em ADMIN_EMAILS. Para retirar o acesso, remova o e-mail de lá.");
        if (!hasRole)
            return RequestResult.WithDomainError("Esta conta não é de administrador.");
        if (user.IsActive && await CountActiveAdminsAsync() <= 1)
            return RequestResult.WithDomainError("Esta é a última conta de administrador ativa. Dê o papel a outra pessoa antes de retirá-lo.");

        var removed = await _users.RemoveFromRoleAsync(user, AdminRole);
        if (!removed.Succeeded)
            return Failed(removed);

        await _users.UpdateSecurityStampAsync(user);
        await RecordAsync(AdminAuditActions.AccountAdminRevoked, actor, user);
        return RequestResult.Success();
    }

    /// <summary>Planos ativos da audiência do perfil da conta — o que o dropdown da tela oferece.</summary>
    public async Task<IReadOnlyList<AdminPlanOption>> ListPlansAsync(string? profileType, CancellationToken cancellationToken = default)
    {
        var audience = profileType == "candidate" ? EPlanAudience.Candidate : EPlanAudience.Advertiser;
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        return await context.SubscriptionPlans.AsNoTracking()
            .Where(plan => plan.IsActive && plan.Audience == audience)
            .OrderBy(plan => plan.Price)
            .Select(plan => new AdminPlanOption(plan.Id.Value, plan.Name, plan.Price, plan.Period))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Aplica um plano à conta pela administração, sem cobrança: cancela a assinatura ativa,
    /// grava a nova com a vigência do período do plano e atualiza o plano do perfil — na mesma
    /// transação. Aplicar o plano Free equivale a inativar o premium da conta.
    /// </summary>
    public async Task<RequestResult> SetPlanAsync(Guid userId, Guid planId, AdminActor actor, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return NotFound(userId);
        if (string.IsNullOrWhiteSpace(user.Email))
            return RequestResult.WithDomainError("Esta conta não tem e-mail para localizar o perfil.");

        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        var key = user.Email.ToLowerInvariant();
        var candidate = await context.Candidates.AsNoTracking()
            .FirstOrDefaultAsync(item => !item.IsDeleted && item.Email.ToLower() == key, cancellationToken);
        var advertiser = candidate is null
            ? await context.Advertisers.AsNoTracking()
                .FirstOrDefaultAsync(item => !item.IsDeleted && item.Email.ToLower() == key, cancellationToken)
            : null;
        if (candidate is null && advertiser is null)
            return RequestResult.WithDomainError("A conta não tem perfil de candidato nem de empresa para receber um plano.");

        var plan = await context.SubscriptionPlans.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == Id.Create(planId) && item.IsActive, cancellationToken);
        if (plan is null)
            return RequestResult.EntityNotFound("SubscriptionPlan", planId, "Plano não encontrado.");

        IHasActivePlan owner;
        RequestResult<Subscription?> activeResult;
        if (candidate is not null)
        {
            if (plan.Audience != EPlanAudience.Candidate)
                return RequestResult.WithDomainError("Este plano é de empresa e não vale para candidato.");
            owner = candidate;
            activeResult = await _subscriptions.GetActiveByCandidateIdAsync(candidate.Id, cancellationToken);
        }
        else
        {
            if (plan.Audience != EPlanAudience.Advertiser)
                return RequestResult.WithDomainError("Este plano é de candidato e não vale para empresa.");
            owner = advertiser!;
            activeResult = await _subscriptions.GetActiveByAdvertiserIdAsync(advertiser!.Id, cancellationToken);
        }
        if (activeResult.Status != EResultStatus.Success)
            return RequestResult.FromResult(activeResult);

        var previous = activeResult.Data;
        if (previous is not null && previous.PlanId == plan.Id)
            return RequestResult.WithDomainError("A conta já está neste plano.");

        var now = _time.GetUtcNow().UtcDateTime;
        if (previous is not null)
        {
            previous.Status = ESubscriptionStatus.Cancelled;
            previous.EndDate = now;
            previous.CancellationReason = "Plano alterado pela administração";
        }

        var subscription = new Subscription
        {
            Id = Id.Create(Guid.NewGuid()),
            AdvertiserId = advertiser?.Id,
            CandidateId = candidate?.Id,
            PlanId = plan.Id,
            StartDate = now,
            EndDate = plan.Period switch
            {
                ESubscriptionPeriod.Monthly => now.AddMonths(1),
                ESubscriptionPeriod.Annual => now.AddYears(1),
                _ => null
            },
            Status = ESubscriptionStatus.Active,
            TenantId = "default"
        };

        var previousPlan = owner.ActivePlan;
        owner.ActivePlan = plan.Name;
        var change = await _subscriptions.ChangePlanAsync(previous, subscription, owner, cancellationToken);
        if (change.Status != EResultStatus.Success)
            return RequestResult.FromResult(change);

        await RecordAsync(AdminAuditActions.AccountPlanChanged, actor, user,
            $"{previousPlan ?? "nenhum"} → {plan.Name}");
        return RequestResult.Success();
    }

    private async Task<bool> IsAdminAsync(ApplicationUser user) =>
        await _users.IsInRoleAsync(user, AdminRole) || AdminEmails.IsConfiguredAdmin(user, AdminEmails.Read(_configuration));

    private async Task<int> CountActiveAdminsAsync()
    {
        var adminEmails = AdminEmails.Read(_configuration);
        await using var context = await _contexts.CreateDbContextAsync();
        return await context.Users.CountAsync(user => user.IsActive
            && (context.UserRoles.Any(link => link.UserId == user.Id && context.Roles.Any(role => role.Id == link.RoleId && role.Name == AdminRole))
                || (user.EmailConfirmed && user.Email != null && adminEmails.Contains(user.Email.ToLower()))));
    }

    /// <summary>
    /// Reflete a situação da conta nos perfis ligados a ela: o perfil fica bloqueado ou volta a ativo e, no caso da empresa,
    /// as vagas ativas somem do site na inativação e voltam na reativação (só as que a própria inativação escondeu).
    /// </summary>
    private async Task ApplyProfilesAsync(string? email, bool blocked, AdminActor actor)
    {
        if (string.IsNullOrWhiteSpace(email))
            return;

        var key = email.ToLowerInvariant();
        var today = _time.GetUtcNow().UtcDateTime.Date;
        await using var context = await _contexts.CreateDbContextAsync();

        var candidates = await context.Candidates.Where(candidate => !candidate.IsDeleted && candidate.Email.ToLower() == key).ToListAsync();
        foreach (var candidate in candidates)
            candidate.Status = blocked ? EAccountStatus.Blocked : EAccountStatus.Active;

        var advertisers = await context.Advertisers.Where(advertiser => !advertiser.IsDeleted && advertiser.Email.ToLower() == key).ToListAsync();
        foreach (var advertiser in advertisers)
        {
            advertiser.Status = blocked ? EAdvertiserStatus.Blocked : EAdvertiserStatus.Active;

            if (blocked)
            {
                var visible = await context.Jobs.Where(job => !job.IsDeleted && job.AdvertiserId == advertiser.Id && job.Status == EJobStatus.Active).ToListAsync();
                foreach (var job in visible)
                {
                    job.Status = EJobStatus.Hidden;
                    job.Moderation ??= new ModerationInfo();
                    job.Moderation.Reason = JobsHiddenReason;
                    job.Moderation.ModeratorId = actor.Id;
                    job.Moderation.ModerationDate = _time.GetUtcNow().UtcDateTime;
                }
            }
            else
            {
                var hidden = await context.Jobs
                    .Where(job => !job.IsDeleted && job.AdvertiserId == advertiser.Id && job.Status == EJobStatus.Hidden && job.Moderation!.Reason == JobsHiddenReason)
                    .ToListAsync();
                foreach (var job in hidden)
                {
                    job.Status = job.ExpirationDate.Date >= today ? EJobStatus.Active : EJobStatus.Expired;
                    job.Moderation!.Reason = null;
                    job.Moderation.ModeratorId = null;
                    job.Moderation.ModerationDate = null;
                }
            }
        }

        await context.SaveChangesAsync();
    }

    private Task RecordAsync(string action, AdminActor actor, ApplicationUser user, string? details = null) =>
        _audit.RecordAsync(new AdminAuditRecord(action, actor, AdminAuditActions.AccountTarget, user.Id.ToString(), user.Email ?? user.UserName ?? user.Id.ToString(), details));

    private static bool IsSelf(ApplicationUser user, AdminActor actor) =>
        string.Equals(user.Id.ToString(), actor.Id, StringComparison.OrdinalIgnoreCase);

    private static RequestResult NotFound(Guid userId) => RequestResult.EntityNotFound("Account", userId, "Conta não encontrada.");

    private static RequestResult Failed(IdentityResult result) =>
        RequestResult.WithDomainError(string.Join(" ", result.Errors.Select(error => error.Description)));
}
