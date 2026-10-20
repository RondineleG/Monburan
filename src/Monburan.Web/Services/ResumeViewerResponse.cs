using Monburan.Core.DTOs.Response;

namespace Monburan.Web.Services;

/// <summary>
/// Currículo autorizado para leitura + os contatos que a empresa já recebeu na candidatura.
/// Não expõe data de nascimento, endereço nem situação da conta do candidato.
/// </summary>
public sealed class ResumeViewerResponse
{
    public ResumeResponse Resume { get; set; } = new();

    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? LinkedIn { get; set; }

    /// <summary>Verdadeiro quando quem lê é o próprio dono do currículo (pode editar, melhorar e excluir).</summary>
    public bool IsOwner { get; set; }
}
