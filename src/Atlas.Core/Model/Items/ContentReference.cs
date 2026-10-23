namespace Atlas.Core.Items;

/// <summary>
///     Content identity: Kind selects the renderer or provider and ResourceId locates the application
///     resource it renders; together the two fields form the basis for content identity comparison and
///     deduplication decisions.
/// </summary>
public readonly struct ContentReference : IEquatable<ContentReference>
{
    // 内容种类：决定由哪个渲染器或提供程序呈现；default 实例为 null。
    private readonly string? _kind;

    // 应用资源标识：在种类范围内定位具体内容；default 实例为 null。
    private readonly string? _resourceId;

    /// <summary>
    ///     Wraps a (Kind, ResourceId) content reference; throws an argument exception immediately when either field is null or whitespace.
    /// </summary>
    /// <param name="kind">A registered renderer or provider kind.</param>
    /// <param name="resourceId">The application resource identifier.</param>
    public ContentReference(string kind, string resourceId)
    {
        _kind = IdentifierGuard.RequireValue(kind, nameof(kind));
        _resourceId = IdentifierGuard.RequireValue(resourceId, nameof(resourceId));
    }

    /// <summary>
    ///     Content kind: determines which renderer or provider presents the content.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     The instance is the default, uninitialized value.
    /// </exception>
    public string Kind => _kind
        ?? throw new InvalidOperationException("A default ContentReference does not have a kind.");

    /// <summary>
    ///     Application resource identifier: locates the specific content within the <c>Kind</c> kind.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     The instance is the default, uninitialized value.
    /// </exception>
    public string ResourceId => _resourceId
        ?? throw new InvalidOperationException("A default ContentReference does not have a resource identity.");

    /// <summary>
    ///     Gets a value indicating whether this instance is the default, uninitialized value.
    /// </summary>
    public bool IsDefault => _kind is null || _resourceId is null;

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
    ///     Compares two content references for equality by ordinal comparison of both fields.
    /// </summary>
    public static bool operator ==(ContentReference left, ContentReference right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Compares two content references for inequality by ordinal comparison of both fields.
    /// </summary>
    public static bool operator !=(ContentReference left, ContentReference right)
    {
        return !left.Equals(right);
    }
}
