using Monburan.Core.Entities.Base;

using System.ComponentModel.DataAnnotations;

namespace Monburan.Core.Entities;

public class TenantConfig : BaseEntity
{
    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public string? LogoUrl { get; set; }

    public string? PrimaryThemeColor { get; set; }

    public string Settings { get; internal set; } = string.Empty;

    public string? SettingsJson { get; set; }

    public T? GetSettings<T>() where T : class
    {
        if (string.IsNullOrEmpty(SettingsJson)) return null;
        return JsonSerializer.Deserialize<T>(SettingsJson);
    }

    public void SetSettings<T>(T settings) where T : class
    {
        SettingsJson = JsonSerializer.Serialize(settings);
    }
}