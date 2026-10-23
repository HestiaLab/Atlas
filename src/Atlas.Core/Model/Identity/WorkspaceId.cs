namespace Atlas.Core;

/// <summary>
///     Identifies an Atlas workspace.
/// </summary>
public readonly struct WorkspaceId : IEquatable<WorkspaceId>
{
    // 包装的原始工作区标识字符串；default 实例为 null，访问 Value 抛异常。
    private readonly string? _value;

    /// <summary>
    ///     Wraps an identifier string; throws an argument exception immediately when the value is null or whitespace.
    /// </summary>
    /// <param name="value">A non-null, non-whitespace identifier value.</param>
    public WorkspaceId(string value)
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
        ?? throw new InvalidOperationException("A default WorkspaceId does not have a value.");

    /// <inheritdoc />
    public bool Equals(WorkspaceId other)
    {
        return StringComparer.Ordinal.Equals(_value, other._value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is WorkspaceId other && Equals(other);
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
    ///     Compares two workspace identifiers for equality by ordinal comparison of the underlying strings.
    /// </summary>
    public static bool operator ==(WorkspaceId left, WorkspaceId right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Compares two workspace identifiers for inequality by ordinal comparison of the underlying strings.
    /// </summary>
    public static bool operator !=(WorkspaceId left, WorkspaceId right)
    {
        return !left.Equals(right);
    }
}
