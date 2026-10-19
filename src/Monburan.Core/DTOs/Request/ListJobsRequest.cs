namespace Monburan.Core.DTOs.Request;

public class ListJobsRequest
{
    public int Page { get; set; } = 1;
    public int Size { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public bool? IsRemote { get; set; }
    public string? Type { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? Category { get; set; }
    public string? SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
    public Guid? AdvertiserId { get; set; }

    /// <summary>Candidato autenticado (quando houver): preenche <c>IsSaved</c> nas vagas retornadas.</summary>
    public Guid? CandidateId { get; set; }

    /// <summary>Nome de <c>EContractType</c> ("Clt", "Pj"...). Valor desconhecido é ignorado.</summary>
    public string? ContractType { get; set; }

    /// <summary>Nome de <c>ESalaryPeriod</c> ("Hour", "Month"...). Valor desconhecido é ignorado.</summary>
    public string? SalaryPeriod { get; set; }

    /// <summary>Vagas publicadas nos últimos N dias; nulo/zero não filtra.</summary>
    public int? PublishedWithinDays { get; set; }
}
