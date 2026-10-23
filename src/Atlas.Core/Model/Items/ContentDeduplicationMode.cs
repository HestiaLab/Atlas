namespace Atlas.Core.Items;

/// <summary>
///     Policy for handling duplicate content identity when opening an item (determined by the content
///     identity policy based on <c>ContentReference</c>); the open planner uses it to decide whether to
///     activate the existing item, create a new instance, replace in place, or fail with a typed error.
/// </summary>
public enum ContentDeduplicationMode
{
    /// <summary>
    ///     Activates the already open item when a duplicate is found; no new instance is created.
    /// </summary>
    ActivateExisting = 0,

    /// <summary>
    ///     The same content may be referenced by multiple item instances, each independent of the others.
    /// </summary>
    AllowMultiple = 1,

    /// <summary>
    ///     Keeps the existing item identifier and placement and only refreshes the content with the requested content.
    /// </summary>
    ReplaceExisting = 2,

    /// <summary>
    ///     Rejects the open with a typed failure without producing any layout change.
    /// </summary>
    RejectDuplicate = 3,
}
