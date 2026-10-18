using System.Diagnostics;

using Monburan.Core.DTOs.Request;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Enums;
using Monburan.Core.Interfaces.Repositories;
using Monburan.Core.Interfaces.Services;
using Monburan.Core.Interfaces.UseCases;
using Monburan.Core.Requests;
using Monburan.Core.ValueObjects;

namespace Monburan.Core.UseCases.Ai;

/// <summary>
/// Botão "Testar" da tela do administrador: faz uma chamada mínima ao provedor (ativo ou não) e explica em português
/// o que deu certo ou errado, sem expor segredo. Não conta no limite de uso das pessoas.
/// </summary>
public class TestAiProviderUseCase : IUseCase<AiProviderActionRequest, AiProviderTestResponse>
{
    private const string Prompt = "Responda somente com a palavra: ok";

    private readonly IAiProviderRepository _repository;
    private readonly IAiSecretReader _secrets;
    private readonly IAiTextClient _ai;

    public TestAiProviderUseCase(IAiProviderRepository repository, IAiSecretReader secrets, IAiTextClient ai)
    {
        _repository = repository;
        _secrets = secrets;
        _ai = ai;
    }

    public Task<RequestResult<AiProviderTestResponse>> ExecuteAsync(AiProviderActionRequest request) =>
        ExecuteAsync(request, CancellationToken.None);

    public async Task<RequestResult<AiProviderTestResponse>> ExecuteAsync(
        AiProviderActionRequest request, CancellationToken cancellationToken)
    {
        var found = await _repository.GetByIdAsync(Id.Create(request.Id));
        if (found.Status != EResultStatus.Success || found.Data is null)
            return RequestResult<AiProviderTestResponse>.EntityNotFound("AiProvider", request.Id, "Provedor não encontrado.");

        var provider = found.Data;
        if (!_secrets.IsSet(provider.ApiKeyEnvVar))
        {
            return RequestResult<AiProviderTestResponse>.Success(new AiProviderTestResponse
            {
                Success = false,
                Message = $"A variável {provider.ApiKeyEnvVar} não está definida neste servidor. Defina-a (veja docs/IA-PROVEDORES.md) e reinicie o app."
            });
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var completion = await _ai.CompleteAsync(
                request.Id,
                new AiCompletionRequest([new AiMessage("user", Prompt)]),
                cancellationToken);

            return RequestResult<AiProviderTestResponse>.Success(new AiProviderTestResponse
            {
                Success = true,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Reply = Excerpt(completion.Content),
                Message = "O provedor respondeu."
            });
        }
        catch (AiClientException exception)
        {
            return RequestResult<AiProviderTestResponse>.Success(new AiProviderTestResponse
            {
                Success = false,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Message = Describe(exception)
            });
        }
    }

    // Para o administrador: diz o que conferir. A mensagem técnica do cliente não leva segredo (só status e destino).
    private static string Describe(AiClientException exception) => exception.Kind switch
    {
        AiFailureKind.NotConfigured => "O provedor não está configurado: confira o endereço, o modelo e a variável da chave.",
        AiFailureKind.BlockedDestination => "O endereço aponta para uma rede interna ou reservada e foi bloqueado por segurança.",
        AiFailureKind.Timeout => "O provedor não respondeu a tempo. Confira o endereço ou aumente o tempo limite.",
        AiFailureKind.RateLimited => "O provedor limitou as chamadas (429). Tente de novo em instantes ou confira o plano.",
        AiFailureKind.Unauthorized => "O provedor recusou a chave (401/403). Confira o valor da variável no servidor.",
        AiFailureKind.InvalidResponse => "A resposta não veio no formato esperado. Confira o protocolo e o endereço escolhidos.",
        AiFailureKind.Truncated => "A resposta foi cortada pelo limite de tokens. Aumente o limite de tokens do provedor.",
        _ => $"O provedor falhou: {exception.Message}"
    };

    private static string Excerpt(string content)
    {
        var flat = string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= 80 ? flat : flat[..80] + "…";
    }
}
