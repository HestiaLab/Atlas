namespace Atlas.Core.Layout;

/// <summary>
///     Behavioural state of a document group: tab overflow mode, fallback activation policy, and recent visit order;
///     the head of the visit list is the preferred candidate for the next most-recently-visited fallback activation.
/// </summary>
public sealed class EditorGroupState
{
    /// <summary>
    ///     Constructs document group state; throws an argument exception when an enum value is out of range or the visit list contains a default or duplicate identifier.
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
    ///     The presentation mode used when the tabs do not fit.
    /// </summary>
    public EditorOverflowMode OverflowMode { get; }

    /// <summary>
    ///     The fallback activation policy used after the currently selected item is closed.
    /// </summary>
    public EditorActivationPolicy ActivationPolicy { get; }

    /// <summary>
    ///     Item identifiers ordered by most recent visit (read-only); a newly visited item is inserted at the head.
    /// </summary>
    public IReadOnlyList<DockItemId> RecentlyVisitedIds { get; }
}
