namespace Atlas.Core.Placement;

/// <summary>
///     Bookmark for restoring (docking back) an auto-hidden item: preferably back to the original group and
///     index, falling back to an edge when the original group no longer exists.
/// </summary>
public readonly struct DockBack : IEquatable<DockBack>
{
    // 恢复时优先回归的组；原组不存在时回退到边缘。
    private readonly LayoutNodeId _preferredGroupId;

    // 组内恢复插入的位置索引，构造时校验非负。
    private readonly int _preferredIndex;

    // 优先组缺失时的回退放置边缘。
    private readonly WorkspaceEdge _fallbackEdge;

    // 是否经构造器初始化；default 实例为 false，属性访问抛异常。
    private readonly bool _isInitialized;

    /// <summary>
    ///     Constructs a bookmark; the preferred group identifier must not be default, the index must not be negative, and the fallback edge must be known.
    /// </summary>
    public DockBack(
        LayoutNodeId preferredGroupId,
        int preferredIndex,
        WorkspaceEdge fallbackEdge)
    {
        if (preferredGroupId == default)
        {
            throw new ArgumentException(
                "A preferred group identifier cannot be the default value.",
                nameof(preferredGroupId));
        }

        if (preferredIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preferredIndex),
                preferredIndex,
                "A preferred item index cannot be negative.");
        }

        ValidateEdge(fallbackEdge);

        _preferredGroupId = preferredGroupId;
        _preferredIndex = preferredIndex;
        _fallbackEdge = fallbackEdge;
        _isInitialized = true;
    }

    /// <summary>
    ///     The group to return to first when restoring.
    /// </summary>
    public LayoutNodeId PreferredGroupId
    {
        get
        {
            EnsureInitialized();
            return _preferredGroupId;
        }
    }

    /// <summary>
    ///     The index at which to insert when restoring within the group (non-negative).
    /// </summary>
    public int PreferredIndex
    {
        get
        {
            EnsureInitialized();
            return _preferredIndex;
        }
    }

    /// <summary>
    ///     The fallback placement edge used when the preferred group is missing.
    /// </summary>
    public WorkspaceEdge FallbackEdge
    {
        get
        {
            EnsureInitialized();
            return _fallbackEdge;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether this instance is the default, uninitialized value.
    /// </summary>
    public bool IsDefault => !_isInitialized;

    /// <inheritdoc />
    public bool Equals(DockBack other)
    {
        return _preferredGroupId == other._preferredGroupId
            && _preferredIndex == other._preferredIndex
            && _fallbackEdge == other._fallbackEdge
            && _isInitialized == other._isInitialized;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is DockBack other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            _preferredGroupId,
            _preferredIndex,
            _fallbackEdge,
            _isInitialized);
    }

    /// <summary>
    ///     Determines whether two dock-back bookmarks are equal.
    /// </summary>
    public static bool operator ==(DockBack left, DockBack right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Determines whether two dock-back bookmarks are unequal.
    /// </summary>
    public static bool operator !=(DockBack left, DockBack right)
    {
        return !left.Equals(right);
    }

    private static void ValidateEdge(WorkspaceEdge edge)
    {
        if (edge is not WorkspaceEdge.InlineStart
            and not WorkspaceEdge.InlineEnd
            and not WorkspaceEdge.BlockEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(edge), edge, "Unknown workspace edge.");
        }
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("A default DockBack does not contain a bookmark.");
        }
    }
}
