namespace Atlas.Core.Placement;

/// <summary>
///     An entry in an auto-hide strip: the identifier of the hidden tool item plus the dock-back bookmark used to restore it.
/// </summary>
public readonly struct AutoHideEntry : IEquatable<AutoHideEntry>
{
    // 被隐藏的工具项标识。
    private readonly DockItemId _itemId;

    // 恢复该条目时使用的 dock-back 书签。
    private readonly DockBack _dockBack;

    // 是否经构造器初始化；default 实例为 false，属性访问抛异常。
    private readonly bool _isInitialized;

    /// <summary>
    ///     Constructs an auto-hide entry; neither the item identifier nor the bookmark may be the default value.
    /// </summary>
    public AutoHideEntry(DockItemId itemId, DockBack dockBack)
    {
        if (itemId == default)
        {
            throw new ArgumentException(
                "An auto-hide item identifier cannot be the default value.",
                nameof(itemId));
        }

        if (dockBack.IsDefault)
        {
            throw new ArgumentException(
                "A dock-back bookmark cannot be the default value.",
                nameof(dockBack));
        }

        _itemId = itemId;
        _dockBack = dockBack;
        _isInitialized = true;
    }

    /// <summary>
    ///     The identifier of the hidden tool item.
    /// </summary>
    public DockItemId ItemId
    {
        get
        {
            EnsureInitialized();
            return _itemId;
        }
    }

    /// <summary>
    ///     The dock-back bookmark used to restore this entry.
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
    ///     Gets a value indicating whether this instance is the default, uninitialized value.
    /// </summary>
    public bool IsDefault => !_isInitialized;

    /// <inheritdoc />
    public bool Equals(AutoHideEntry other)
    {
        return _itemId == other._itemId
            && _dockBack == other._dockBack
            && _isInitialized == other._isInitialized;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is AutoHideEntry other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(_itemId, _dockBack, _isInitialized);
    }

    /// <summary>
    ///     Determines whether two auto-hide entries are equal.
    /// </summary>
    public static bool operator ==(AutoHideEntry left, AutoHideEntry right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Determines whether two auto-hide entries are unequal.
    /// </summary>
    public static bool operator !=(AutoHideEntry left, AutoHideEntry right)
    {
        return !left.Equals(right);
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "A default AutoHideEntry does not contain placement data.");
        }
    }
}
