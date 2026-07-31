using Atlas.Core.Items;
using Atlas.Core.Operations;

namespace Atlas.Core;

/// <summary>Defines immutable create-time workspace services and history behavior.</summary>
public sealed class AtlasWorkspaceOptions
{
    /// <summary>Initializes workspace options.</summary>
    public AtlasWorkspaceOptions(
        WorkspaceHistoryOptions? history = null,
        IContentIdentityPolicy? contentIdentityPolicy = null,
        IDockItemCloseGuardResolver? closeGuardResolver = null)
    {
        History = history ?? new WorkspaceHistoryOptions();
        ContentIdentityPolicy = contentIdentityPolicy;
        CloseGuardResolver = closeGuardResolver;
    }

    /// <summary>Gets bounded history behavior.</summary>
    public WorkspaceHistoryOptions History { get; }

    /// <summary>Gets the optional custom content identity policy.</summary>
    public IContentIdentityPolicy? ContentIdentityPolicy { get; }

    /// <summary>Gets the optional item close guard resolver.</summary>
    public IDockItemCloseGuardResolver? CloseGuardResolver { get; }
}
