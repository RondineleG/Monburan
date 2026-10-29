using Monburan.Core.Entities;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;
using Monburan.Core.DTOs.Response;

namespace Monburan.Core.UseCases.ResumeShares;

/// <summary>Campos que o candidato escolhe mostrar no link de um currículo.</summary>
public sealed record ResumeShareOptions(
    string? Label,
    bool ShowName,
    bool ShowSummary,
    bool ShowExperience,
    bool ShowEducation,
    bool ShowSkills,
    bool ShowLanguages,
    DateTime? ExpiresAt);

/// <summary>Resultado da criação: o token aparece só aqui. Depois, só o hash fica guardado.</summary>
public sealed record CreatedResumeShare(Guid ShareId, string Token);

/// <summary>Situação de um link, para o candidato saber quais estão ativos.</summary>
public sealed record ResumeShareSummary(Guid Id, string? Label, string Status, DateTime CreatedAt, DateTime? ExpiresAt, DateTime? RevokedAt);

/// <summary>Cria um link público para um currículo do próprio candidato.</summary>
public sealed class CreateResumeShareUseCase
{
    private readonly IResumeRepository _resumes;
    private readonly IResumeShareRepository _shares;
    private readonly TimeProvider _time;

    public CreateResumeShareUseCase(IResumeRepository resumes, IResumeShareRepository shares, TimeProvider time)
    {
        _resumes = resumes;
        _shares = shares;
        _time = time;
    }

    public async Task<RequestResult<CreatedResumeShare>> ExecuteAsync(Guid candidateId, Guid resumeId, ResumeShareOptions options, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;

        var resume = await OwnedResumeAsync(candidateId, resumeId, cancellationToken);
        if (resume is null)
            return RequestResult<CreatedResumeShare>.EntityNotFound("Resume", resumeId, "Currículo não encontrado.");

        if (options.ExpiresAt is { } expires && (expires <= now || expires > now.AddYears(1)))
            return RequestResult<CreatedResumeShare>.WithValidationError("A validade do link deve ser futura e de no máximo 1 ano.", "ExpiresAt");

        var existing = await _shares.ListByResumeAsync(resume.Id, cancellationToken);
        if (existing.Status != EResultStatus.Success)
            return RequestResult<CreatedResumeShare>.FromResult(existing);
        if (existing.Data!.Count(share => share.IsAvailableAt(now)) >= ResumeShareRules.MaxActiveSharesPerResume)
            return RequestResult<CreatedResumeShare>.WithDomainError($"Este currículo já tem {ResumeShareRules.MaxActiveSharesPerResume} links ativos. Revogue um antes de criar outro.");

        var token = ResumeShareRules.NewToken();
        var share = new ResumeShare
        {
            Id = Id.Create(Guid.NewGuid()),
            ResumeId = resume.Id,
            TokenHash = ResumeShareRules.Hash(token),
            Label = string.IsNullOrWhiteSpace(options.Label) ? null : options.Label.Trim(),
            ShowName = options.ShowName,
            ShowSummary = options.ShowSummary,
            ShowExperience = options.ShowExperience,
            ShowEducation = options.ShowEducation,
            ShowSkills = options.ShowSkills,
            ShowLanguages = options.ShowLanguages,
            ExpiresAt = options.ExpiresAt,
            TenantId = "default"
        };

        var created = await _shares.CreateAsync(share, cancellationToken);
        if (created.Status != EResultStatus.Success)
            return RequestResult<CreatedResumeShare>.FromResult(created);

        return RequestResult<CreatedResumeShare>.Success(new CreatedResumeShare(share.Id.Value, token));
    }

    private async Task<Resume?> OwnedResumeAsync(Guid candidateId, Guid resumeId, CancellationToken cancellationToken)
    {
        var found = await _resumes.GetByIdAsync(Id.Create(resumeId), cancellationToken);
        return found.Status == EResultStatus.Success && found.Data is { } resume && resume.CandidateId.Value == candidateId
            ? resume
            : null;
    }
}

/// <summary>Revoga um link de forma imediata. O currículo continua existindo.</summary>
public sealed class RevokeResumeShareUseCase
{
    private readonly IResumeShareRepository _shares;
    private readonly TimeProvider _time;

    public RevokeResumeShareUseCase(IResumeShareRepository shares, TimeProvider time)
    {
        _shares = shares;
        _time = time;
    }

    public async Task<RequestResult<bool>> ExecuteAsync(Guid candidateId, Guid shareId, CancellationToken cancellationToken = default)
    {
        var found = await _shares.GetByIdAsync(Id.Create(shareId), cancellationToken);
        if (found.Status != EResultStatus.Success || found.Data?.Resume is null || found.Data.Resume.CandidateId.Value != candidateId)
            return RequestResult<bool>.EntityNotFound("ResumeShare", shareId, "Link não encontrado.");

        var share = found.Data;
        if (share.RevokedAt is not null)
            return RequestResult<bool>.Success(true);

        share.RevokedAt = _time.GetUtcNow().UtcDateTime;
        var updated = await _shares.UpdateAsync(share, cancellationToken);
        return updated.Status == EResultStatus.Success ? RequestResult<bool>.Success(true) : RequestResult<bool>.FromResult(updated);
    }
}

/// <summary>Lista os links de um currículo do candidato, com a situação de cada um.</summary>
public sealed class ListResumeSharesUseCase
{
    private readonly IResumeRepository _resumes;
    private readonly IResumeShareRepository _shares;
    private readonly TimeProvider _time;

    public ListResumeSharesUseCase(IResumeRepository resumes, IResumeShareRepository shares, TimeProvider time)
    {
        _resumes = resumes;
        _shares = shares;
        _time = time;
    }

    public async Task<RequestResult<IReadOnlyList<ResumeShareSummary>>> ExecuteAsync(Guid candidateId, Guid resumeId, CancellationToken cancellationToken = default)
    {
        var found = await _resumes.GetByIdAsync(Id.Create(resumeId), cancellationToken);
        if (found.Status != EResultStatus.Success || found.Data is null || found.Data.CandidateId.Value != candidateId)
            return RequestResult<IReadOnlyList<ResumeShareSummary>>.EntityNotFound("Resume", resumeId, "Currículo não encontrado.");

        var shares = await _shares.ListByResumeAsync(found.Data.Id, cancellationToken);
        if (shares.Status != EResultStatus.Success)
            return RequestResult<IReadOnlyList<ResumeShareSummary>>.FromResult(shares);

        var now = _time.GetUtcNow().UtcDateTime;
        var list = shares.Data!
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ResumeShareSummary(s.Id.Value, s.Label, Status(s, now), s.CreatedAt, s.ExpiresAt, s.RevokedAt))
            .ToList();
        return RequestResult<IReadOnlyList<ResumeShareSummary>>.Success(list);
    }

    private static string Status(ResumeShare share, DateTime now) =>
        share.RevokedAt is not null ? "Revogado"
        : share.ExpiresAt is { } expires && expires <= now ? "Expirado"
        : "Ativo";
}

/// <summary>Abre o link público. Qualquer link inválido, revogado ou expirado responde da mesma forma.</summary>
public sealed class GetPublicResumeUseCase
{
    private readonly IResumeShareRepository _shares;
    private readonly TimeProvider _time;

    public GetPublicResumeUseCase(IResumeShareRepository shares, TimeProvider time)
    {
        _shares = shares;
        _time = time;
    }

    public async Task<RequestResult<PublicResumeView>> ExecuteAsync(string? token, CancellationToken cancellationToken = default)
    {
        const string unavailable = "Este link não está disponível. Ele pode ter sido desativado ou expirado.";
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
            return RequestResult<PublicResumeView>.EntityNotFound("ResumeShare", "token", unavailable);

        var found = await _shares.GetByTokenHashAsync(ResumeShareRules.Hash(token), cancellationToken);
        if (found.Status != EResultStatus.Success || found.Data is null || found.Data.Resume is null)
            return RequestResult<PublicResumeView>.EntityNotFound("ResumeShare", "token", unavailable);

        var share = found.Data;
        if (!share.IsAvailableAt(_time.GetUtcNow().UtcDateTime))
            return RequestResult<PublicResumeView>.EntityNotFound("ResumeShare", "token", unavailable);

        return RequestResult<PublicResumeView>.Success(ResumeShareRules.Project(share.Resume!, share));
    }
}
