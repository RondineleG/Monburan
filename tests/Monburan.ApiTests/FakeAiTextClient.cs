using Monburan.Core.Interfaces.Services;

namespace Monburan.ApiTests;

/// <summary>Cliente de IA falso: responde o que o teste definir e guarda o que recebeu.</summary>
public sealed class FakeAiTextClient : IAiTextClient
{
    public const string DefaultReply = """
        {"summary": "Resumo reescrito pela IA de teste.",
         "experiences": [{"company": "ACME", "position": "Analista", "suggestions": ["Frase de destaque 1.", "Frase de destaque 2."]}],
         "skillsToAdd": ["Git"], "missingSections": ["Projetos"], "tips": ["Seja objetivo."]}
        """;

    private readonly object _lock = new();
    private readonly List<AiCompletionRequest> _requests = [];
    private readonly List<Guid> _providerIds = [];

    public bool IsConfigured { get; set; } = true;

    public string ProviderName => "Provedor de teste";

    public string? ModelName => IsConfigured ? "modelo-de-teste" : null;

    public Func<AiCompletionRequest, AiCompletion> Responder { get; set; } = DefaultResponder;

    public IReadOnlyList<AiCompletionRequest> Requests
    {
        get
        {
            lock (_lock)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Provedores pedidos pelo id (teste do administrador); o padrão não aparece aqui.</summary>
    public IReadOnlyList<Guid> ProviderIds
    {
        get
        {
            lock (_lock)
            {
                return [.. _providerIds];
            }
        }
    }

    public static AiCompletion DefaultResponder(AiCompletionRequest request) =>
        new(DefaultReply, "modelo-de-teste", 100, 200);

    /// <summary>Volta ao estado inicial: configurado, resposta padrão e nenhuma chamada registrada.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _requests.Clear();
            _providerIds.Clear();
        }

        IsConfigured = true;
        Responder = DefaultResponder;
    }

    public Task<AiCompletion> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _requests.Add(request);
        }

        return Task.FromResult(Responder(request));
    }

    public Task<AiCompletion> CompleteAsync(Guid providerId, AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _providerIds.Add(providerId);
        }

        return CompleteAsync(request, cancellationToken);
    }
}
