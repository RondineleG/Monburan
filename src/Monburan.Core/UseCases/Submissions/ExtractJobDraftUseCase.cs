using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.Storage;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.Services;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Submissions;

/// <summary>
/// Gera um rascunho dos campos da vaga a partir das imagens da sugestão, usando o provedor de IA padrão. O rascunho
/// fica guardado na sugestão para o moderador conferir; nada é publicado. O mesmo conjunto de imagens não é enviado
/// de novo enquanto o rascunho estiver válido.
/// </summary>
public sealed class ExtractJobDraftUseCase : IUseCase<ExtractJobDraftRequest, AiJobDraft>
{
    private const string Instructions =
        "Você extrai dados de uma imagem de divulgação de vaga de emprego. Responda somente com um objeto JSON. " +
        "Para cada campo use {\"value\": texto ou null, \"confidence\": \"high\" | \"medium\" | \"low\"}. " +
        "Use null quando a imagem não mostrar a informação. Nunca invente, estime ou complete com conhecimento geral. " +
        "Campos: title, companyName, city, state (sigla), description, requirements, benefits, salaryMin e salaryMax " +
        "(número, sem símbolo), salaryPeriod (hour, day, week, month ou year), deadline (AAAA-MM-DD), " +
        "channel (externalUrl, whatsapp, email ou onSite), channelDetail (endereço, número ou e-mail), " +
        "instructions (para atendimento presencial: local, dias, horário e documentos), cboCode (código CBO).";

    private readonly IJobSubmissionRepository _submissions;
    private readonly IEvidenceStorage _evidence;
    private readonly IAiTextClient _ai;
    private readonly IAiUsageLimiter _limiter;
    private readonly ILogger<ExtractJobDraftUseCase> _logger;

    public ExtractJobDraftUseCase(
        IJobSubmissionRepository submissions,
        IEvidenceStorage evidence,
        IAiTextClient ai,
        IAiUsageLimiter limiter,
        ILogger<ExtractJobDraftUseCase> logger)
    {
        _submissions = submissions;
        _evidence = evidence;
        _ai = ai;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<RequestResult<AiJobDraft>> ExecuteAsync(ExtractJobDraftRequest request, CancellationToken cancellationToken = default)
    {
        var found = await _submissions.GetByIdAsync(Id.Create(request.SubmissionId), cancellationToken);
        if (found.Status != EResultStatus.Success || found.Data is null)
            return RequestResult<AiJobDraft>.FromResult(found);

        var submission = found.Data;
        if (submission.Evidences.Count == 0)
            return RequestResult<AiJobDraft>.WithDomainError("Esta sugestão não tem imagem para analisar.");

        var fingerprint = Fingerprint(submission.Evidences.Select(e => e.Sha256));
        if (submission.AiDraftJson is not null && submission.AiDraftEvidenceHash == fingerprint)
        {
            var cached = JsonSerializer.Deserialize<AiJobDraft>(submission.AiDraftJson);
            if (cached is not null)
                return RequestResult<AiJobDraft>.Success(cached);
        }

        if (!_ai.IsConfigured)
            return RequestResult<AiJobDraft>.WithDomainError("A IA não está ativa neste ambiente. Preencha o rascunho manualmente.");

        if (!_limiter.TryAcquire($"submission-ai:{submission.Id.Value}", out var retryAfter))
        {
            var minutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));
            return RequestResult<AiJobDraft>.WithDomainError(
                $"Este rascunho já foi gerado há pouco. Tente de novo em {minutes} {(minutes == 1 ? "minuto" : "minutos")}.");
        }

        var images = new List<AiImage>();
        foreach (var evidence in submission.Evidences)
        {
            var bytes = await _evidence.ReadAsync(evidence.StorageKey, cancellationToken);
            if (bytes is null)
                return RequestResult<AiJobDraft>.WithDomainError("Uma das imagens não está mais disponível. Preencha o rascunho manualmente.");
            images.Add(new AiImage(evidence.ContentType, bytes));
        }

        var messages = new List<AiMessage>
        {
            new("system", Instructions),
            new("user", "Extraia os campos desta divulgação de vaga.", images)
        };

        AiCompletion completion;
        try
        {
            completion = await _ai.CompleteAsync(new AiCompletionRequest(messages, JsonObject: true), cancellationToken);
        }
        catch (AiClientException ex)
        {
            _logger.LogWarning(ex, "Falha da IA ao gerar rascunho de vaga ({Kind})", ex.Kind);
            return RequestResult<AiJobDraft>.WithDomainError(ex.Kind == AiFailureKind.VisionNotSupported
                ? "O provedor de IA padrão não aceita imagens. Peça ao administrador para marcá-lo como multimodal."
                : "Não foi possível gerar o rascunho agora. Preencha manualmente.");
        }

        var draft = AiJobDraftParser.Parse(completion.Content, DateOnly.FromDateTime(DateTime.UtcNow));
        if (draft is null)
            return RequestResult<AiJobDraft>.WithDomainError("A IA respondeu em um formato que não pôde ser lido. Preencha manualmente.");

        submission.AiDraftJson = JsonSerializer.Serialize(draft);
        submission.AiDraftEvidenceHash = fingerprint;
        submission.AiDraftProvider = _ai.ProviderName;
        submission.AiDraftAt = DateTime.UtcNow;

        var saved = await _submissions.UpdateAsync(submission, cancellationToken);
        if (saved.Status != EResultStatus.Success)
            return RequestResult<AiJobDraft>.FromResult(saved);

        return RequestResult<AiJobDraft>.Success(draft);
    }

    /// <summary>Impressão digital do conjunto de imagens, independente da ordem em que foram enviadas.</summary>
    private static string Fingerprint(IEnumerable<string> hashes) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", hashes.OrderBy(h => h, StringComparer.Ordinal)))))
            .ToLowerInvariant();
}
