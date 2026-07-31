namespace Atlas.Core;

/// <summary>
/// Identifies a dock item within an Atlas workspace.
/// </summary>
public readonly struct DockItemId : IEquatable<DockItemId>
{
    private readonly string? _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockItemId"/> struct.
    /// </summary>
    /// <param name="value">The non-empty, non-whitespace identifier value.</param>
    public DockItemId(string value)
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
        ?? throw new InvalidOperationException("A default DockItemId does not have a value.");

    /// <inheritdoc />
    public bool Equals(DockItemId other)
    {
        return StringComparer.Ordinal.Equals(_value, other._value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is DockItemId other && Equals(other);
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
    /// Determines whether two dock item identifiers are equal.
    /// </summary>
    public static bool operator ==(DockItemId left, DockItemId right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two dock item identifiers are not equal.
    /// </summary>
    public static bool operator !=(DockItemId left, DockItemId right)
    {
        return !left.Equals(right);
    }
}
