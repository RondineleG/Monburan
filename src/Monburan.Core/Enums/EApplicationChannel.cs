namespace Monburan.Core.Enums;

/// <summary>Canal pelo qual a pessoa se candidata. Vagas diretas usam InternalPlatform; vagas externas usam um dos demais.</summary>
public enum EApplicationChannel
{
    InternalPlatform,
    ExternalUrl,
    WhatsApp,
    Email,
    OnSite
}
