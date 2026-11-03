namespace Atlas.Core.Placement;

/// <summary>
///     浮动窗口的返回书签：记录浮出前的 placement 种类、逻辑区域与 dock-back
///     位置。默认实例不携带数据，属性访问时抛出异常。
/// </summary>
public readonly struct FloatingReturnPlacement : IEquatable<FloatingReturnPlacement>
{
    private readonly FloatingReturnKind _kind;
    private readonly LogicalRegion _region;
    private readonly DockBack _dockBack;
    private readonly WorkspaceEdge? _autoHideEdge;
    private readonly int? _autoHideIndex;
    private readonly bool _returnGroupWasDynamic;
    private readonly bool _isInitialized;

    /// <summary>
    ///     构造返回 Group 的书签；区域必须已知，dock-back 不得为默认值。
    /// </summary>
    /// <param name="region">浮出时所属的逻辑工具区域。</param>
    /// <param name="dockBack">原 Group、索引与回退边缘。</param>
    public FloatingReturnPlacement(
        LogicalRegion region,
        DockBack dockBack)
        : this(region, dockBack, returnGroupWasDynamic: false)
    {
    }

    /// <summary>
    ///     构造返回 Group 的书签，并记录原 Group 是否为 Dynamic。
    /// </summary>
    /// <param name="region">浮出时所属的逻辑工具区域。</param>
    /// <param name="dockBack">原 Group、索引与回退边缘。</param>
    /// <param name="returnGroupWasDynamic">原 Group 是否为 Dynamic。</param>
    public FloatingReturnPlacement(
        LogicalRegion region,
        DockBack dockBack,
        bool returnGroupWasDynamic)
    {
        ValidateRegion(region);
        if (dockBack.IsDefault)
        {
            throw new ArgumentException(
                "A floating-window return placement requires a dock-back bookmark.",
                nameof(dockBack));
        }

        _kind = FloatingReturnKind.Group;
        _region = region;
        _dockBack = dockBack;
        _returnGroupWasDynamic = returnGroupWasDynamic;
        _isInitialized = true;
    }

    /// <summary>
    ///     构造返回 Auto-hide 的书签；区域、边缘与索引必须有效。
    /// </summary>
    /// <param name="region">浮出时所属的逻辑工具区域。</param>
    /// <param name="dockBack">原 Group、索引与回退边缘。</param>
    /// <param name="autoHideEdge">原自动隐藏条边缘。</param>
    /// <param name="autoHideIndex">原自动隐藏条内的从零开始索引。</param>
    public FloatingReturnPlacement(
        LogicalRegion region,
        DockBack dockBack,
        WorkspaceEdge autoHideEdge,
        int autoHideIndex)
    {
        ValidateRegion(region);
        if (dockBack.IsDefault)
        {
            throw new ArgumentException(
                "A floating-window return placement requires a dock-back bookmark.",
                nameof(dockBack));
        }

        ValidateEdge(autoHideEdge);
        if (autoHideIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(autoHideIndex),
                autoHideIndex,
                "An auto-hide index cannot be negative.");
        }

        _kind = FloatingReturnKind.AutoHide;
        _region = region;
        _dockBack = dockBack;
        _autoHideEdge = autoHideEdge;
        _autoHideIndex = autoHideIndex;
        _isInitialized = true;
    }

    /// <summary>
    ///     返回 placement 种类。
    /// </summary>
    public FloatingReturnKind Kind
    {
        get
        {
            EnsureInitialized();
            return _kind;
        }
    }

    /// <summary>
    ///     浮出时所属的逻辑工具区域。
    /// </summary>
    public LogicalRegion Region
    {
        get
        {
            EnsureInitialized();
            return _region;
        }
    }

    /// <summary>
    ///     原 Group、索引与回退边缘。
    /// </summary>
    public DockBack DockBack
    {
        get
        {
            EnsureInitialized();
            return _dockBack;
        }
    }

    /// <summary>
    ///     原自动隐藏条边缘；仅 <see cref="FloatingReturnKind.AutoHide"/> 时有值。
    /// </summary>
    public WorkspaceEdge? AutoHideEdge
    {
        get
        {
            EnsureInitialized();
            return _autoHideEdge;
        }
    }

    /// <summary>
    ///     原自动隐藏条内的索引；仅 <see cref="FloatingReturnKind.AutoHide"/> 时有值。
    /// </summary>
    public int? AutoHideIndex
    {
        get
        {
            EnsureInitialized();
            return _autoHideIndex;
        }
    }

    /// <summary>
    ///     获取一个值，指示原 Group 是否为 Dynamic；仅 Group 返回时有意义。
    /// </summary>
    public bool ReturnGroupWasDynamic
    {
        get
        {
            EnsureInitialized();
            return _returnGroupWasDynamic;
        }
    }

    /// <summary>
    ///     获取一个值，指示该实例是否为默认的未初始化值。
    /// </summary>
    public bool IsDefault => !_isInitialized;

    /// <inheritdoc />
    public bool Equals(FloatingReturnPlacement other)
    {
        return _kind == other._kind
            && _region == other._region
            && _dockBack.Equals(other._dockBack)
            && _autoHideEdge == other._autoHideEdge
            && _autoHideIndex == other._autoHideIndex
            && _returnGroupWasDynamic == other._returnGroupWasDynamic
            && _isInitialized == other._isInitialized;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is FloatingReturnPlacement other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            _kind,
            _region,
            _dockBack,
            _autoHideEdge,
            _autoHideIndex,
            _returnGroupWasDynamic,
            _isInitialized);
    }

    /// <summary>
    ///     比较两个返回书签是否相等。
    /// </summary>
    public static bool operator ==(
        FloatingReturnPlacement left,
        FloatingReturnPlacement right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     比较两个返回书签是否不相等。
    /// </summary>
    public static bool operator !=(
        FloatingReturnPlacement left,
        FloatingReturnPlacement right)
    {
        return !left.Equals(right);
    }

    private static void ValidateRegion(LogicalRegion region)
    {
        if (!LogicalRegionEdge.IsDefined(region))
        {
            throw new ArgumentOutOfRangeException(
                nameof(region),
                region,
                "Unknown logical region.");
        }
    }

    private static void ValidateEdge(WorkspaceEdge edge)
    {
        if (edge is not WorkspaceEdge.InlineStart
            and not WorkspaceEdge.InlineEnd
            and not WorkspaceEdge.BlockEnd)
        {
            throw new ArgumentOutOfRangeException(
                nameof(edge),
                edge,
                "Unknown workspace edge.");
        }
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "A default floating-window return placement has no bookmark.");
        }
    }
}
