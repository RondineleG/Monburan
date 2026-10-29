using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Monburan.Core.DTOs.Request;
using Monburan.Core.Entities;
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
/// Recebe uma sugestão anônima: valida os campos e a evidência, grava as imagens em armazenamento privado e registra a
/// sugestão como pendente. Não cria vaga: a publicação depende de aprovação humana.
/// </summary>
public sealed class SubmitJobSuggestionUseCase : IUseCase<SubmitJobSuggestionRequest, Guid>
{
    private const int MinTitle = 3;
    private const int MaxTitle = 200;
    private const int MaxText = 2000;
    private const int MinSourceDescription = 10;

    private readonly IJobSubmissionRepository _submissions;
    private readonly IEvidenceStorage _evidence;
    private readonly ISubmissionRateLimiter _limiter;
    private readonly ILogger<SubmitJobSuggestionUseCase> _logger;

    public SubmitJobSuggestionUseCase(
        IJobSubmissionRepository submissions,
        IEvidenceStorage evidence,
        ISubmissionRateLimiter limiter,
        ILogger<SubmitJobSuggestionUseCase> logger)
    {
        _submissions = submissions;
        _evidence = evidence;
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<RequestResult<Guid>> ExecuteAsync(SubmitJobSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        var submitterHash = Hash(string.IsNullOrWhiteSpace(request.SubmitterKey) ? "anonimo" : request.SubmitterKey);

        if (!_limiter.TryAcquire(submitterHash, out var retryAfter))
        {
            var minutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));
            return RequestResult<Guid>.WithDomainError(
                $"Você enviou muitas sugestões em pouco tempo. Tente de novo em {minutes} {(minutes == 1 ? "minuto" : "minutos")}.");
        }

        var problem = FirstProblem(request);
        if (problem is not null)
            return RequestResult<Guid>.WithValidationError(problem.Value.Message, problem.Value.Field);

        var stored = new List<string>();
        var evidences = new List<JobSubmissionEvidence>();
        var submissionId = Id.Create(Guid.NewGuid());

        try
        {
            foreach (var input in request.Evidences)
            {
                var key = await _evidence.SaveAsync(input.Content, input.ContentType, cancellationToken);
                stored.Add(key);
                evidences.Add(new JobSubmissionEvidence
                {
                    Id = Id.Create(Guid.NewGuid()),
                    SubmissionId = submissionId,
                    FileName = Path.GetFileName(input.FileName),
                    ContentType = input.ContentType,
                    Size = input.Content.LongLength,
                    Sha256 = Sha256Hex(input.Content),
                    StorageKey = key,
                    TenantId = "default"
                });
            }

            var submission = new JobSubmission
            {
                Id = submissionId,
                Title = request.Title.Trim(),
                CompanyName = Clean(request.CompanyName),
                City = Clean(request.City),
                State = Clean(request.State),
                Description = Clean(request.Description),
                SourceUrl = Clean(request.SourceUrl),
                SourceDescription = Clean(request.SourceDescription),
                ContactEmail = Clean(request.ContactEmail),
                Status = ESubmissionStatus.Pending,
                SubmitterKeyHash = submitterHash,
                TenantId = "default"
            };
            foreach (var evidence in evidences)
                submission.Evidences.Add(evidence);
            submission.History.Add(new JobSubmissionHistory
            {
                Id = Id.Create(Guid.NewGuid()),
                SubmissionId = submissionId,
                Status = ESubmissionStatus.Pending,
                TenantId = "default"
            });

            var created = await _submissions.CreateAsync(submission, cancellationToken);
            if (created.Status != EResultStatus.Success)
            {
                await DeleteStoredAsync(stored, cancellationToken);
                return RequestResult<Guid>.FromResult(created);
            }

            return RequestResult<Guid>.Success(submissionId.Value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falha ao registrar sugestão de vaga");
            await DeleteStoredAsync(stored, cancellationToken);
            return RequestResult<Guid>.WithError("Não foi possível receber a sugestão agora. Tente novamente em instantes.");
        }
    }

    private static (string Field, string Message)? FirstProblem(SubmitJobSuggestionRequest request)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        if (title.Length < MinTitle || title.Length > MaxTitle)
            return ("Title", $"Informe o cargo (entre {MinTitle} e {MaxTitle} caracteres).");

        if (Exceeds(request.CompanyName, 200) || Exceeds(request.City, 200) || Exceeds(request.State, 200))
            return ("CompanyName", "Empresa, cidade e estado devem ter no máximo 200 caracteres.");

        if (Exceeds(request.Description, MaxText))
            return ("Description", $"A descrição deve ter no máximo {MaxText} caracteres.");

        var hasUrl = !string.IsNullOrWhiteSpace(request.SourceUrl);
        if (hasUrl && !JobSubmissionRules.IsHttpUrl(request.SourceUrl))
            return ("SourceUrl", "O endereço da fonte precisa começar com http:// ou https://.");

        if (!hasUrl && (request.SourceDescription?.Trim().Length ?? 0) < MinSourceDescription)
            return ("SourceDescription", "Informe onde encontrou a vaga: o endereço da publicação ou uma descrição do local.");

        if (!string.IsNullOrWhiteSpace(request.ContactEmail) && !MailAddress.TryCreate(request.ContactEmail.Trim(), out _))
            return ("ContactEmail", "O e-mail de contato não é válido. Deixe em branco se preferir não informar.");

        var count = request.Evidences.Count;
        if (count < JobSubmissionRules.MinEvidences || count > JobSubmissionRules.MaxEvidences)
            return ("Evidences", $"Envie de {JobSubmissionRules.MinEvidences} a {JobSubmissionRules.MaxEvidences} imagens como evidência.");

        long total = 0;
        foreach (var input in request.Evidences)
        {
            if (input.Content is null || input.Content.Length == 0)
                return ("Evidences", "Uma das imagens está vazia.");
            if (input.Content.LongLength > JobSubmissionRules.MaxEvidenceBytes)
                return ("Evidences", "Cada imagem deve ter no máximo 5 MB.");
            if (!JobSubmissionRules.AllowedImageTypes.Contains(input.ContentType, StringComparer.OrdinalIgnoreCase))
                return ("Evidences", "Formato não aceito. Envie imagens JPG, PNG ou WebP.");
            if (!JobSubmissionRules.HasImageSignature(input.Content, input.ContentType.ToLowerInvariant()))
                return ("Evidences", "Uma das imagens não corresponde ao formato informado.");
            total += input.Content.LongLength;
        }

        if (total > JobSubmissionRules.MaxTotalBytes)
            return ("Evidences", "O total das imagens deve ter no máximo 15 MB.");

        return null;
    }

    private async Task DeleteStoredAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        foreach (var key in keys)
        {
            try
            {
                await _evidence.DeleteAsync(key, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Não foi possível remover uma evidência órfã");
            }
        }
    }

    private static bool Exceeds(string? value, int max) => value is not null && value.Trim().Length > max;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    internal static string Sha256Hex(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
