using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;

using Monburan.Web.Models;

namespace Monburan.Web.Services;

public enum ResumeImprovementDraftLoad
{
    /// <summary>Não há rascunho salvo para este currículo neste navegador.</summary>
    None,

    /// <summary>Rascunho encontrado e íntegro.</summary>
    Found,

    /// <summary>Armazenamento indisponível, conteúdo corrompido ou rascunho de outro contexto.</summary>
    Error
}

/// <summary>
/// Persistência do rascunho de melhoria no armazenamento local do navegador, criptografado
/// com ASP.NET Core Data Protection e com chave por candidato + currículo. O conteúdo nunca
/// sai do navegador em texto claro e nunca vai para o banco de dados.
/// </summary>
public sealed class ResumeImprovementDraftService(IJSRuntime js, IDataProtectionProvider dataProtection, ILogger<ResumeImprovementDraftService> logger)
{
    private const string Purpose = "monburan.resume-improvement-draft.v1";

    private static string StorageKey(Guid candidateId, Guid resumeId) =>
        $"resume-improvement:{candidateId:N}:{resumeId:N}";

    private readonly SemaphoreSlim saveGate = new(1, 1);

    private IDataProtector CreateProtector() => dataProtection.CreateProtector(Purpose);

    /// <summary>Só chamar depois da primeira renderização (não há JS durante o pré-render).</summary>
    public async Task<(ResumeImprovementDraftLoad Outcome, ResumeImprovementDraft? Draft)> LoadAsync(
        Guid candidateId, Guid resumeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var stored = await js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, StorageKey(candidateId, resumeId));
            if (string.IsNullOrEmpty(stored))
                return (ResumeImprovementDraftLoad.None, null);

            var draft = JsonSerializer.Deserialize<ResumeImprovementDraft>(
                CreateProtector().Unprotect(Convert.FromBase64String(stored)));

            // Um rascunho só vale para o dono e o currículo da chave que o guardou
            if (draft?.ResumeSnapshot is null || draft.Result is null ||
                draft.CandidateId != candidateId || draft.ResumeId != resumeId)
            {
                logger.LogWarning("Rascunho de melhoria descartado: não corresponde à chave ou está incompleto.");
                return (ResumeImprovementDraftLoad.Error, null);
            }

            return (ResumeImprovementDraftLoad.Found, draft);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or JsonException
            or System.Security.Cryptography.CryptographicException or FormatException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Não foi possível ler o rascunho de melhoria do navegador.");
            return (ResumeImprovementDraftLoad.Error, null);
        }
    }

    /// <summary>
    /// Verdadeiro só quando a gravação no navegador confirmou — nunca fingir que salvou.
    /// Gravações são serializadas e o conteúdo é capturado dentro da trava, então um
    /// salvamento atrasado nunca pisa em cima de um estado mais novo.
    /// </summary>
    public async Task<bool> SaveAsync(ResumeImprovementDraft draft, CancellationToken cancellationToken = default)
    {
        await saveGate.WaitAsync(cancellationToken);
        try
        {
            draft.SavedAtUtc = DateTime.UtcNow;
            var payload = Convert.ToBase64String(
                CreateProtector().Protect(JsonSerializer.SerializeToUtf8Bytes(draft)));
            await js.InvokeVoidAsync("localStorage.setItem", cancellationToken, StorageKey(draft.CandidateId, draft.ResumeId), payload);
            return true;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            // Cota estourada, modo privado ou circuito caindo: o chamador avisa e segue em memória
            logger.LogWarning(ex, "Não foi possível gravar o rascunho de melhoria no navegador.");
            return false;
        }
        finally
        {
            saveGate.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid candidateId, Guid resumeId, CancellationToken cancellationToken = default)
    {
        await saveGate.WaitAsync(cancellationToken);
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", cancellationToken, StorageKey(candidateId, resumeId));
            return true;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Não foi possível remover o rascunho de melhoria do navegador.");
            return false;
        }
        finally
        {
            saveGate.Release();
        }
    }
}
