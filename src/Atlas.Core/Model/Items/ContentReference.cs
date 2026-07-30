namespace Atlas.Core.Items;

/// <summary>
/// Identifies a content renderer kind and the application resource it renders.
/// </summary>
public readonly struct ContentReference : IEquatable<ContentReference>
{
    private readonly string? _kind;
    private readonly string? _resourceId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentReference"/> struct.
    /// </summary>
    /// <param name="kind">The registered renderer or provider kind.</param>
    /// <param name="resourceId">The application resource identity.</param>
    public ContentReference(string kind, string resourceId)
    {
        _kind = IdentifierGuard.RequireValue(kind, nameof(kind));
        _resourceId = IdentifierGuard.RequireValue(resourceId, nameof(resourceId));
    }

    /// <summary>
    /// Gets the renderer or provider kind.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The instance is the default, uninitialized value.
    /// </exception>
    public string Kind => _kind
        ?? throw new InvalidOperationException("A default ContentReference does not have a kind.");

    /// <summary>
    /// Gets the application resource identity.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The instance is the default, uninitialized value.
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
    /// Determines whether two content references are equal.
    /// </summary>
    public static bool operator ==(ContentReference left, ContentReference right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two content references are not equal.
    /// </summary>
    public static bool operator !=(ContentReference left, ContentReference right)
    {
        return !left.Equals(right);
    }
}
