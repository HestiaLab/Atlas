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
    private readonly IAtlasContentStateScope? stateScope;

    internal AtlasContentContext(IAtlasWorkspace workspace, DockItem item)
        : this(workspace, item, stateScope: null)
    {
    }

    internal AtlasContentContext(
        IAtlasWorkspace workspace,
        DockItem item,
        IAtlasContentStateScope? stateScope)
    {
        WorkspaceId = workspace.Id;
        ItemId = item.Id;
        Reference = item.Content;
        Item = item;
        Commands = new AtlasContentCommands(workspace, item.Id);
        this.stateScope = stateScope;
    }

    /// <summary>Gets the hosting Workspace scope used to partition shared state.</summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>Gets the current UI occurrence identity.</summary>
    public DockItemId ItemId { get; }

    /// <summary>Gets the renderer and resource identity.</summary>
    public ContentReference Reference { get; }

    /// <summary>Gets the immutable item at the rendered revision.</summary>
    public DockItem Item { get; }

    /// <summary>Gets commands bound to the current item.</summary>
    public AtlasContentCommands Commands { get; }

    /// <summary>
    /// Gets the per-item business state resolved for <see cref="Reference"/>, or
    /// <see langword="null"/> when no state was created yet or no state scope is
    /// available. Querying never creates state.
    /// </summary>
    public TState? GetState<TState>()
        where TState : class
    {
        if (stateScope is null)
        {
            return null;
        }

        return stateScope.TryGetState<TState>(Reference, out var state) ? state : null;
    }

    /// <summary>
    /// Gets the per-item business state resolved for <see cref="Reference"/>,
    /// creating it through <paramref name="factory"/> when needed. Without a
    /// state scope the factory runs on every call (backward-compatible
    /// fallback).
    /// </summary>
    public TState GetOrCreateState<TState>(Func<TState> factory)
        where TState : class
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        if (stateScope is null)
        {
            return factory();
        }

        return stateScope.GetOrCreate(Reference, factory);
    }
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
