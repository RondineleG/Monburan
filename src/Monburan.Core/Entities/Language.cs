using Monburan.Core.Enums;

namespace Monburan.Core.Entities;

public class Language
{
    public ELanguageLevel Level { get; set; }

    public string Name { get; set; } = string.Empty;
}