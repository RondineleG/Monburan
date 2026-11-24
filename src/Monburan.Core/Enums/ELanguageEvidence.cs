namespace Monburan.Core.Enums;

/// <summary>Como o nível de idioma foi declarado — sempre sem veredito da plataforma.</summary>
public enum ELanguageEvidence
{
    /// <summary>Nível autodeclarado, sem documento.</summary>
    SelfDeclared,
    /// <summary>Pessoa informa ter certificado (ex.: DELE, TOEFL) — sem verificação.</summary>
    Certificate,
    /// <summary>Idioma nativo.</summary>
    Native
}
