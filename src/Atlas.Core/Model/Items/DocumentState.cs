namespace Atlas.Core.Items;

/// <summary>
///     仅文档项携带的固定/预览状态；两者互斥。
/// </summary>
public readonly struct DocumentState : IEquatable<DocumentState>
{
    /// <summary>
    ///     创建文档状态；固定与预览同时为 <see langword="true"/> 时立即抛异常。
    /// </summary>
    /// <param name="isPinned">文档是否已固定。</param>
    /// <param name="isPreview">文档是否为预览实例。</param>
    /// <exception cref="ArgumentException">
    ///     文档不能同时处于固定状态和预览状态。
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
    ///     文档是否固定；与 <see cref="IsPreview"/> 互斥。
    /// </summary>
    public bool IsPinned { get; }

    /// <summary>
    ///     文档是否为预览实例；与 <see cref="IsPinned"/> 互斥。
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
    ///     确定两个文档状态是否相等。
    /// </summary>
    public static bool operator ==(DocumentState left, DocumentState right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     确定两个文档状态是否不相等。
    /// </summary>
    public static bool operator !=(DocumentState left, DocumentState right)
    {
        return !left.Equals(right);
    }
}
