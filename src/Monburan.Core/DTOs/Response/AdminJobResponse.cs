namespace Monburan.Core.DTOs.Response;

public class AdminJobResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public Guid? AdvertiserId { get; set; }

    public string? AdvertiserName { get; set; }

    public string? AdvertiserEmail { get; set; }

    /// <summary>Nome de <c>EJobStatus</c>.</summary>
    public string Status { get; set; } = string.Empty;

    public string? City { get; set; }

    public string? State { get; set; }

    public bool IsRemote { get; set; }

    public DateTime ExpirationDate { get; set; }

    /// <summary>A validade já passou (a vaga não pode voltar a ficar ativa sem renová-la).</summary>
    public bool IsPastExpiration { get; set; }

    public int Views { get; set; }

    public int Applications { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? ModerationReason { get; set; }

    public DateTime? ModerationDate { get; set; }

    public string? ModeratorId { get; set; }
}

public class AdminListJobsResponse
{
    public IEnumerable<AdminJobResponse> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int Size { get; set; }

    public int TotalPages => Size <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)Size);

    /// <summary>Quantidade de vagas por situação (não excluídas), para os contadores da tela.</summary>
    public IReadOnlyDictionary<string, int> StatusCounts { get; set; } = new Dictionary<string, int>();
}
