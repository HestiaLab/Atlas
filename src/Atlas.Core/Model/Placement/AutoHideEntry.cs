namespace Atlas.Core.Placement;

/// <summary>
///     自动隐藏条中的条目：被隐藏工具项的标识符，以及用于恢复它的 dock-back
///     书签。
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
    ///     构造自动隐藏条目；条目标识符与书签都不得为默认值。
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
    ///     被隐藏工具项的标识符。
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
    ///     用于恢复该条目的 dock-back 书签。
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
    ///     获取一个值，指示该实例是否为默认的未初始化值。
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
    ///     判断两个自动隐藏条目是否相等。
    /// </summary>
    public static bool operator ==(AutoHideEntry left, AutoHideEntry right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     判断两个自动隐藏条目是否不相等。
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
