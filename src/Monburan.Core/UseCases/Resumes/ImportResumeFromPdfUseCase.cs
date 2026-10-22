using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;

namespace Monburan.Core.UseCases.Resumes;

/// <summary>
/// Transforma o PDF enviado em um currículo estruturado: extrai o texto, tenta estruturar pela IA
/// quando a pessoa autorizou o envio ao provedor (e ela está configurada), cai para a leitura local
/// por regras nos demais casos, e grava o resultado como currículo normal — o questionário depois só
/// pergunta o que ficou faltando. O PDF de origem fica referenciado no currículo.
/// </summary>
public class ImportResumeFromPdfUseCase : IUseCase<ImportResumeRequest, ImportResumeResult>
{
    private readonly IPdfTextExtractor _extractor;
    private readonly IAiTextClient _ai;
    private readonly IAiUsageLimiter _limiter;
    private readonly CreateResumeUseCase _createResume;

    public ImportResumeFromPdfUseCase(
        IPdfTextExtractor extractor,
        IAiTextClient ai,
        IAiUsageLimiter limiter,
        CreateResumeUseCase createResume)
    {
        _extractor = extractor;
        _ai = ai;
        _limiter = limiter;
        _createResume = createResume;
    }

    public async Task<RequestResult<ImportResumeResult>> ExecuteAsync(
        ImportResumeRequest request, CancellationToken cancellationToken = default)
    {
        string text;
        try
        {
            // Streams de upload (IBrowserFile.OpenReadStream) podem não ser seekáveis; o PdfPig precisa
            // navegar no arquivo, então copiamos para a memória antes de extrair
            var buffer = new MemoryStream();
            await request.FileStream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            text = _extractor.ExtractText(buffer);
        }
        catch (PdfExtractionException)
        {
            return RequestResult<ImportResumeResult>.WithDomainError(
                "Não conseguimos ler este PDF. Verifique se ele não está protegido nem corrompido e tente outro arquivo.");
        }

        ImportedResumeDraft draft;
        if (text.Length == 0)
        {
            // PDF sem camada de texto (digitalização/foto): não há o que ler localmente nem com IA
            draft = new ImportedResumeDraft
            {
                Title = Path.GetFileNameWithoutExtension(request.FileName),
                ExtractedBy = EResumeImportMode.None
            };
        }
        else
        {
            draft = await ExtractWithAiAsync(text, request, cancellationToken)
                    ?? ResumeTextHeuristics.Parse(text, request.FileName);
        }

        draft.MissingFields = ResumeTextHeuristics.MissingFieldsOf(draft);

        var createResult = await _createResume.ExecuteAsync(ToCreateRequest(draft, request), cancellationToken);
        if (createResult.Status != EResultStatus.Success || createResult.Data is null)
            return RequestResult<ImportResumeResult>.FromResult(createResult);

        return RequestResult<ImportResumeResult>.Success(new ImportResumeResult
        {
            ResumeId = createResult.Data.Id,
            ExtractedBy = draft.ExtractedBy,
            MissingFields = draft.MissingFields,
            SectionsFound = CountSections(draft)
        });
    }

    /// <summary>Extrai pela IA quando há autorização e provedor ativo; qualquer falha devolve null para o fallback local.</summary>
    private async Task<ImportedResumeDraft?> ExtractWithAiAsync(
        string text, ImportResumeRequest request, CancellationToken cancellationToken)
    {
        if (!request.AcceptsExternalProcessing || !_ai.IsConfigured)
            return null;

        if (!_limiter.TryAcquire($"resume-import:{request.CandidateId}", out _))
            return null;

        try
        {
            var completion = await _ai.CompleteAsync(
                new AiCompletionRequest(ResumeImportPrompt.Build(text), JsonObject: true), cancellationToken);

            return ResumeImportParser.TryParse(completion.Content, out var draft) ? draft : null;
        }
        catch (AiClientException)
        {
            return null;
        }
    }

    private static CreateResumeRequest ToCreateRequest(ImportedResumeDraft draft, ImportResumeRequest request) => new()
    {
        CandidateId = request.CandidateId,
        Title = ToValidTitle(draft.Title),
        Summary = string.IsNullOrWhiteSpace(draft.Summary) ? null : draft.Summary.Trim(),
        IsActive = false,
        SourceFileName = request.FileName,
        SourceFileUrl = request.FileUrl,
        ExperienceHistory = draft.Experiences.Select(e => new ExperienceRequest
        {
            Company = e.Company,
            Position = e.Position,
            Description = e.Description,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            IsCurrent = e.IsCurrent
        }).ToList(),
        EducationHistory = draft.Education.Select(e => new EducationRequest
        {
            Institution = e.Institution,
            Course = e.Course,
            Level = e.Level,
            Status = e.Status,
            StartDate = e.StartDate,
            EndDate = e.EndDate
        }).ToList(),
        Languages = draft.Languages.Select(l => new LanguageRequest { Language = l.Language, Level = l.Level }).ToList(),
        Skills = draft.Skills.Select(s => new SkillRequest { Skill = s.Skill, Level = s.Level, YearsOfExperience = s.YearsOfExperience }).ToList()
    };

    /// <summary>
    /// O título vem do nome do arquivo ou da IA e pode fugir da faixa aceita pelo currículo (3 a 100
    /// caracteres). Fora dela, usa o título padrão ou corta, em vez de rejeitar um arquivo já enviado.
    /// </summary>
    private static string ToValidTitle(string? title)
    {
        const string fallback = "Currículo importado";
        const int maxLength = 100;

        var text = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
        if (text.Length < 3)
            return fallback;

        return text.Length <= maxLength ? text : text[..(maxLength - 1)].TrimEnd() + "…";
    }

    private static int CountSections(ImportedResumeDraft draft)
    {
        var count = 0;
        if (!string.IsNullOrWhiteSpace(draft.Summary))
            count++;
        if (draft.Experiences.Count > 0)
            count++;
        if (draft.Education.Count > 0)
            count++;
        if (draft.Languages.Count > 0)
            count++;
        if (draft.Skills.Count > 0)
            count++;
        return count;
    }
}
