using System.ComponentModel.DataAnnotations;
using Monburan.Core.Entities.Base;

namespace Monburan.Core.Entities;

/// <summary>Anúncio local exibido no detalhe de vagas da cidade e estado segmentados.</summary>
public sealed class LocalAdCampaign : BaseEntity
{
    [Required, MaxLength(120)]
    public string SponsorName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Headline { get; set; } = string.Empty;

    [MaxLength(280)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    [Required, MaxLength(1000)]
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>Localidades no formato JSON [{"city":"Curitiba","state":"PR"}].</summary>
    [Required, MaxLength(4000)]
    public string TargetLocationsJson { get; set; } = "[]";

    /// <summary>Filtro indexável para não carregar campanhas de outros estados.</summary>
    [Required, MaxLength(10)]
    public string TargetStates { get; set; } = "||";

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public bool IsActive { get; set; }

    public int ImpressionCount { get; set; }

    public int ClickCount { get; set; }
}
