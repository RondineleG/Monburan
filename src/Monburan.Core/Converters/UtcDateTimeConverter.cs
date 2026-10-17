using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Monburan.Core.Converters;

/// <summary>
/// Normaliza <see cref="DateTime"/> para UTC antes de gravar e marca como UTC ao ler.
/// Datas vindas de formulários (<c>&lt;input type="date"&gt;</c>, JSON sem offset) chegam com
/// <see cref="DateTimeKind.Unspecified"/>, e o Npgsql recusa gravá-las em
/// <c>timestamp with time zone</c> — o save lançava exceção e derrubava a tela.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        value => ToUtc(value),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }

    internal static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        // Sem fuso informado: o valor digitado é preservado como está (datas de calendário)
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter() : base(
        value => value.HasValue ? UtcDateTimeConverter.ToUtc(value.Value) : value,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value)
    {
    }
}
