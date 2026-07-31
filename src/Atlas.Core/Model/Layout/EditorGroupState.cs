namespace Atlas.Core.Layout;

/// <summary>
/// Stores document-group behavior and recent user-visit order.
/// </summary>
public sealed class EditorGroupState
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EditorGroupState"/> class.
    /// </summary>
    public EditorGroupState(
        EditorOverflowMode overflowMode = EditorOverflowMode.Scroll,
        EditorActivationPolicy activationPolicy = EditorActivationPolicy.Adjacent,
        IEnumerable<DockItemId>? recentlyVisitedIds = null)
    {
        if (overflowMode is < EditorOverflowMode.Scroll or > EditorOverflowMode.Wrap)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overflowMode),
                overflowMode,
                "Unknown editor overflow mode.");
        }

        if (activationPolicy is not EditorActivationPolicy.Adjacent
            and not EditorActivationPolicy.MostRecentlyVisited)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activationPolicy),
                activationPolicy,
                "Unknown editor activation policy.");
        }

        var materializedIds = recentlyVisitedIds?.ToArray() ?? Array.Empty<DockItemId>();
        if (materializedIds.Any(itemId => itemId == default))
        {
            throw new ArgumentException(
                "Recently visited items cannot contain a default identifier.",
                nameof(recentlyVisitedIds));
        }

        if (materializedIds.Distinct().Count() != materializedIds.Length)
        {
            throw new ArgumentException(
                "Recently visited items cannot contain duplicate identifiers.",
                nameof(recentlyVisitedIds));
        }

        OverflowMode = overflowMode;
        ActivationPolicy = activationPolicy;
        RecentlyVisitedIds = Array.AsReadOnly(materializedIds);
    }

    /// <summary>
    /// Gets the tab overflow mode.
    /// </summary>
    public EditorOverflowMode OverflowMode { get; }

    /// <summary>
    /// Gets the fallback activation policy.
    /// </summary>
    public EditorActivationPolicy ActivationPolicy { get; }

    /// <summary>
    /// Gets user-visited item identities in most-recent-first order.
    /// </summary>
    public IReadOnlyList<DockItemId> RecentlyVisitedIds { get; }
}
