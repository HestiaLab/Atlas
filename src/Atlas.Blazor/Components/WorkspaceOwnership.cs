namespace Atlas.Blazor.Components;

/// <summary>Controls ownership of a programmatically supplied workspace.</summary>
public enum WorkspaceOwnership
{
    /// <summary>Uses host ownership for declarative mode and external ownership for programmatic mode.</summary>
    Automatic = 0,

    /// <summary>The application owns and disposes the workspace.</summary>
    External = 1,

    /// <summary>The host owns and disposes the workspace.</summary>
    Host = 2,
}
