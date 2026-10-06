using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Monburan.Core.ValueObjects;

using System.Text.Json.Serialization;

namespace Monburan.Core.Converters;

public class IdValueComparer : ValueComparer<Id>
{
    public IdValueComparer() : base(
        (l, r) => (l == null && r == null) || (l != null && r != null && l.Value == r.Value),
        id => id != null ? id.Value.GetHashCode() : 0,
        id => id!)
    {
    }
}

public class IdValueConverter : ValueConverter<Id, Guid>
{
    public IdValueConverter() : base(
        id => id != null ? id.Value : Guid.Empty,
        guid => guid != Guid.Empty ? Id.Create(guid) : null!)
    {
    }
}

public class NameConverter : JsonConverter<Name>
{
    public override Name Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return Name.Create(value!);
    }

    public override void Write(Utf8JsonWriter writer, Name value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}