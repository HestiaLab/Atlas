namespace Atlas.Core;

/// <summary>
///     标识一个 Atlas 工作区。
/// </summary>
public readonly struct WorkspaceId : IEquatable<WorkspaceId>
{
    // 包装的原始工作区标识字符串；default 实例为 null，访问 Value 抛异常。
    private readonly string? _value;

    /// <summary>
    ///     包装一个标识字符串；值为 null 或空白时立即抛参数异常。
    /// </summary>
    /// <param name="value">非空、非空白的标识符值。</param>
    public WorkspaceId(string value)
    {
        _value = IdentifierGuard.RequireValue(value, nameof(value));
    }

    /// <summary>
    ///     构造时经 IdentifierGuard 校验的原始标识字符串。
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     该实例是默认的未初始化值。
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
    ///     按底层字符串序数比较两个工作区标识是否相等。
    /// </summary>
    public static bool operator ==(WorkspaceId left, WorkspaceId right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     按底层字符串序数比较两个工作区标识是否不等。
    /// </summary>
    public static bool operator !=(WorkspaceId left, WorkspaceId right)
    {
        return !left.Equals(right);
    }
}
