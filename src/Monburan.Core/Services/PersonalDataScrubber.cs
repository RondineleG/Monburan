using System.Text.RegularExpressions;

namespace Monburan.Core.Services;

/// <summary>
/// Troca dados pessoais soltos em texto livre (e-mail, telefone, CPF/CNPJ, CEP e links) por marcadores antes de o
/// conteúdo sair para um provedor externo. Não detecta nomes: o currículo enviado já não leva o nome da pessoa.
/// Os padrões de telefone exigem DDD entre parênteses, "+55" ou o formato de celular com separador, para não
/// confundir períodos como "2018-2021" com números de telefone.
/// </summary>
public static partial class PersonalDataScrubber
{
    public static string Scrub(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var result = Email().Replace(text, "[e-mail]");
        result = Link().Replace(result, "[link]");
        result = Cnpj().Replace(result, "[documento]");
        result = Cpf().Replace(result, "[documento]");
        result = Phone().Replace(result, "[telefone]");
        result = Cep().Replace(result, "[cep]");
        return result;
    }

    [GeneratedRegex(@"[\p{L}\p{N}._%+\-]+@[\p{L}\p{N}.\-]+\.\p{L}{2,}")]
    private static partial Regex Email();

    [GeneratedRegex(@"(?:https?://|www\.)[^\s<>""']+|\b(?:[a-z0-9-]+\.)?(?:linkedin|github|gitlab|behance|instagram|facebook|twitter)\.com/\S+", RegexOptions.IgnoreCase)]
    private static partial Regex Link();

    [GeneratedRegex(@"(?<!\d)(?:\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}|\d{14})(?!\d)")]
    private static partial Regex Cnpj();

    [GeneratedRegex(@"(?<!\d)(?:\d{3}\.\d{3}\.\d{3}-\d{2}|\d{11})(?!\d)")]
    private static partial Regex Cpf();

    [GeneratedRegex(@"(?<!\d)(?:(?:\+?55[\s.-]?)?\(\d{2}\)[\s.-]?9?\d{4}[\s.-]?\d{4}|\+55[\s.-]?\d{2}[\s.-]?9?\d{4}[\s.-]?\d{4}|(?:\d{2}[\s.-])?9\d{4}[\s.-]\d{4})(?!\d)")]
    private static partial Regex Phone();

    [GeneratedRegex(@"(?<!\d)\d{5}-\d{3}(?!\d)")]
    private static partial Regex Cep();
}
