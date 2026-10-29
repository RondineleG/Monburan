using System.Security.Cryptography;
using System.Text;
using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;

namespace Monburan.Core.Services;

/// <summary>Token, hash e projeção pública dos links de currículo.</summary>
public static class ResumeShareRules
{
    public const int MaxActiveSharesPerResume = 5;

    /// <summary>Token de 256 bits, gerado com o gerador criptográfico e codificado em base64url (sem caracteres especiais).</summary>
    public static string NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>
    /// Monta a visão pública: só os campos que o candidato marcou. Não há caminho para o PDF original, o identificador
    /// interno nem dados de contato.
    /// </summary>
    public static PublicResumeView Project(Resume resume, ResumeShare share) => new()
    {
        Title = resume.Title,
        Name = share.ShowName ? resume.Candidate?.Name : null,
        Summary = share.ShowSummary ? resume.Summary : null,
        Experiences = share.ShowExperience
            ? resume.ExperienceHistory.Select(e => new PublicExperience(e.Position, e.Company, e.StartDate, e.EndDate, e.IsCurrent, e.Description)).ToList()
            : [],
        Education = share.ShowEducation
            ? resume.EducationHistory.Select(e => new PublicEducation(e.Course, e.Institution, e.Level.ToString(), e.Status.ToString(), e.StartDate, e.EndDate)).ToList()
            : [],
        Skills = share.ShowSkills
            ? resume.Skills.Select(s => s.Skill).ToList()
            : [],
        Languages = share.ShowLanguages
            ? resume.Languages.Select(l => l.Name).ToList()
            : []
    };
}
