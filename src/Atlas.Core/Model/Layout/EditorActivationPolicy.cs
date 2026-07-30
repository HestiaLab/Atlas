namespace Atlas.Core.Layout;

/// <summary>
/// Defines how a document group chooses a fallback selection.
/// </summary>
public enum EditorActivationPolicy
{
    /// <summary>
    /// Prefer the adjacent tab at the closed tab's index.
    /// </summary>
    Adjacent = 0,

    /// <summary>
    /// Prefer the most recently visited remaining tab.
    /// </summary>
    MostRecentlyVisited = 1,
}
