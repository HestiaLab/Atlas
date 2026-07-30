namespace Atlas.Core.Items;

/// <summary>
/// Identifies the behavioral kind of a dock item.
/// </summary>
public enum DockItemKind
{
    /// <summary>
    /// A tool window item hosted by a tool group.
    /// </summary>
    Tool = 0,

    /// <summary>
    /// A document item hosted by a document group.
    /// </summary>
    Document = 1,
}
