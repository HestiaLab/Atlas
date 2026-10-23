namespace Atlas.Core;

/// <summary>
///     Identifies a node in the Atlas workspace layout tree.
/// </summary>
public readonly struct LayoutNodeId : IEquatable<LayoutNodeId>
{
    // 包装的原始节点标识字符串；default 实例为 null，访问 Value 抛异常。
    private readonly string? _value;

    /// <summary>
    ///     Wraps an identifier string; throws an argument exception immediately when the value is null or whitespace.
    /// </summary>
    /// <param name="value">A non-null, non-whitespace identifier value.</param>
    public LayoutNodeId(string value)
    {
        _value = IdentifierGuard.RequireValue(value, nameof(value));
    }

    /// <summary>
    ///     The raw identifier string validated by IdentifierGuard at construction time.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     The instance is the default, uninitialized value.
    /// </exception>
    public string Value => _value
        ?? throw new InvalidOperationException("A default LayoutNodeId does not have a value.");

    /// <summary>
    ///     Gets a value indicating whether this instance is the default, uninitialized value.
    /// </summary>
    public bool IsDefault => _value is null;

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
    ///     Compares two layout node identifiers for equality by ordinal comparison of the underlying strings.
    /// </summary>
    public static bool operator ==(LayoutNodeId left, LayoutNodeId right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Compares two layout node identifiers for inequality by ordinal comparison of the underlying strings.
    /// </summary>
    public static bool operator !=(LayoutNodeId left, LayoutNodeId right)
    {
        return !left.Equals(right);
    }
}
