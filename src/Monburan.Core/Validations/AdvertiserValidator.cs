using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Validations;

using System.Text.RegularExpressions;

namespace Monburan.Core.Validations;

public class AdvertiserValidator : IEntityValidator<Advertiser>
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(250));

    private static readonly Regex UrlRegex = new(
        @"^https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(250));

    public IValidationResult Validate(Advertiser entity)
    {
        var result = new ValidationResult();

        // Validar nome (3-100 chars)
        if (string.IsNullOrWhiteSpace(entity.Name))
            result.AddError("Nome é obrigatório.", "Name");
        else if (entity.Name.Length < 3 || entity.Name.Length > 100)
            result.AddError("Nome deve ter entre 3 e 100 caracteres.", "Name");

        // Validar email (formato e obrigatório)
        if (string.IsNullOrWhiteSpace(entity.Email))
            result.AddError("Email é obrigatório.", "Email");
        else if (!EmailRegex.IsMatch(entity.Email))
            result.AddError("Email em formato inválido.", "Email");

        // Validar telefone (obrigatório, max 25 chars)
        if (string.IsNullOrWhiteSpace(entity.Phone))
            result.AddError("Telefone é obrigatório.", "Phone");
        else if (entity.Phone.Length > 25)
            result.AddError("Telefone deve ter no máximo 25 caracteres.", "Phone");

        // Validar TaxId (CNPJ/CPF, obrigatório, max 50 chars)
        if (string.IsNullOrWhiteSpace(entity.TaxId))
            result.AddError("CNPJ/CPF é obrigatório.", "TaxId");
        else if (entity.TaxId.Length > 50)
            result.AddError("CNPJ/CPF deve ter no máximo 50 caracteres.", "TaxId");
        else if (!IsValidTaxId(entity.TaxId))
            result.AddError("CNPJ/CPF inválido.", "TaxId");

        // Validar website se informado (URL válida)
        if (!string.IsNullOrEmpty(entity.Website) && !UrlRegex.IsMatch(entity.Website))
            result.AddError("Website deve ser uma URL válida.", "Website");

        return result;
    }

    public IValidationResult ValidateUpdate(Advertiser entity)
    {
        return Validate(entity);
    }

    /// <summary>
    /// Valida TaxId detectando automaticamente se é CPF ou CNPJ baseado no tamanho.
    /// CPF: 11 dígitos | CNPJ: 14 dígitos
    /// </summary>
    private static bool IsValidTaxId(string taxId)
    {
        // Remover formatação para contagem de dígitos
        var digitsOnly = taxId.Replace(".", "").Replace("-", "").Replace("/", "").Trim();

        // Detectar tipo baseado no tamanho
        return digitsOnly.Length switch
        {
            11 => CpfValidator.IsValid(taxId),
            14 => CnpjValidator.IsValid(taxId),
            _ => false // Tamanho inválido
        };
    }
}
