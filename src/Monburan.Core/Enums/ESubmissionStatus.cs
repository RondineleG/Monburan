namespace Monburan.Core.Enums;

/// <summary>Situação de uma sugestão anônima de vaga. Só Approved gera vaga publicada, e só após decisão humana.</summary>
public enum ESubmissionStatus
{
    Pending,
    NeedsMoreInformation,
    Approved,
    Rejected,
    Duplicate
}
