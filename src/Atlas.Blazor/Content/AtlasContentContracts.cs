using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Results;
using Microsoft.AspNetCore.Components;

namespace Atlas.Blazor.Content;

/// <summary>
///     Provides request commands bound to a fixed item for rendered content.
///     Each command submits the corresponding LayoutOperation to the
///     workspace atomically; expected failures return as WorkspaceResult
///     status values and never throw.
/// </summary>
public sealed class AtlasContentCommands
{
    // The workspace the commands commit against.
    private readonly IAtlasWorkspace workspace;

    // The fixed item the commands act on.
    private readonly DockItemId itemId;

    internal AtlasContentCommands(IAtlasWorkspace workspace, DockItemId itemId)
    {
        this.workspace = workspace;
        this.itemId = itemId;
    }

    /// <summary>
    ///     Submits a SelectItemOperation: selects this item, making it the
    ///     active item of its group.
    /// </summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> ActivateAsync(
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(new SelectItemOperation(itemId), cancellationToken);
    }

    /// <summary>
    ///     Submits a CloseItemOperation: whether the close proceeds is decided
    ///     by the workspace from the CanClose capability and any close guard;
    ///     rejections return a failure result.
    /// </summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> CloseAsync(
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(new CloseItemOperation(itemId), cancellationToken);
    }

    /// <summary>
    ///     Submits an UpdateItemPresentationOperation for the title and icon
    ///     metadata; a null title restores the identity default and a null
    ///     iconKey clears the icon.
    /// </summary>
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

/// <summary>
///     The immutable content context handed to outlet renderers: carries the
///     workspace and item identity, the immutable item snapshot, the bound
///     commands, and the per-reference business-state accessor.
/// </summary>
public sealed class AtlasContentContext
{
    // Per-item business-state scope; when null, state access falls back to
    // running the factory on every call.
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

    /// <summary>
    ///     Gets the Host workspace scope used to partition shared state.
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     Gets the concrete view identity of this outlet.
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     Gets the renderer and resource identity.
    /// </summary>
    public ContentReference Reference { get; }

    /// <summary>
    ///     Gets the immutable item at the rendered revision.
    /// </summary>
    public DockItem Item { get; }

    /// <summary>
    ///     Gets the commands bound to this item.
    /// </summary>
    public AtlasContentCommands Commands { get; }

    /// <summary>
    ///     Gets the per-item business state resolved for
    ///     <see cref="Reference"/>, or <see langword="null"/> when no state
    ///     has been created or no state scope is available. Queries never
    ///     create state.
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
    ///     Gets the per-item business state resolved for
    ///     <see cref="Reference"/>, creating it through
    ///     <paramref name="factory"/> when needed. Without a state scope the
    ///     factory runs on every call (backward-compatible fallback).
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

/// <summary>
///     Base class for content resolved through the Atlas content providers;
///     the Content parameter is injected automatically by the outlet for
///     both DI and Map sources, so derived components never fetch it
///     themselves.
/// </summary>
public abstract class AtlasContentComponent : ComponentBase
{
    /// <summary>
    ///     The item context injected by the outlet at render time;
    ///     [EditorRequired] marks it as supplied by the Atlas render pipeline
    ///     rather than an optional parameter.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public AtlasContentContext Content { get; set; } = null!;
}

/// <summary>
///     Provides an immutable context for group empty states.
/// </summary>
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

    /// <summary>
    ///     Gets the workspace identifier.
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     Gets the empty group identifier.
    /// </summary>
    public LayoutNodeId GroupId { get; }

    /// <summary>
    ///     Gets the group item kind.
    /// </summary>
    public DockItemKind GroupKind { get; }

    /// <summary>
    ///     Gets the group retention policy.
    /// </summary>
    public GroupRetentionPolicy RetentionPolicy { get; }
}

/// <summary>
///     Provides immutable item data to header templates.
/// </summary>
public sealed class AtlasItemHeaderContext
{
    internal AtlasItemHeaderContext(DockItem item, bool isSelected)
    {
        Item = item;
        IsSelected = isSelected;
    }

    /// <summary>
    ///     Gets the rendered item.
    /// </summary>
    public DockItem Item { get; }

    /// <summary>
    ///     Gets whether the item is selected in its group.
    /// </summary>
    public bool IsSelected { get; }
}
