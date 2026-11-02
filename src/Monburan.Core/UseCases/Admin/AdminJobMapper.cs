using Monburan.Core.DTOs.Response;
using Monburan.Core.Entities;

namespace Monburan.Core.UseCases.Admin;

internal static class AdminJobMapper
{
    public static AdminJobResponse ToResponse(Job job, DateTime today) => new()
    {
        Id = job.Id.Value,
        Title = job.Title,
        AdvertiserId = job.AdvertiserId?.Value,
        AdvertiserName = job.Advertiser?.Name,
        AdvertiserEmail = job.Advertiser?.Email,
        Status = job.Status.ToString(),
        City = job.Location?.City,
        State = job.Location?.State,
        IsRemote = job.Location?.IsRemote ?? false,
        ExpirationDate = job.ExpirationDate,
        IsPastExpiration = job.ExpirationDate.Date < today,
        Views = job.Counters?.Views ?? 0,
        Applications = job.Counters?.Applications ?? 0,
        CreatedAt = job.CreatedAt,
        ModerationReason = job.Moderation?.Reason,
        ModerationDate = job.Moderation?.ModerationDate,
        ModeratorId = job.Moderation?.ModeratorId
    };
}
