using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;
using Monburan.Web.Models;

namespace Monburan.Web.Services;

public sealed record FeatureFlagInput(
    Guid? Id,
    string Name,
    string Description,
    bool EnabledForEveryone,
    IReadOnlyCollection<string> Groups,
    DateTime? RemoveBy);

public sealed record AccessGroupRow(Guid Id, string Name, string Description, int Members, bool IsImplicit);

public sealed record AccessGroupMember(Guid UserId, string Email);

/// <summary>
/// Administração das feature flags, dos grupos de acesso e do papel de produto. Toda mudança fica na auditoria e descarta
/// o cache de avaliação, então vale na hora.
/// </summary>
public sealed class FeatureFlagAdminService
{
    private const int MaxFlagNameLength = 60;

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole<Guid>> _roles;
    private readonly IFeatureFlags _flags;
    private readonly IAdminAuditLog _audit;

    public FeatureFlagAdminService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        IFeatureFlags flags,
        IAdminAuditLog audit)
    {
        _contextFactory = contextFactory;
        _users = users;
        _roles = roles;
        _flags = flags;
        _audit = audit;
    }

    // ---- Flags ----

    public async Task<IReadOnlyList<FeatureFlag>> ListFlagsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.FeatureFlags.AsNoTracking().Where(f => !f.IsDeleted).OrderBy(f => f.Name).ToListAsync(cancellationToken);
    }

    public async Task<RequestResult> SaveFlagAsync(FeatureFlagInput input, AdminActor actor, CancellationToken cancellationToken = default)
    {
        var name = input.Name.Trim();
        var description = input.Description.Trim();
        if (!IsValidFlagName(name))
            return RequestResult.WithDomainError("Nome inválido: use letras minúsculas, números e hífen (ex.: nova-busca-de-vagas).");
        if (description.Length is 0 or > 500)
            return RequestResult.WithDomainError("Descreva a funcionalidade (até 500 caracteres).");

        var groups = input.Groups.Select(g => g.Trim()).Where(g => g.Length > 0).Distinct(StringComparer.Ordinal).ToList();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var known = await context.AccessGroups.AsNoTracking().Where(g => !g.IsDeleted).Select(g => g.Name).ToListAsync(cancellationToken);
        var unknown = groups.Where(g => !AccessGroups.IsImplicit(g) && !known.Contains(g)).ToList();
        if (unknown.Count > 0)
            return RequestResult.WithDomainError($"Grupo inexistente: {string.Join(", ", unknown)}.");

        FeatureFlag flag;
        if (input.Id is { } id)
        {
            var found = await context.FeatureFlags.FirstOrDefaultAsync(f => f.Id == Id.Create(id) && !f.IsDeleted, cancellationToken);
            if (found is null)
                return RequestResult.WithDomainError("Funcionalidade não encontrada.");
            if (found.Name != name)
                return RequestResult.WithDomainError("O nome não muda depois de criado: o código usa esse nome.");
            flag = found;
        }
        else
        {
            if (await context.FeatureFlags.AnyAsync(f => f.Name == name, cancellationToken))
                return RequestResult.WithDomainError("Já existe uma funcionalidade com esse nome.");
            flag = new FeatureFlag { Name = name, TenantId = "default", CreatedBy = actor.Email };
            context.FeatureFlags.Add(flag);
        }

        flag.Description = description;
        flag.EnabledForEveryone = input.EnabledForEveryone;
        flag.Groups = string.Join(',', groups);
        flag.RemoveBy = input.RemoveBy is { } removeBy ? DateTime.SpecifyKind(removeBy.Date, DateTimeKind.Utc) : null;
        flag.UpdatedBy = actor.Email;
        await context.SaveChangesAsync(cancellationToken);

        _flags.Invalidate();
        await _audit.RecordAsync(new AdminAuditRecord(AdminAuditActions.FeatureFlagSaved, actor, AdminAuditActions.FeatureFlagTarget,
            flag.Id.Value.ToString(), flag.Name,
            flag.EnabledForEveryone ? "liberada para todos" : $"grupos: {(groups.Count == 0 ? "nenhum (desligada)" : string.Join(", ", groups))}"),
            cancellationToken);
        return RequestResult.Success();
    }

    public async Task<RequestResult> DeleteFlagAsync(Guid id, AdminActor actor, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var flag = await context.FeatureFlags.FirstOrDefaultAsync(f => f.Id == Id.Create(id), cancellationToken);
        if (flag is null)
            return RequestResult.WithDomainError("Funcionalidade não encontrada.");

        context.FeatureFlags.Remove(flag);
        await context.SaveChangesAsync(cancellationToken);
        _flags.Invalidate();
        await _audit.RecordAsync(new AdminAuditRecord(AdminAuditActions.FeatureFlagDeleted, actor, AdminAuditActions.FeatureFlagTarget,
            id.ToString(), flag.Name), cancellationToken);
        return RequestResult.Success();
    }

    // ---- Grupos ----

    public async Task<IReadOnlyList<AccessGroupRow>> ListGroupsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var groups = await context.AccessGroups.AsNoTracking().Where(g => !g.IsDeleted).OrderBy(g => g.Name).ToListAsync(cancellationToken);
        var counts = await context.UserClaims.AsNoTracking()
            .Where(c => c.ClaimType == AccessGroups.ClaimType)
            .GroupBy(c => c.ClaimValue)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Name ?? string.Empty, g => g.Count, cancellationToken);

        return AccessGroups.Implicit.Select(g => new AccessGroupRow(Guid.Empty, g.Name, g.Description, 0, true))
            .Concat(groups.Select(g => new AccessGroupRow(g.Id.Value, g.Name, g.Description, counts.GetValueOrDefault(g.Name), false)))
            .ToList();
    }

    public async Task<RequestResult> CreateGroupAsync(string name, string description, AdminActor actor, CancellationToken cancellationToken = default)
    {
        name = name.Trim();
        description = description.Trim();
        if (!AccessGroups.IsValidName(name))
            return RequestResult.WithDomainError("Nome inválido: use letras minúsculas, números e hífen (ex.: recrutador-beta), até 40 caracteres.");
        if (AccessGroups.IsImplicit(name))
            return RequestResult.WithDomainError("Esse nome é de um grupo automático.");
        if (description.Length is 0 or > 300)
            return RequestResult.WithDomainError("Descreva quem faz parte do grupo (até 300 caracteres).");

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        if (await context.AccessGroups.AnyAsync(g => g.Name == name, cancellationToken))
            return RequestResult.WithDomainError("Já existe um grupo com esse nome.");

        var group = new AccessGroup { Name = name, Description = description, TenantId = "default", CreatedBy = actor.Email };
        context.AccessGroups.Add(group);
        await context.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync(new AdminAuditRecord(AdminAuditActions.AccessGroupSaved, actor, AdminAuditActions.AccessGroupTarget,
            group.Id.Value.ToString(), name), cancellationToken);
        return RequestResult.Success();
    }

    public async Task<RequestResult> DeleteGroupAsync(Guid id, AdminActor actor, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var group = await context.AccessGroups.FirstOrDefaultAsync(g => g.Id == Id.Create(id), cancellationToken);
        if (group is null)
            return RequestResult.WithDomainError("Grupo não encontrado.");

        var usedBy = (await context.FeatureFlags.AsNoTracking().ToListAsync(cancellationToken))
            .Where(f => f.GroupList.Contains(group.Name)).Select(f => f.Name).ToList();
        if (usedBy.Count > 0)
            return RequestResult.WithDomainError($"O grupo ainda libera: {string.Join(", ", usedBy)}. Tire-o dessas funcionalidades antes.");

        var claims = await context.UserClaims.Where(c => c.ClaimType == AccessGroups.ClaimType && c.ClaimValue == group.Name).ToListAsync(cancellationToken);
        context.UserClaims.RemoveRange(claims);
        context.AccessGroups.Remove(group);
        await context.SaveChangesAsync(cancellationToken);

        _flags.Invalidate();
        await _audit.RecordAsync(new AdminAuditRecord(AdminAuditActions.AccessGroupDeleted, actor, AdminAuditActions.AccessGroupTarget,
            id.ToString(), group.Name, $"{claims.Count} conta(s) saíram do grupo"), cancellationToken);
        return RequestResult.Success();
    }

    public async Task<IReadOnlyList<AccessGroupMember>> ListMembersAsync(string groupName, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await (
                from claim in context.UserClaims.AsNoTracking()
                join user in context.Users.AsNoTracking() on claim.UserId equals user.Id
                where claim.ClaimType == AccessGroups.ClaimType && claim.ClaimValue == groupName
                orderby user.Email
                select new AccessGroupMember(user.Id, user.Email ?? user.UserName ?? user.Id.ToString()))
            .ToListAsync(cancellationToken);
    }

    public async Task<RequestResult> AddMemberAsync(string group, string email, AdminActor actor, CancellationToken cancellationToken = default)
    {
        await using (var context = await _contextFactory.CreateDbContextAsync(cancellationToken))
        {
            if (!await context.AccessGroups.AnyAsync(g => g.Name == group && !g.IsDeleted, cancellationToken))
                return RequestResult.WithDomainError("Grupo não encontrado.");
        }

        var user = await _users.FindByEmailAsync(email.Trim());
        if (user is null)
            return RequestResult.WithDomainError("Nenhuma conta com esse e-mail.");

        var claims = await _users.GetClaimsAsync(user);
        if (claims.Any(c => c.Type == AccessGroups.ClaimType && c.Value == group))
            return RequestResult.WithDomainError("Essa conta já está no grupo.");

        var added = await _users.AddClaimAsync(user, new System.Security.Claims.Claim(AccessGroups.ClaimType, group));
        if (!added.Succeeded)
            return RequestResult.WithDomainError("Não foi possível incluir a conta no grupo.");

        _flags.Invalidate(user.Id);
        await _audit.RecordAsync(new AdminAuditRecord(AdminAuditActions.AccessGroupMemberAdded, actor, AdminAuditActions.AccountTarget,
            user.Id.ToString(), user.Email ?? user.Id.ToString(), $"grupo {group}"), cancellationToken);
        return RequestResult.Success();
    }

    public async Task<RequestResult> RemoveMemberAsync(string group, Guid userId, AdminActor actor, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
            return RequestResult.WithDomainError("Conta não encontrada.");

        var removed = await _users.RemoveClaimAsync(user, new System.Security.Claims.Claim(AccessGroups.ClaimType, group));
        if (!removed.Succeeded)
            return RequestResult.WithDomainError("Não foi possível retirar a conta do grupo.");

        _flags.Invalidate(user.Id);
        await _audit.RecordAsync(new AdminAuditRecord(AdminAuditActions.AccessGroupMemberRemoved, actor, AdminAuditActions.AccountTarget,
            user.Id.ToString(), user.Email ?? user.Id.ToString(), $"grupo {group}"), cancellationToken);
        return RequestResult.Success();
    }

    // ---- Papel de produto (só administradores concedem) ----

    public async Task<IReadOnlyList<AccessGroupMember>> ListProductTeamAsync()
    {
        var users = await _users.GetUsersInRoleAsync(AccessGroups.ProductRole);
        return users.OrderBy(u => u.Email).Select(u => new AccessGroupMember(u.Id, u.Email ?? u.Id.ToString())).ToList();
    }

    public async Task<RequestResult> SetProductRoleAsync(string email, bool grant, AdminActor actor, bool actorIsAdmin)
    {
        if (!actorIsAdmin)
            return RequestResult.WithDomainError("Só administradores mudam a equipe de produto.");

        var user = await _users.FindByEmailAsync(email.Trim());
        if (user is null)
            return RequestResult.WithDomainError("Nenhuma conta com esse e-mail.");

        var hasRole = await _users.IsInRoleAsync(user, AccessGroups.ProductRole);
        if (grant == hasRole)
            return RequestResult.WithDomainError(grant ? "Essa conta já é da equipe de produto." : "Essa conta não é da equipe de produto.");
        if (grant && !user.EmailConfirmed)
            return RequestResult.WithDomainError("Confirme o e-mail da conta antes.");

        if (grant && !await _roles.RoleExistsAsync(AccessGroups.ProductRole))
            await _roles.CreateAsync(new IdentityRole<Guid>(AccessGroups.ProductRole));

        var result = grant
            ? await _users.AddToRoleAsync(user, AccessGroups.ProductRole)
            : await _users.RemoveFromRoleAsync(user, AccessGroups.ProductRole);
        if (!result.Succeeded)
            return RequestResult.WithDomainError("Não foi possível mudar o papel da conta.");

        await _users.UpdateSecurityStampAsync(user); // a sessão passa a trazer (ou perde) o papel
        _flags.Invalidate(user.Id);
        await _audit.RecordAsync(new AdminAuditRecord(
            grant ? AdminAuditActions.AccountProductGranted : AdminAuditActions.AccountProductRevoked,
            actor, AdminAuditActions.AccountTarget, user.Id.ToString(), user.Email ?? user.Id.ToString()));
        return RequestResult.Success();
    }

    private static bool IsValidFlagName(string name) =>
        name.Length <= MaxFlagNameLength && System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9]+(-[a-z0-9]+)*$");
}
