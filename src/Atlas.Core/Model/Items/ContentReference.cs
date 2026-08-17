namespace Atlas.Core.Items;

/// <summary>
///     内容标识：Kind 选择渲染器或提供程序，ResourceId 定位其渲染的应用资源；
///     两字段共同构成内容身份比较与去重判定的依据。
/// </summary>
public readonly struct ContentReference : IEquatable<ContentReference>
{
    // 内容种类：决定由哪个渲染器或提供程序呈现；default 实例为 null。
    private readonly string? _kind;

    // 应用资源标识：在种类范围内定位具体内容；default 实例为 null。
    private readonly string? _resourceId;

    /// <summary>
    ///     包装 (Kind, ResourceId) 内容引用；任一字段为 null 或空白时立即抛参数异常。
    /// </summary>
    /// <param name="kind">已注册的渲染器或提供程序种类。</param>
    /// <param name="resourceId">应用程序资源标识。</param>
    public ContentReference(string kind, string resourceId)
    {
        _kind = IdentifierGuard.RequireValue(kind, nameof(kind));
        _resourceId = IdentifierGuard.RequireValue(resourceId, nameof(resourceId));
    }

    /// <summary>
    ///     内容种类：决定由哪个渲染器或提供程序呈现。
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     该实例是默认的未初始化值。
    /// </exception>
    public string Kind => _kind
        ?? throw new InvalidOperationException("A default ContentReference does not have a kind.");

    /// <summary>
    ///     应用资源标识：在 <c>Kind</c> 种类范围内定位具体内容。
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     该实例是默认的未初始化值。
    /// </exception>
    public string ResourceId => _resourceId
        ?? throw new InvalidOperationException("A default ContentReference does not have a resource identity.");

    internal bool IsDefault => _kind is null || _resourceId is null;

    /// <inheritdoc />
    public bool Equals(ContentReference other)
    {
        return StringComparer.Ordinal.Equals(_kind, other._kind)
            && StringComparer.Ordinal.Equals(_resourceId, other._resourceId);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ContentReference other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            _kind is null ? 0 : StringComparer.Ordinal.GetHashCode(_kind),
            _resourceId is null ? 0 : StringComparer.Ordinal.GetHashCode(_resourceId));
    }

    /// <summary>
    ///     按两字段序数比较两个内容引用是否相等。
    /// </summary>
    public static bool operator ==(ContentReference left, ContentReference right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     按两字段序数比较两个内容引用是否不等。
    /// </summary>
    public static bool operator !=(ContentReference left, ContentReference right)
    {
        return !left.Equals(right);
    }
}
