using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

using Monburan.Core.DTOs.Response;
using Monburan.Web.Models;
using Monburan.Web.Services;

namespace Monburan.UnitTests.Services;

public class ResumeImprovementDraftServiceTests
{
    // Criptografia real: DataProtectionProvider.Create usa o keyring padrão com o
    // nome do app como discriminador — outro nome de app falha de fato ao descriptografar.
    private static IDataProtectionProvider NewRealProtection() =>
        DataProtectionProvider.Create("monburan-unit-draft");

    private sealed class FakeProtectionProvider(IDataProtectionProvider inner) : IDataProtectionProvider
    {
        public IDataProtector CreateProtector(string purpose) => inner.CreateProtector(purpose);
    }

    private sealed class FakeLocalStorage : IJSRuntime
    {
        public readonly Dictionary<string, string> Store = new();
        public bool FailSet;
        public bool FailRemove;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            object? value = identifier switch
            {
                "localStorage.getItem" => Store.GetValueOrDefault((string)args![0]!),
                "localStorage.setItem" when FailSet => throw new JSException("quota"),
                "localStorage.setItem" => SetItem((string)args![0]!, (string)args![1]!),
                "localStorage.removeItem" when FailRemove => throw new JSException("denied"),
                "localStorage.removeItem" => RemoveItem((string)args![0]!),
                _ => null
            };
            return new ValueTask<TValue>((TValue?)value!);
        }

        private object? SetItem(string key, string val) { Store[key] = val; return null; }
        private object? RemoveItem(string key) { Store.Remove(key); return null; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    private static ResumeImprovementDraft NewDraft(Guid candidateId, Guid resumeId) =>
        ResumeImprovementEditor.CreateDraft(candidateId, new ResumeResponse
        {
            Id = resumeId,
            CandidateId = candidateId,
            Title = "CV",
            Summary = "segredo"
        }, new ResumeImprovementResponse { Summary = "melhorado" });

    private static ResumeImprovementDraftService NewService(IJSRuntime js, IDataProtectionProvider? protection = null) =>
        new(js, new FakeProtectionProvider(protection ?? NewRealProtection()), NullLogger<ResumeImprovementDraftService>.Instance);

    [Fact]
    public async Task Save_StoresEncryptedPayload_NoPlaintext()
    {
        var js = new FakeLocalStorage();
        var draft = NewDraft(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(await NewService(js).SaveAsync(draft));

        var payload = Assert.Single(js.Store).Value;
        Assert.DoesNotContain("segredo", payload);
        Assert.DoesNotContain("melhorado", payload);
        Assert.DoesNotContain(draft.ResumeId.ToString(), payload);
    }

    [Fact]
    public async Task Load_RestoresDraft_SameKey()
    {
        var js = new FakeLocalStorage();
        var candidateId = Guid.NewGuid();
        var resumeId = Guid.NewGuid();
        var service = NewService(js);
        var draft = NewDraft(candidateId, resumeId);
        await service.SaveAsync(draft);

        var (outcome, found) = await service.LoadAsync(candidateId, resumeId);

        Assert.Equal(ResumeImprovementDraftLoad.Found, outcome);
        Assert.NotNull(found);
        Assert.Equal("melhorado", found!.Result!.Summary);
        Assert.Equal(resumeId, found.ResumeId);
    }

    [Fact]
    public async Task Load_WrongKeyOrCorrupt_ReturnsError()
    {
        var js = new FakeLocalStorage();
        var candidateId = Guid.NewGuid();
        var resumeId = Guid.NewGuid();
        var draft = NewDraft(candidateId, resumeId);
        var key = $"resume-improvement:{candidateId:N}:{resumeId:N}";

        // payload criptografado sob outra identidade de app (outro cofre de chaves)
        await NewService(js, DataProtectionProvider.Create("outro-app")).SaveAsync(draft);
        var (outcome, found) = await NewService(js).LoadAsync(candidateId, resumeId);
        Assert.Equal(ResumeImprovementDraftLoad.Error, outcome);
        Assert.Null(found);

        // payload corrompido (nem Base64)
        js.Store[key] = "@@@lixo@@@";
        var (corruptOutcome, corruptFound) = await NewService(js).LoadAsync(candidateId, resumeId);
        Assert.Equal(ResumeImprovementDraftLoad.Error, corruptOutcome);
        Assert.Null(corruptFound);
    }

    [Fact]
    public async Task Load_OtherResumeOrCandidate_NotFound()
    {
        var js = new FakeLocalStorage();
        var service = NewService(js);
        var candidateId = Guid.NewGuid();
        await service.SaveAsync(NewDraft(candidateId, Guid.NewGuid()));

        Assert.Equal(ResumeImprovementDraftLoad.None,
            (await service.LoadAsync(candidateId, Guid.NewGuid())).Item1);
        Assert.Equal(ResumeImprovementDraftLoad.None,
            (await service.LoadAsync(Guid.NewGuid(), Guid.NewGuid())).Item1);
    }

    [Fact]
    public async Task Save_QuotaFailure_ReturnsFalse()
    {
        var js = new FakeLocalStorage { FailSet = true };
        Assert.False(await NewService(js).SaveAsync(NewDraft(Guid.NewGuid(), Guid.NewGuid())));
    }

    [Fact]
    public async Task Delete_Failure_ReturnsFalse()
    {
        var js = new FakeLocalStorage { FailRemove = true };
        Assert.False(await NewService(js).DeleteAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task SaveAndDeleteRace_SerializedByGate_NoResurrect()
    {
        // setItem bloqueado num TaskCompletionSource: o delete espera na trava e
        // só executa depois que o save solta — não revive nem intercala escrita.
        var js = new BlockingLocalStorage();
        var service = NewService(js);
        var candidateId = Guid.NewGuid();
        var resumeId = Guid.NewGuid();

        var saveTask = service.SaveAsync(NewDraft(candidateId, resumeId));
        var deleteTask = service.DeleteAsync(candidateId, resumeId);

        await Task.Delay(200);
        Assert.Equal(0, js.RemoveCalls);
        Assert.False(deleteTask.IsCompleted);

        js.ReleaseSet();
        await Task.WhenAll(saveTask, deleteTask);

        Assert.True(await saveTask);
        Assert.True(await deleteTask);
        Assert.Equal(1, js.RemoveCalls);
        Assert.Equal(ResumeImprovementDraftLoad.None,
            (await service.LoadAsync(candidateId, resumeId)).Item1);
    }

    private sealed class BlockingLocalStorage : IJSRuntime
    {
        private readonly TaskCompletionSource _setReleased = new();
        private readonly Dictionary<string, string> _store = new();
        public int RemoveCalls;

        public void ReleaseSet() => _setReleased.TrySetResult();

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            object? value = identifier switch
            {
                "localStorage.getItem" => _store.GetValueOrDefault((string)args![0]!),
                "localStorage.setItem" => await SetItemAsync((string)args![0]!, (string)args![1]!),
                "localStorage.removeItem" => RemoveItem((string)args![0]!),
                _ => null
            };
            return (TValue?)value!;
        }

        private async Task<object?> SetItemAsync(string key, string val)
        {
            await _setReleased.Task;
            _store[key] = val;
            return null;
        }

        private object? RemoveItem(string key)
        {
            RemoveCalls++;
            _store.Remove(key);
            return null;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }
}
