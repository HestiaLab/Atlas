namespace Atlas.Core.Items;

/// <summary>
/// Contains committed state that is valid only for document items.
/// </summary>
public readonly struct DocumentState : IEquatable<DocumentState>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentState"/> struct.
    /// </summary>
    /// <param name="isPinned">Whether the document is pinned.</param>
    /// <param name="isPreview">Whether the document is a preview occurrence.</param>
    /// <exception cref="ArgumentException">
    /// The document is both pinned and a preview.
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
    /// Gets a value indicating whether the document is pinned.
    /// </summary>
    public bool IsPinned { get; }

    /// <summary>
    /// Gets a value indicating whether the document is a preview occurrence.
    /// </summary>
    public bool IsPreview { get; }

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
    /// Determines whether two document states are equal.
    /// </summary>
    public static bool operator ==(DocumentState left, DocumentState right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two document states are not equal.
    /// </summary>
    public static bool operator !=(DocumentState left, DocumentState right)
    {
        return !left.Equals(right);
    }
}
