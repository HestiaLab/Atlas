namespace Atlas.Core.Operations;

/// <summary>Identifies the logical drop zone for a document docking operation.</summary>
public enum DocumentDockZone
{
    /// <summary>Appends the document to the target editor group.</summary>
    Center = 0,

    /// <summary>Creates an editor group before the target on the inline axis.</summary>
    InlineStart = 1,

    /// <summary>Creates an editor group after the target on the inline axis.</summary>
    InlineEnd = 2,

    /// <summary>Creates an editor group before the target on the block axis.</summary>
    BlockStart = 3,

    /// <summary>Creates an editor group after the target on the block axis.</summary>
    BlockEnd = 4,
}
