using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Resumes;

/// <summary>
/// Gera, com IA, sugestões de melhoria para um currículo do próprio candidato. O envio do conteúdo ao provedor
/// exige autorização expressa a cada uso, o conteúdo sai sem dados pessoais e há um limite de usos por pessoa.
/// O recurso é exclusivo de planos de candidato com assistente de IA (Premium). Nada é gravado: o resultado
/// só existe na resposta.
/// </summary>
public class ImproveResumeUseCase : IUseCase<ImproveResumeRequest, ResumeImprovementResponse>
{
    private readonly GetResumeByIdUseCase _getResume;
    private readonly IAiTextClient _ai;
    private readonly IAiUsageLimiter _limiter;
    private readonly ISubscriptionRepository _subscriptions;

    public ImproveResumeUseCase(GetResumeByIdUseCase getResume, IAiTextClient ai, IAiUsageLimiter limiter, ISubscriptionRepository subscriptions)
    {
        _getResume = getResume;
        _ai = ai;
        _limiter = limiter;
        _subscriptions = subscriptions;
    }

    public async Task<RequestResult<ResumeImprovementResponse>> ExecuteAsync(
        ImproveResumeRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.AcceptsExternalProcessing)
        {
            return RequestResult<ResumeImprovementResponse>.WithValidationError(
                "É preciso autorizar o envio do conteúdo profissional do currículo ao provedor de IA.",
                nameof(request.AcceptsExternalProcessing));
        }

        if (!_ai.IsConfigured)
            return RequestResult<ResumeImprovementResponse>.WithDomainError("As sugestões com IA ainda não estão ativas neste ambiente.");

        var found = await _getResume.ExecuteAsync(request.ResumeId, cancellationToken);
        if (found.Status != EResultStatus.Success || found.Data is null)
            return RequestResult<ResumeImprovementResponse>.FromResult(found);

        // Currículo de outra pessoa se comporta como inexistente: não confirma que ele existe
        var resume = found.Data;
        if (resume.CandidateId != request.CandidateId)
            return RequestResult<ResumeImprovementResponse>.EntityNotFound("Resume", request.ResumeId, "Currículo não encontrado.");

        // O assistente é benefício Premium: só planos de candidato com HasAiAssistant liberam o recurso
        var subscription = await _subscriptions.GetActiveByCandidateIdAsync(Id.Create(request.CandidateId), cancellationToken);
        if (subscription.Status != EResultStatus.Success)
            return RequestResult<ResumeImprovementResponse>.FromResult(subscription);
        if (subscription.Data?.Plan?.HasAiAssistant != true)
            return RequestResult<ResumeImprovementResponse>.WithDomainError(
                "As sugestões de melhoria com IA são um benefício do plano Premium.");

        if (!ResumeAiPrompt.HasContent(resume))
        {
            return RequestResult<ResumeImprovementResponse>.WithDomainError(
                "Preencha o resumo, uma experiência, a formação ou as habilidades do currículo para receber sugestões.");
        }

        if (!_limiter.TryAcquire($"resume-ai:{request.CandidateId}", out var retryAfter))
        {
            var minutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));
            return RequestResult<ResumeImprovementResponse>.WithDomainError(
                $"Você já gerou várias sugestões em pouco tempo. Tente de novo em {minutes} {(minutes == 1 ? "minuto" : "minutos")}.");
        }

        AiCompletion completion;
        try
        {
            completion = await _ai.CompleteAsync(new AiCompletionRequest(ResumeAiPrompt.Build(resume), JsonObject: true), cancellationToken);
        }
        catch (AiClientException exception)
        {
            return RequestResult<ResumeImprovementResponse>.WithDomainError(MessageFor(exception.Kind));
        }

        if (!ResumeImprovementParser.TryParse(completion.Content, out var improvement) || improvement is null)
            return RequestResult<ResumeImprovementResponse>.WithDomainError(MessageFor(AiFailureKind.InvalidResponse));

        improvement.Model = completion.Model ?? _ai.ModelName;
        improvement.GeneratedAt = DateTime.UtcNow;
        return RequestResult<ResumeImprovementResponse>.Success(improvement);
    }

    // O detalhe técnico (status, corpo da resposta) fica no log do cliente; a pessoa vê só o que pode fazer
    private static string MessageFor(AiFailureKind kind) => kind switch
    {
        AiFailureKind.NotConfigured => "As sugestões com IA ainda não estão ativas neste ambiente.",
        AiFailureKind.Timeout => "A IA demorou demais para responder. Tente novamente em instantes.",
        AiFailureKind.RateLimited => "O serviço de IA está muito ocupado agora. Tente novamente em alguns minutos.",
        _ => "Não foi possível gerar as sugestões agora. Tente novamente em instantes."
    };
}
