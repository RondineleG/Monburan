namespace Monburan.Core.DTOs.Request;

public class ListApplicationsByJobRequest
{
    public Guid AdvertiserId { get; set; }

    public Guid? JobId { get; set; }
}
