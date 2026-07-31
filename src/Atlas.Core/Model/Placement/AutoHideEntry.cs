namespace Atlas.Core.Placement;

/// <summary>
/// Represents one auto-hidden tool item and its dock-back bookmark.
/// </summary>
public readonly struct AutoHideEntry : IEquatable<AutoHideEntry>
{
    private readonly DockItemId _itemId;
    private readonly DockBack _dockBack;
    private readonly bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoHideEntry"/> struct.
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
    /// Gets the auto-hidden item identity.
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
    /// Gets the dock-back bookmark.
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
    /// Determines whether two auto-hide entries are equal.
    /// </summary>
    public static bool operator ==(AutoHideEntry left, AutoHideEntry right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two auto-hide entries are not equal.
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
