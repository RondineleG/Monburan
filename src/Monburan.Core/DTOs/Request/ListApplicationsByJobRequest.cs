namespace Monburan.Core.DTOs.Request;

public class ListApplicationsByJobRequest
{
    public Guid AdvertiserId { get; set; }

    public Guid? JobId { get; set; }

    /// <summary>Página (começa em 1). Com <see cref="Size"/> em 0, devolve a primeira página do teto da listagem.</summary>
    public int Page { get; set; } = 1;

    public int Size { get; set; }
}
