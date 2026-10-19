namespace Monburan.Core.DTOs.Response;

/// <summary>Progresso do candidato: completude do perfil, candidaturas por situação, vagas salvas e alertas ativos.</summary>
public class CandidateProgressResponse
{
    public int CompletenessPercent { get; set; }

    public List<CandidateProgressChecklistItem> Checklist { get; set; } = [];

    public CandidateApplicationCounts Applications { get; set; } = new();

    public int SavedJobs { get; set; }

    public int ActiveAlerts { get; set; }
}

public class CandidateProgressChecklistItem
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public bool Done { get; set; }

    public string ActionUrl { get; set; } = string.Empty;
}

public class CandidateApplicationCounts
{
    public int Total { get; set; }

    public int Sent { get; set; }

    public int Viewed { get; set; }

    public int Selected { get; set; }

    public int Rejected { get; set; }

    public int Archived { get; set; }
}
