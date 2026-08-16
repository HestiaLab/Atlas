using Atlas.Blazor.Components;
using Atlas.Blazor.Content;
using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Persistence;
using Atlas.Core.Placement;
using Microsoft.AspNetCore.Components;

namespace Atlas.Showcase.Components;

/// <summary>
/// Describes one showcase tool panel for the top-bar quick menu.
/// </summary>
public sealed record ShowcasePanelEntry(
    string Key,
    string Title,
    DockItemId ItemId,
    LayoutNodeId GroupId,
    LogicalRegion Region);

public sealed partial class ShowcaseWorkspace : IAsyncDisposable
{
    private IAtlasWorkspace? workspace;
    private string operationStatus = "Ready";
    private string? persistenceEvent;
    private readonly AtlasXmlLayoutSerializer serializer = new();

    [Inject]
    private IAtlasWorkspaceFactory Factory { get; set; } = null!;

    [Parameter]
    public string WorkspaceName { get; set; } = "atlas-showcase";

    /// <summary>Fixed panel catalog for the quick menu (hide/move actions).</summary>
    internal static readonly ShowcasePanelEntry[] Panels =
    {
        new("catalog", "Teaching Catalog", new DockItemId("showcase-catalog"), new LayoutNodeId("left-upper"), LogicalRegion.InlineStartUpper),
        new("layout", "Declarative Layout", new DockItemId("showcase-layout"), new LayoutNodeId("left-lower"), LogicalRegion.InlineStartLower),
        new("content", "Content Registration", new DockItemId("showcase-content"), new LayoutNodeId("right-upper"), LogicalRegion.InlineEndUpper),
        new("dragdrop", "Drag & Dock", new DockItemId("showcase-dragdrop"), new LayoutNodeId("right-upper"), LogicalRegion.InlineEndUpper),
        new("keyboard", "Keyboard", new DockItemId("showcase-keyboard"), new LayoutNodeId("right-lower"), LogicalRegion.InlineEndLower),
        new("splitter", "Splitter", new DockItemId("showcase-splitter"), new LayoutNodeId("bottom-left"), LogicalRegion.BlockEndInlineStart),
        new("theme", "Theming", new DockItemId("showcase-theme"), new LayoutNodeId("bottom-left"), LogicalRegion.BlockEndInlineStart),
        new("persistence", "Persistence", new DockItemId("showcase-persistence"), new LayoutNodeId("bottom-right"), LogicalRegion.BlockEndInlineEnd),
    };

    internal static readonly ShowcaseCatalogItem[] CatalogItems =
    {
        new("catalog", "Teaching Catalog", "01"),
        new("layout", "Declarative Layout", "02"),
        new("content", "Content Registration", "03"),
        new("dragdrop", "Drag & Dock", "04"),
        new("splitter", "Splitter", "05"),
        new("keyboard", "Keyboard", "06"),
        new("theme", "Theming", "07"),
        new("persistence", "Persistence", "08"),
    };

    protected override void OnInitialized()
    {
        var result = Factory.Create(ShowcaseWorkspaceDefinition.Create(WorkspaceName));
        if (!result.Succeeded || result.Value is null)
        {
            throw new InvalidOperationException(result.Error?.DiagnosticId ?? "Atlas Showcase initialization failed.");
        }

        workspace = result.Value;
        workspace.Changed += OnWorkspaceChanged;
    }

    private bool IsPanelVisible(ShowcasePanelEntry panel)
    {
        if (workspace is null)
        {
            return false;
        }

        return workspace.Current.Nodes.TryGetValue(panel.GroupId, out var node)
            && node is GroupNode group
            && group.Visibility == GroupVisibility.Expanded
            && group.ItemIds.Contains(panel.ItemId);
    }

    private async Task TogglePanelAsync(ShowcasePanelEntry panel)
    {
        if (workspace is null)
        {
            return;
        }

        var target = IsPanelVisible(panel) ? GroupVisibility.Collapsed : GroupVisibility.Expanded;
        await RunOperationAsync(
            new SetGroupVisibilityOperation(panel.GroupId, target),
            $"{(target == GroupVisibility.Expanded ? "Showed" : "Hidden")} {panel.Title}");
    }

    private async Task MovePanelAsync(ShowcasePanelEntry panel, LogicalRegion targetRegion)
    {
        if (workspace is null)
        {
            return;
        }

        // Map the destination region to the owning tool group.
        var targetGroupId = RegionToGroupId(targetRegion);
        if (targetGroupId is null || targetGroupId.Value == panel.GroupId)
        {
            return;
        }

        await RunOperationAsync(
            new MoveToolOperation(panel.ItemId, targetGroupId.Value),
            $"Moved {panel.Title} to {targetRegion}");
    }

    private static LayoutNodeId? RegionToGroupId(LogicalRegion region) => region switch
    {
        LogicalRegion.InlineStartUpper => new LayoutNodeId("left-upper"),
        LogicalRegion.InlineStartLower => new LayoutNodeId("left-lower"),
        LogicalRegion.InlineEndUpper => new LayoutNodeId("right-upper"),
        LogicalRegion.InlineEndLower => new LayoutNodeId("right-lower"),
        LogicalRegion.BlockEndInlineStart => new LayoutNodeId("bottom-left"),
        LogicalRegion.BlockEndInlineEnd => new LayoutNodeId("bottom-right"),
        _ => null,
    };

    private async Task RunOperationAsync(LayoutOperation operation, string successMessage)
    {
        if (workspace is null)
        {
            return;
        }

        var result = await workspace.ExecuteAsync(operation);
        operationStatus = result.Succeeded ? successMessage : $"No change: {result.Error?.DiagnosticId ?? "unknown"}";
        await InvokeAsync(StateHasChanged);
    }

    private async Task<string?> CaptureLayoutAsync()
    {
        if (workspace is null)
        {
            return null;
        }

        var xml = serializer.Serialize(workspace.Current);
        persistenceEvent = "Layout captured to XML";
        await InvokeAsync(StateHasChanged);
        return xml;
    }

    private async Task RestoreLayoutAsync(string xml)
    {
        if (workspace is null)
        {
            return;
        }

        var deserialize = serializer.Deserialize(xml, workspace.Id);
        if (!deserialize.Succeeded || deserialize.Value is null)
        {
            persistenceEvent = $"Deserialize failed: {deserialize.Error?.DiagnosticId ?? "unknown"}";
            await InvokeAsync(StateHasChanged);
            return;
        }

        var result = await workspace.ExecuteAsync(new RestoreWorkspaceOperation(deserialize.Value));
        persistenceEvent = result.Succeeded
            ? "Layout restored"
            : $"Restore failed: {result.Error?.DiagnosticId ?? "unknown"}";
        await InvokeAsync(StateHasChanged);
    }

    private async Task UndoAsync()
    {
        if (workspace is null)
        {
            return;
        }

        var result = await workspace.UndoAsync();
        operationStatus = result.Succeeded ? "Undid last change" : "Nothing to undo";
        await InvokeAsync(StateHasChanged);
    }

    private async Task RedoAsync()
    {
        if (workspace is null)
        {
            return;
        }

        var result = await workspace.RedoAsync();
        operationStatus = result.Succeeded ? "Redid last change" : "Nothing to redo";
        await InvokeAsync(StateHasChanged);
    }

    private void OnWorkspaceChanged(object? sender, WorkspaceChangedEventArgs args)
    {
        operationStatus = $"Revision {args.ChangeSet.AfterRevision} — {args.ChangeSet.ChangeKind}";
        _ = InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        if (workspace is null)
        {
            return;
        }

        workspace.Changed -= OnWorkspaceChanged;
        await workspace.DisposeAsync();
        workspace = null;
    }
}
