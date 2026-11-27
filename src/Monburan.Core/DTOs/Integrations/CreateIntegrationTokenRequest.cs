using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.DTOs.Integrations;

public class CreateIntegrationTokenRequest
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public string[] Scopes { get; set; } = [];

    [Range(1, 90)]
    public int ExpiresInDays { get; set; } = 30;
}
