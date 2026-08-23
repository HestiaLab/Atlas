namespace Atlas.Core.Placement;

/// <summary>
///     自动隐藏条中的一项：被隐藏的工具项标识 + 恢复用的 dock-back 书签。
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
    ///     构造自动隐藏条目；项标识与书签均不得为默认值。
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
    ///     被隐藏的工具项标识。
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
    ///     恢复该条目时使用的 dock-back 书签。
    /// </summary>
    public DockBack DockBack
    {
        get
        {
            EnsureInitialized();
            return _dockBack;
        }
    }

    internal bool IsDefault => !_isInitialized;

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
    ///     确定两个自动隐藏条目是否相等。
    /// </summary>
    public static bool operator ==(AutoHideEntry left, AutoHideEntry right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     确定两个自动隐藏条目是否不相等。
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
