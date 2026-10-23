namespace Atlas.Core.Layout;

/// <summary>
///     Policy for fallback activation of the selected item after the currently selected item is closed in a document group.
/// </summary>
public enum EditorActivationPolicy
{
    /// <summary>
    ///     Selects the remaining item at the original index of the closed item; when the index is out of range, the last item is chosen.
    /// </summary>
    Adjacent = 0,

    /// <summary>
    ///     Selects the foremost item that still exists in the recently visited list.
    /// </summary>
    MostRecentlyVisited = 1,
}
