namespace Monburan.Web.Repositories;

/// <summary>
/// Calcula o recorte (skip/take) de uma listagem paginada.
/// <para>
/// <paramref name="size"/> em 0 significa "sem paginação": a listagem devolve até o teto
/// (<paramref name="maxSize"/>). Com tamanho maior, devolve a página pedida, limitada ao teto.
/// </para>
/// </summary>
internal static class Pagination
{
    public static (int Skip, int Take) For(int page, int size, int maxSize)
    {
        var safePage = page > 0 ? page : 1;

        if (size <= 0)
            return (0, maxSize);

        var safeSize = Math.Min(size, maxSize);
        return ((safePage - 1) * safeSize, safeSize);
    }
}
