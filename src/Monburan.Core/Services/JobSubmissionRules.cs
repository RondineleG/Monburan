using Monburan.Core.Enums;

namespace Monburan.Core.Services;

/// <summary>Regras de negócio das sugestões de vaga: transições de situação e validação da evidência.</summary>
public static class JobSubmissionRules
{
    public const int MinEvidences = 1;
    public const int MaxEvidences = 5;
    public const long MaxEvidenceBytes = 5L * 1024 * 1024;
    public const long MaxTotalBytes = 15L * 1024 * 1024;

    public static readonly IReadOnlyList<string> AllowedImageTypes = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Uma sugestão decidida (aprovada, rejeitada ou duplicada) não volta a ser decidida.</summary>
    public static bool CanTransition(ESubmissionStatus from, ESubmissionStatus to) => from switch
    {
        ESubmissionStatus.Pending => to is ESubmissionStatus.NeedsMoreInformation or ESubmissionStatus.Approved or ESubmissionStatus.Rejected or ESubmissionStatus.Duplicate,
        ESubmissionStatus.NeedsMoreInformation => to is ESubmissionStatus.Pending or ESubmissionStatus.Approved or ESubmissionStatus.Rejected or ESubmissionStatus.Duplicate,
        _ => false
    };

    public static ESubmissionStatus StatusFor(ESubmissionDecision decision) => decision switch
    {
        ESubmissionDecision.Approve => ESubmissionStatus.Approved,
        ESubmissionDecision.Reject => ESubmissionStatus.Rejected,
        ESubmissionDecision.Duplicate => ESubmissionStatus.Duplicate,
        _ => ESubmissionStatus.NeedsMoreInformation
    };

    /// <summary>Confere pelos bytes (assinatura) que o conteúdo é do tipo declarado, sem confiar no nome do arquivo.</summary>
    public static bool HasImageSignature(ReadOnlySpan<byte> content, string contentType) => contentType switch
    {
        "image/jpeg" => content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
        "image/png" => content.Length >= 8 && content[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        "image/webp" => content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content.Slice(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    public static bool IsHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
