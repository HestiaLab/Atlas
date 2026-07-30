namespace Atlas.Core;

/// <summary>
/// Identifies a node in an Atlas workspace layout tree.
/// </summary>
public readonly struct LayoutNodeId : IEquatable<LayoutNodeId>
{
    private readonly string? _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutNodeId"/> struct.
    /// </summary>
    /// <param name="value">The non-empty, non-whitespace identifier value.</param>
    public LayoutNodeId(string value)
    {
        _value = IdentifierGuard.RequireValue(value, nameof(value));
    }

    /// <summary>
    /// Gets the identifier value.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The instance is the default, uninitialized value.
    /// </exception>
    public string Value => _value
        ?? throw new InvalidOperationException("A default LayoutNodeId does not have a value.");

    /// <inheritdoc />
    public bool Equals(LayoutNodeId other)
    {
        return StringComparer.Ordinal.Equals(_value, other._value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is LayoutNodeId other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return _value ?? string.Empty;
    }

    /// <summary>
    /// Determines whether two layout node identifiers are equal.
    /// </summary>
    public static bool operator ==(LayoutNodeId left, LayoutNodeId right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two layout node identifiers are not equal.
    /// </summary>
    public static bool operator !=(LayoutNodeId left, LayoutNodeId right)
    {
        return !left.Equals(right);
    }
}
