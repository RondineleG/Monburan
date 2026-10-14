using FluentAssertions;
using Monburan.Core.Exceptions;
using Monburan.Core.ValueObjects;
using Xunit;

namespace Monburan.UnitTests.ValueObjects;

public class IdTests
{
    [Fact]
    public void Create_WithValidGuid_ReturnsId()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = Id.Create(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void Create_WithValidString_ReturnsId()
    {
        // Arrange
        var guidString = Guid.NewGuid().ToString();

        // Act
        var id = Id.Create(guidString);

        // Assert
        id.Value.Should().Be(Guid.Parse(guidString));
    }

    [Fact]
    public void Create_WithEmptyGuid_ReturnsError()
    {
        // Act
        var result = Id.TryCreate(Guid.Empty, out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyString_ReturnsError()
    {
        // Act
        var result = Id.TryCreate(string.Empty, out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void Create_WithInvalidString_ReturnsError()
    {
        // Act
        var result = Id.TryCreate("invalid-guid", out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void Create_WithoutParameters_ReturnsNewId()
    {
        // Act
        var id = Id.Create();

        // Assert
        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void TryCreate_WithValidGuid_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var result = Id.TryCreate(guid, out var id);

        // Assert
        result.Should().BeTrue();
        id.Should().NotBeNull();
        id!.Value.Should().Be(guid);
    }

    [Fact]
    public void TryCreate_WithEmptyGuid_ReturnsFalse()
    {
        // Act
        var result = Id.TryCreate(Guid.Empty, out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void TryCreate_WithValidString_ReturnsTrue()
    {
        // Arrange
        var guidString = Guid.NewGuid().ToString();

        // Act
        var result = Id.TryCreate(guidString, out var id);

        // Assert
        result.Should().BeTrue();
        id.Should().NotBeNull();
        id!.Value.Should().Be(Guid.Parse(guidString));
    }

    [Fact]
    public void TryCreate_WithInvalidString_ReturnsFalse()
    {
        // Act
        var result = Id.TryCreate("invalid-guid", out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void TryCreate_WithEmptyString_ReturnsFalse()
    {
        // Act
        var result = Id.TryCreate(string.Empty, out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void TryCreate_WithNullString_ReturnsFalse()
    {
        // Act
        var result = Id.TryCreate(null, out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void Empty_ReturnsIdWithEmptyGuid()
    {
        // Act
        var id = Id.Empty;

        // Assert
        id.Value.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ImplicitOperator_FromGuid_ReturnsId()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        Id id = guid;

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void ImplicitOperator_FromId_ReturnsGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = Id.Create(guid);

        // Act
        Guid result = id;

        // Assert
        result.Should().Be(guid);
    }

    [Fact]
    public void ImplicitOperator_FromString_ReturnsId()
    {
        // Arrange
        var guidString = Guid.NewGuid().ToString();

        // Act
        Id? id = guidString;

        // Assert
        id.Should().NotBeNull();
        id!.Value.Should().Be(Guid.Parse(guidString));
    }

    [Fact]
    public void ImplicitOperator_FromId_ReturnsString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = Id.Create(guid);

        // Act
        string result = id;

        // Assert
        result.Should().Be(guid.ToString());
    }

    [Fact]
    public void Equality_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = Id.Create(guid);
        var id2 = Id.Create(guid);

        // Act
        var result = id1 == id2;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equality_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var id1 = Id.Create(Guid.NewGuid());
        var id2 = Id.Create(Guid.NewGuid());

        // Act
        var result = id1 == id2;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Inequality_WithSameValue_ReturnsFalse()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = Id.Create(guid);
        var id2 = Id.Create(guid);

        // Act
        var result = id1 != id2;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Inequality_WithDifferentValue_ReturnsTrue()
    {
        // Arrange
        var id1 = Id.Create(Guid.NewGuid());
        var id2 = Id.Create(Guid.NewGuid());

        // Act
        var result = id1 != id2;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ToString_ReturnsGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = Id.Create(guid);

        // Act
        var result = id.ToString();

        // Assert
        result.Should().Be(guid.ToString());
    }
}
