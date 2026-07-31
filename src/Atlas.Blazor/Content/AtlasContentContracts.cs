using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Results;
using Microsoft.AspNetCore.Components;

namespace Atlas.Blazor.Content;

/// <summary>Provides item-scoped commands to rendered content.</summary>
public sealed class AtlasContentCommands
{
    private readonly IAtlasWorkspace workspace;
    private readonly DockItemId itemId;

    internal AtlasContentCommands(IAtlasWorkspace workspace, DockItemId itemId)
    {
        this.workspace = workspace;
        this.itemId = itemId;
    }

    /// <summary>Activates the current item.</summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> ActivateAsync(
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(new SelectItemOperation(itemId), cancellationToken);
    }

    /// <summary>Closes the current item when its capability permits closing.</summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> CloseAsync(
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(new CloseItemOperation(itemId), cancellationToken);
    }

    /// <summary>Updates the current item's presentation.</summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> UpdatePresentationAsync(
        string? title,
        string? iconKey,
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(
            new UpdateItemPresentationOperation(itemId, title, iconKey),
            cancellationToken);
    }
}

/// <summary>Provides immutable content identity, item state, and bound commands.</summary>
public sealed class AtlasContentContext
{
    internal AtlasContentContext(IAtlasWorkspace workspace, DockItem item)
    {
        ItemId = item.Id;
        Reference = item.Content;
        Item = item;
        Commands = new AtlasContentCommands(workspace, item.Id);
    }

    /// <summary>Gets the current UI occurrence identity.</summary>
    public DockItemId ItemId { get; }

    /// <summary>Gets the renderer and resource identity.</summary>
    public ContentReference Reference { get; }

    /// <summary>Gets the immutable item at the rendered revision.</summary>
    public DockItem Item { get; }

    /// <summary>Gets commands bound to the current item.</summary>
    public AtlasContentCommands Commands { get; }
}

/// <summary>Base class for provider-resolved Atlas content.</summary>
public abstract class AtlasContentComponent : ComponentBase
{
    /// <summary>Gets or sets the required content context.</summary>
    [Parameter]
    [EditorRequired]
    public AtlasContentContext Content { get; set; } = null!;
}

/// <summary>Provides immutable context for a group empty state.</summary>
public sealed class AtlasEmptyStateContext
{
    internal AtlasEmptyStateContext(
        WorkspaceId workspaceId,
        LayoutNodeId groupId,
        DockItemKind groupKind,
        GroupRetentionPolicy retentionPolicy)
    {
        WorkspaceId = workspaceId;
        GroupId = groupId;
        GroupKind = groupKind;
        RetentionPolicy = retentionPolicy;
    }

    /// <summary>Gets the workspace identity.</summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>Gets the empty group identity.</summary>
    public LayoutNodeId GroupId { get; }

    /// <summary>Gets the group item kind.</summary>
    public DockItemKind GroupKind { get; }

    /// <summary>Gets the group retention policy.</summary>
    public GroupRetentionPolicy RetentionPolicy { get; }
}

/// <summary>Provides immutable item data to a header template.</summary>
public sealed class AtlasItemHeaderContext
{
    internal AtlasItemHeaderContext(DockItem item, bool isSelected)
    {
        Item = item;
        IsSelected = isSelected;
    }

    /// <summary>Gets the rendered item.</summary>
    public DockItem Item { get; }

    /// <summary>Gets whether the item is selected in its group.</summary>
    public bool IsSelected { get; }
}
