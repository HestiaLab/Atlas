namespace Atlas.Core.Items;

/// <summary>
///     Identifies the behavioural kind of a dock item; determines the hosting group, the allowed capability set, and the document state rules.
/// </summary>
public enum DockItemKind
{
    /// <summary>
    ///     A tool window item hosted by a tool group.
    /// </summary>
    Tool = 0,

    /// <summary>
    ///     A document item hosted by a document group.
    /// </summary>
    Document = 1,
}
