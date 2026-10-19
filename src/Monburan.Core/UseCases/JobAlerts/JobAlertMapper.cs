using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;

namespace Monburan.Core.UseCases.JobAlerts;

internal static class JobAlertMapper
{
    public static JobAlertResponse ToResponse(JobAlert alert) => new()
    {
        Id = alert.Id.Value,
        Name = alert.Name,
        Keywords = alert.Keywords,
        City = alert.City,
        State = alert.State,
        Mode = alert.Mode,
        Category = alert.Category,
        SalaryMin = alert.SalaryMin,
        IsActive = alert.IsActive,
        CreatedAt = alert.CreatedAt,
        LastMatchedAt = alert.LastMatchedAt
    };
}
