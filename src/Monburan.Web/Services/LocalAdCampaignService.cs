using System.Globalization;
using System.Text.RegularExpressions;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;
using Monburan.Web.Data;

namespace Monburan.Web.Services;

public sealed record LocalAdCampaignInput(
    Guid? Id,
    string SponsorName,
    string Headline,
    string? Description,
    string? ImageUrl,
    string TargetUrl,
    string LocationsText,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsActive);

public sealed record LocalAdCampaignRow(
    Guid Id,
    string SponsorName,
    string Headline,
    string? Description,
    string? ImageUrl,
    string TargetUrl,
    string LocationsText,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsActive,
    int ImpressionCount,
    int ClickCount);

public sealed record LocalAdPlacement(
    Guid Id,
    string SponsorName,
    string Headline,
    string? Description,
    string? ImageUrl,
    string ClickUrl);

/// <summary>Gestão e exibição de campanhas de publicidade local, segmentadas pela localidade da vaga.</summary>
public sealed class LocalAdCampaignService
{
    public const string FeatureName = "publicidade-local";
    private const int MaxCampaignsPerState = 500;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex LocalImagePath = new(
        @"^/uploads/\d{4}/\d{2}/[0-9a-f]{32}\.(jpg|png|gif|webp)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IFeatureFlags _flags;
    private readonly IAdminAuditLog _audit;
    private readonly TimeProvider _time;

    public LocalAdCampaignService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IFeatureFlags flags,
        IAdminAuditLog audit,
        TimeProvider time)
    {
        _contexts = contexts;
        _flags = flags;
        _audit = audit;
        _time = time;
    }

    public async Task<IReadOnlyList<LocalAdCampaignRow>> ListAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(user, cancellationToken)) return [];

        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        var campaigns = await context.LocalAdCampaigns.AsNoTracking()
            .Where(campaign => !campaign.IsDeleted)
            .OrderByDescending(campaign => campaign.CreatedAt)
            .ToListAsync(cancellationToken);
        return campaigns.Select(ToRow).ToList();
    }

    public async Task<RequestResult> SaveAsync(
        LocalAdCampaignInput input,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(user, cancellationToken))
            return RequestResult.WithDomainError("Você não tem acesso à publicidade local.");

        var sponsor = input.SponsorName.Trim();
        var headline = input.Headline.Trim();
        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        var targetUrl = input.TargetUrl.Trim();
        var imageUrl = string.IsNullOrWhiteSpace(input.ImageUrl) ? null : input.ImageUrl.Trim();
        if (sponsor.Length is 0 or > 120 || headline.Length is 0 or > 100 || description?.Length > 280)
            return RequestResult.WithDomainError("Informe o anunciante, um título (até 100 caracteres) e uma descrição de até 280 caracteres.");
        if (!IsHttpsUrl(targetUrl) || targetUrl.Length > 1000)
            return RequestResult.WithDomainError("O link do anúncio deve ser uma URL HTTPS válida de até 1000 caracteres.");
        if (imageUrl is not null && (!LocalImagePath.IsMatch(imageUrl) || imageUrl.Length > 1000))
            return RequestResult.WithDomainError("Envie uma imagem pelo campo de upload. São aceitos JPEG, PNG, GIF ou WebP.");
        if (input.EndsAt.Date < input.StartsAt.Date)
            return RequestResult.WithDomainError("A data final da campanha deve ser igual ou posterior à inicial.");

        var locations = ParseLocations(input.LocationsText, out var locationError);
        if (locationError is not null) return RequestResult.WithDomainError(locationError);
        if (locations.Count == 0)
            return RequestResult.WithDomainError("Informe pelo menos uma cidade e UF para segmentar o anúncio.");

        var start = DateTime.SpecifyKind(input.StartsAt.Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(input.EndsAt.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        var stateFilter = $"|{string.Join("|", locations.Select(location => location.State).Distinct(StringComparer.Ordinal))}|";

        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        LocalAdCampaign campaign;
        if (input.Id is { } id)
        {
            campaign = await context.LocalAdCampaigns.FirstOrDefaultAsync(
                item => item.Id == Id.Create(id) && !item.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("Campanha não encontrada.");
        }
        else
        {
            campaign = new LocalAdCampaign
            {
                Id = Id.Create(Guid.NewGuid()),
                TenantId = "default",
                CreatedBy = ActorEmail(user)
            };
            context.LocalAdCampaigns.Add(campaign);
        }

        campaign.SponsorName = sponsor;
        campaign.Headline = headline;
        campaign.Description = description ?? string.Empty;
        campaign.ImageUrl = imageUrl;
        campaign.TargetUrl = targetUrl;
        campaign.TargetLocationsJson = JsonSerializer.Serialize(locations, JsonOptions);
        campaign.TargetStates = stateFilter;
        campaign.StartsAt = start;
        campaign.EndsAt = end;
        campaign.IsActive = input.IsActive;
        campaign.UpdatedBy = ActorEmail(user);
        await context.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync(new AdminAuditRecord(
            AdminAuditActions.LocalAdCampaignSaved,
            Actor(user),
            AdminAuditActions.LocalAdCampaignTarget,
            campaign.Id.Value.ToString(),
            campaign.SponsorName,
            $"Campanha {(campaign.IsActive ? "ativada" : "pausada")}; {locations.Count} localidades"), cancellationToken);
        return RequestResult.Success();
    }

    public async Task<RequestResult> DeleteAsync(
        Guid id,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(user, cancellationToken))
            return RequestResult.WithDomainError("Você não tem acesso à publicidade local.");

        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        var campaign = await context.LocalAdCampaigns.FirstOrDefaultAsync(
            item => item.Id == Id.Create(id) && !item.IsDeleted, cancellationToken);
        if (campaign is null) return RequestResult.WithDomainError("Campanha não encontrada.");

        campaign.IsActive = false;
        campaign.IsDeleted = true;
        campaign.DeletedAt = _time.GetUtcNow().UtcDateTime;
        campaign.DeletedBy = ActorEmail(user);
        await context.SaveChangesAsync(cancellationToken);
        await _audit.RecordAsync(new AdminAuditRecord(
            AdminAuditActions.LocalAdCampaignDeleted, Actor(user), AdminAuditActions.LocalAdCampaignTarget, id.ToString(), campaign.SponsorName), cancellationToken);
        return RequestResult.Success();
    }

    /// <summary>Retorna um anúncio elegível e conta uma impressão. Sem cidade ou campanha compatível não exibe nada.</summary>
    public async Task<LocalAdPlacement?> GetForJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!await _flags.IsEnabledAsync(FeatureName, null, cancellationToken)) return null;

        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        var job = await context.Jobs.AsNoTracking()
            .Where(item => item.Id == Id.Create(jobId) && !item.IsDeleted && item.Status == EJobStatus.Active)
            .Select(item => new { City = item.Location!.City, State = item.Location.State })
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null || string.IsNullOrWhiteSpace(job.City) || string.IsNullOrWhiteSpace(job.State)) return null;

        var city = Normalize(job.City);
        var state = job.State.Trim().ToUpperInvariant();
        var now = _time.GetUtcNow().UtcDateTime;
        var candidates = await context.LocalAdCampaigns.AsNoTracking()
            .Where(campaign => !campaign.IsDeleted && campaign.IsActive
                && campaign.StartsAt <= now && campaign.EndsAt >= now
                && campaign.TargetStates.Contains($"|{state}|"))
            .OrderBy(campaign => campaign.ImpressionCount)
            .ThenBy(campaign => campaign.CreatedAt)
            .Take(MaxCampaignsPerState)
            .ToListAsync(cancellationToken);

        var chosen = candidates.FirstOrDefault(campaign => HasLocation(campaign.TargetLocationsJson, city, state));
        if (chosen is null) return null;

        return new LocalAdPlacement(
            chosen.Id.Value,
            chosen.SponsorName,
            chosen.Headline,
            chosen.Description,
            chosen.ImageUrl,
            $"/ads/{chosen.Id.Value:D}/click");
    }

    /// <summary>Conta a impressão depois do primeiro render interativo, evitando duplicar a visita do prerender SSR.</summary>
    public async Task RegisterImpressionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await _flags.IsEnabledAsync(FeatureName, null, cancellationToken)) return;
        var now = _time.GetUtcNow().UtcDateTime;
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        await context.LocalAdCampaigns
            .Where(item => item.Id == Id.Create(id) && !item.IsDeleted && item.IsActive
                && item.StartsAt <= now && item.EndsAt >= now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ImpressionCount, item => item.ImpressionCount + 1), cancellationToken);
    }

    public async Task<string?> RegisterClickAndGetTargetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await _flags.IsEnabledAsync(FeatureName, null, cancellationToken)) return null;
        var now = _time.GetUtcNow().UtcDateTime;
        await using var context = await _contexts.CreateDbContextAsync(cancellationToken);
        var campaign = await context.LocalAdCampaigns.AsNoTracking()
            .Where(item => item.Id == Id.Create(id) && !item.IsDeleted && item.IsActive
                && item.StartsAt <= now && item.EndsAt >= now)
            .Select(item => new { item.TargetUrl })
            .FirstOrDefaultAsync(cancellationToken);
        if (campaign is null) return null;

        var updated = await context.LocalAdCampaigns
            .Where(item => item.Id == Id.Create(id) && !item.IsDeleted && item.IsActive
                && item.StartsAt <= now && item.EndsAt >= now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ClickCount, item => item.ClickCount + 1), cancellationToken);
        return updated == 0 ? null : campaign.TargetUrl;
    }

    private async Task<bool> CanManageAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
        user.IsInRole("Admin") && await _flags.IsEnabledAsync(FeatureName, user, cancellationToken);

    private static IReadOnlyList<TargetLocation> ParseLocations(string input, out string? error)
    {
        error = null;
        var locations = new List<TargetLocation>();
        foreach (var line in input.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = line.Split(',', 2, StringSplitOptions.TrimEntries);
            if (pieces.Length != 2 || pieces[0].Length is 0 or > 100 || pieces[1].Length != 2)
            {
                error = $"Localidade inválida: “{line}”. Use o formato Cidade, UF (ex.: Curitiba, PR).";
                return [];
            }

            var city = CanonicalCity(pieces[0]);
            var state = pieces[1].ToUpperInvariant();
            if (city is null || (state is not "PR" and not "SC"))
            {
                error = $"A localidade “{line}” está fora das regiões atendidas.";
                return [];
            }
            if (!locations.Any(item => Normalize(item.City) == Normalize(city) && item.State == state))
                locations.Add(new TargetLocation(city, state));
        }
        return locations;
    }

    private static string? CanonicalCity(string city)
    {
        var known = RegionalCities.Values.SelectMany(items => items).FirstOrDefault(item => Normalize(item) == Normalize(city));
        return known;
    }

    private static bool HasLocation(string json, string city, string state)
    {
        try
        {
            return (JsonSerializer.Deserialize<List<TargetLocation>>(json, JsonOptions) ?? [])
                .Any(location => location.State == state && Normalize(location.City) == city);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static LocalAdCampaignRow ToRow(LocalAdCampaign campaign) => new(
        campaign.Id.Value,
        campaign.SponsorName,
        campaign.Headline,
        campaign.Description,
        campaign.ImageUrl,
        campaign.TargetUrl,
        string.Join(Environment.NewLine, JsonSerializer.Deserialize<List<TargetLocation>>(campaign.TargetLocationsJson, JsonOptions)
            ?.Select(location => $"{location.City}, {location.State}") ?? []),
        campaign.StartsAt,
        campaign.EndsAt,
        campaign.IsActive,
        campaign.ImpressionCount,
        campaign.ClickCount);

    private static bool IsHttpsUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);

    private static string Normalize(string value) => string.Concat(value.Trim().Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
        .ToLowerInvariant();

    private static string ActorEmail(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email) ?? user.Identity?.Name ?? string.Empty;

    private static AdminActor Actor(ClaimsPrincipal user) => new(
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
        ActorEmail(user));

    private sealed record TargetLocation(string City, string State);

    private static readonly IReadOnlyDictionary<string, string[]> RegionalCities = new Dictionary<string, string[]>
    {
        ["PR"] = ["Ponta Grossa", "Carambeí", "Castro", "Palmeira", "Porto Amazonas", "Ipiranga", "Curitiba", "Almirante Tamandaré", "Araucária", "Balsa Nova", "Campina Grande do Sul", "Campo Largo", "Campo Magro", "Colombo", "Contenda", "Fazenda Rio Grande", "Mandirituba", "Pinhais", "Piraquara", "Quatro Barras", "Quitandinha", "São José dos Pinhais", "Tijucas do Sul"],
        ["SC"] = ["Joinville", "Araquari", "Balneário Barra do Sul", "Garuva", "Guaramirim", "Jaraguá do Sul", "São Francisco do Sul", "Schroeder", "Itajaí", "Navegantes", "Balneário Camboriú", "Camboriú", "Penha", "Balneário Piçarras", "Ilhota", "Itapema", "Porto Belo", "Brusque", "Gaspar", "Tijucas", "Luiz Alves"]
    };
}
