using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;
using Monburan.Core.Services;

namespace Monburan.Core.Mappers;

/// <summary>
/// Mapeamento único de <see cref="Job"/> para <see cref="JobResponse"/>. Todo caminho que devolve uma vaga
/// (detalhe, listagem, busca, criação, edição, situação e painel) passa por aqui, então a resposta é sempre a
/// mesma: nome da empresa, logo e motivo da moderação não dependem da operação que originou a vaga.
/// </summary>
public static class JobResponseMapper
{
    /// <summary>Converte a vaga na resposta da API.</summary>
    /// <param name="job">Vaga a converter; a navegação <c>Advertiser</c> pode não estar carregada.</param>
    /// <param name="isSaved">Verdadeiro quando o candidato autenticado salvou a vaga.</param>
    /// <returns>Resposta da vaga, com os mesmos campos em qualquer caminho.</returns>
    /// <exception cref="ArgumentNullException">Quando <paramref name="job"/> é nulo.</exception>
    public static JobResponse ToResponse(Job job, bool isSaved = false)
    {
        ArgumentNullException.ThrowIfNull(job);

        return new JobResponse
        {
            Id = job.Id.Value,
            Title = job.Title,
            Description = job.Description,
            Requirements = job.Requirements,
            Responsibilities = job.Responsibilities,
            Benefits = job.Benefits,
            ApplicationInstructions = job.ApplicationInstructions,
            NumberOfOpenings = job.NumberOfOpenings,
            Observations = job.Observations,
            Category = job.Category,
            ContractType = job.ContractType?.ToString(),
            Tags = job.Tags,
            ExpirationDate = job.ExpirationDate,
            Status = job.Status.ToString(),
            ModerationReason = JobModeration.VisibleReason(job),
            City = job.Location?.City,
            State = job.Location?.State,
            Country = job.Location?.Country ?? "BR",
            Type = job.Location?.Type ?? "OnSite",
            IsRemote = job.Location?.IsRemote ?? false,
            SalaryMin = job.Salary?.Min,
            SalaryMax = job.Salary?.Max,
            SalaryCurrency = job.Salary?.Currency ?? "BRL",
            SalaryDisplay = job.Salary?.Display ?? true,
            SalaryPeriod = job.Salary?.Period?.ToString(),
            Views = job.Counters?.Views ?? 0,
            Applications = job.Counters?.Applications ?? 0,
            AdvertiserId = job.AdvertiserId.Value,
            AdvertiserName = job.Advertiser?.Name,
            AdvertiserLogoUrl = job.Advertiser?.LogoUrl,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
            IsSaved = isSaved
        };
    }
}
