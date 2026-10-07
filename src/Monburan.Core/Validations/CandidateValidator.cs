using Monburan.Core.Entities;
using Monburan.Core.Interfaces.Validations;

using System.Text.RegularExpressions;

namespace Monburan.Core.Validations;

public class CandidateValidator : IEntityValidator<Candidate>
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(250));

    private static readonly Regex UrlRegex = new(
        @"^https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(250));

    public IValidationResult Validate(Candidate entity)
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

        // Validar data de nascimento (maior de 14 anos)
        if (!entity.DateOfBirth.HasValue)
            result.AddError("Data de nascimento é obrigatória.", "DateOfBirth");
        else if (CalculateAge(entity.DateOfBirth.Value) < 14)
            result.AddError("Candidato deve ter pelo menos 14 anos.", "DateOfBirth");

        // Validar telefone se informado (max 25 chars)
        if (!string.IsNullOrEmpty(entity.Phone) && entity.Phone.Length > 25)
            result.AddError("Telefone deve ter no máximo 25 caracteres.", "Phone");

        // Validar LinkedIn se informado (URL válida)
        if (!string.IsNullOrEmpty(entity.LinkedIn) && !UrlRegex.IsMatch(entity.LinkedIn))
            result.AddError("LinkedIn deve ser uma URL válida.", "LinkedIn");

        return result;
    }

    public IValidationResult ValidateUpdate(Candidate entity)
    {
        return Validate(entity);
    }

    private static int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.UtcNow;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age))
            age--;
        return age;
    }
}
