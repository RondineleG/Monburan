namespace Monburan.Core.DTOs.Integrations;

public class IntegrationTokenResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Valor opaco, presente somente na resposta da criação.</summary>
    public string? Token { get; set; }
}
