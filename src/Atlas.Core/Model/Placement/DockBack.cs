namespace Atlas.Core.Placement;

/// <summary>
/// Stores the preferred group placement for restoring an auto-hidden item.
/// </summary>
public readonly struct DockBack : IEquatable<DockBack>
{
    private readonly LayoutNodeId _preferredGroupId;
    private readonly int _preferredIndex;
    private readonly WorkspaceEdge _fallbackEdge;
    private readonly bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockBack"/> struct.
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
    /// Gets the preferred group identity.
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
    /// Gets the preferred insertion index.
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
    /// Gets the edge used when the preferred group no longer exists.
    /// </summary>
    public WorkspaceEdge FallbackEdge
    {
        get
        {
            EnsureInitialized();
            return _fallbackEdge;
        }
    }

    internal bool IsDefault => !_isInitialized;

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
    /// Determines whether two dock-back bookmarks are equal.
    /// </summary>
    public static bool operator ==(DockBack left, DockBack right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two dock-back bookmarks are not equal.
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
