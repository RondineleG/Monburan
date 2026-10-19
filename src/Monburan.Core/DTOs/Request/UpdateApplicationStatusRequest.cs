using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Request;

public class UpdateApplicationStatusRequest
{
    public Guid ApplicationId { get; set; }
    public Guid AdvertiserId { get; set; }
    public EApplicationStatus NewStatus { get; set; }
    public string? Notes { get; set; }

    /// <summary>Quem alterou a etapa (nome ou email), guardado no histórico.</summary>
    public string? Responsible { get; set; }
}
