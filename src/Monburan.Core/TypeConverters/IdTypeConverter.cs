using Monburan.Core.ValueObjects;

using System.ComponentModel;

namespace Monburan.Core.TypeConverters;

[TypeConverter(typeof(IdTypeConverter))]
public class IdTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) ||
               sourceType == typeof(Guid) ||
               base.CanConvertFrom(context, sourceType);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) ||
               destinationType == typeof(Guid) ||
               base.CanConvertTo(context, destinationType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return value switch
        {
            string stringValue when !string.IsNullOrWhiteSpace(stringValue) => Id.TryCreate(stringValue, out var id) ? id : null,
            Guid guidValue when guidValue != Guid.Empty => Id.TryCreate(guidValue, out Id? id) ? id : null,
            null => null,
            _ => base.ConvertFrom(context, culture, value),
        };
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is Id id)
        {
            if (destinationType == typeof(string))
                return id.ToString();
            if (destinationType == typeof(Guid))
                return id.Value;
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool IsValid(ITypeDescriptorContext? context, object? value)
    {
        if (value == null) return true;

        return value switch
        {
            string str => Id.TryCreate(str, out _),
            Guid guid => Id.TryCreate(guid, out _),
            Id => true,
            _ => false
        };
    }
}