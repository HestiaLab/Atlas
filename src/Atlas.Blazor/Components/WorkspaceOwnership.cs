namespace Atlas.Blazor.Components;

/// <summary>
///     Controls the ownership of a programmatically supplied workspace.
/// </summary>
public enum WorkspaceOwnership
{
    /// <summary>
    ///     Uses Host ownership in declarative mode and external ownership in
    ///     programmatic mode.
    /// </summary>
    Automatic = 0,

    /// <summary>
    ///     The workspace is owned and disposed by the application.
    /// </summary>
    External = 1,

    /// <summary>
    ///     The workspace is owned and disposed by the Host.
    /// </summary>
    Host = 2,
}
