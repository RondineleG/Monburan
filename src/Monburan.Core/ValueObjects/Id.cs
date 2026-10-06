using Monburan.Core.Exceptions;
using Monburan.Core.TypeConverters;
using Monburan.Core.Validations;
using Monburan.Core.ValueObjects.Base;

using System.ComponentModel;

namespace Monburan.Core.ValueObjects;

[TypeConverter(typeof(IdTypeConverter))]
public sealed class Id : ValueObject
{
    private Id(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static Id Empty => new(Guid.Empty);

    public static Id Create()
    {
        return new Id(Guid.NewGuid());
    }

    public static Id Create(Guid guid)
    {
        return TryCreate(guid, out var id) ? id! : throw new DomainException(IdResources.IdIsRequired);
    }

    public static Id Create(string value)
    {
        return TryCreate(value, out var id) ? id! : throw new DomainException(IdResources.InvalidIdFormat);
    }

    public static implicit operator Guid(Id id) => id.Value;

    public static implicit operator Id(Guid guid) => Create(guid);

    public static implicit operator Id?(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : TryCreate(value, out var id) ? id : null;

    public static implicit operator string(Id id) => id.ToString();

    public static bool operator !=(Id? left, Id? right) => !(left == right);

    public static bool operator ==(Id? left, Id? right) => left?.Equals(right) ?? right is null;

    public static bool TryCreate(string? value, out Id? id)
    {
        id = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (!Guid.TryParse(value, out var guid))
            return false;
        return TryCreate(guid, out id);
    }

    public static bool TryCreate(Guid guid, out Id? id)
    {
        if (guid == Guid.Empty)
        {
            id = null;
            return false;
        }
        id = new Id(guid);
        return true;
    }

    public int CompareTo(Id? other)
    {
        if (other is null) return 1;
        return Value.CompareTo(other.Value);
    }

    public bool Equals(Id? other)
    {
        return other is not null && Value.Equals(other.Value);
    }

    public override bool Equals(object? obj)
    {
        return obj is Id other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString() => Value.ToString("D");

    public string ToString(string format) => Value.ToString(format);

    public override ValidationResult Validate()
    {
        var validationResult = new ValidationResult();
        validationResult.AddErrorIf(() => Value == Guid.Empty, IdResources.IdIsRequired, nameof(Id));
        return validationResult;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}