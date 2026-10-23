namespace Atlas.Core.Items;

/// <summary>
///     The pinned/preview state carried by document items only; the two are mutually exclusive.
/// </summary>
public readonly struct DocumentState : IEquatable<DocumentState>
{
    /// <summary>
    ///     Creates a document state; throws immediately when pinned and preview are both <see langword="true"/>.
    /// </summary>
    /// <param name="isPinned">Whether the document is pinned.</param>
    /// <param name="isPreview">Whether the document is a preview instance.</param>
    /// <exception cref="ArgumentException">
    ///     A document cannot be pinned and a preview at the same time.
    /// </exception>
    public DocumentState(bool isPinned, bool isPreview)
    {
        if (isPinned && isPreview)
        {
            throw new ArgumentException("A document cannot be pinned and a preview at the same time.");
        }

        IsPinned = isPinned;
        IsPreview = isPreview;
    }

    /// <summary>
    ///     Whether the document is pinned; mutually exclusive with <see cref="IsPreview"/>.
    /// </summary>
    public bool IsPinned { get; }

    /// <summary>
    ///     Whether the document is a preview instance; mutually exclusive with <see cref="IsPinned"/>.
    /// </summary>
    public bool IsPreview { get; }

    /// <summary>
    ///     Gets a value indicating whether this instance is the default state
    ///     (unpinned and not a preview). The default state is a legal value.
    /// </summary>
    public bool IsDefault => !IsPinned && !IsPreview;

    /// <inheritdoc />
    public bool Equals(DocumentState other)
    {
        return IsPinned == other.IsPinned && IsPreview == other.IsPreview;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is DocumentState other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(IsPinned, IsPreview);
    }

    /// <summary>
    ///     Determines whether two document states are equal.
    /// </summary>
    public static bool operator ==(DocumentState left, DocumentState right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Determines whether two document states are unequal.
    /// </summary>
    public static bool operator !=(DocumentState left, DocumentState right)
    {
        return !left.Equals(right);
    }
}
