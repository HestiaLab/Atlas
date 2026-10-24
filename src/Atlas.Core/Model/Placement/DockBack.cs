namespace Atlas.Core.Placement;

/// <summary>
///     用于恢复（回停）自动隐藏条目的书签：优先回到原 Group 与原索引，原 Group
///     不存在时回退到边缘。
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
    ///     构造书签；首选 Group 标识符不得为默认值，索引不得为负数，回退边缘
    ///     必须是已知值。
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
    ///     恢复时优先返回的 Group。
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
    ///     在 Group 内恢复时插入的位置索引（非负）。
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
    ///     首选 Group 缺失时使用的回退放置边缘。
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
    ///     获取一个值，指示该实例是否为默认的未初始化值。
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
    ///     判断两个 dock-back 书签是否相等。
    /// </summary>
    public static bool operator ==(DockBack left, DockBack right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     判断两个 dock-back 书签是否不相等。
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
