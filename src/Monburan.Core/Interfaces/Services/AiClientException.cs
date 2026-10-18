namespace Monburan.Core.Interfaces.Services;

/// <summary>
/// Falha previsível ao falar com o provedor de IA. A mensagem é técnica (vai para o log); quem chama escolhe o
/// texto mostrado à pessoa a partir de <see cref="Kind"/>.
/// </summary>
public sealed class AiClientException : Exception
{
    public AiClientException(AiFailureKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public AiFailureKind Kind { get; }
}
