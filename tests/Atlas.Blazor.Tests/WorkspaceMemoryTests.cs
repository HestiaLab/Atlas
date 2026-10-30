#nullable enable
using Atlas.Blazor.Components;
using Atlas.Blazor.Content;
using Atlas.Blazor.DependencyInjection;
using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Placement;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class WorkspaceMemoryTests : BunitContext
{
    private readonly MemoryTracker tracker = new();

    public WorkspaceMemoryTests()
    {
        Services.AddAtlasWorkspace();
        Services.AddAtlasContent<MemoryProbeContent>("memory");
        Services.AddSingleton(tracker);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Closing_a_document_tab_disposes_its_content_and_unsubscribes_events()
    {
        var first = Document("first", "First");
        var second = Document("second", "Second");
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { first.Id, second.Id },
            first.Id,
            new EditorGroupState());
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { first, second });
        var cut = RenderHost(workspace);

        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(1, tracker.Created);
                Assert.Equal(1, tracker.SubscriptionCount);
                Assert.Equal(2, cut.FindAll("[data-atlas-drag-kind=document-tab]").Count);
            });
        var nodesBefore = cut.FindAll("*").Count;

        var close = await workspace.ExecuteAsync(new CloseItemOperation(first.Id), Xunit.TestContext.Current.CancellationToken);

        Assert.True(close.Succeeded);
        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(1, tracker.Disposed);
                Assert.Equal(2, tracker.Created);
                Assert.Equal(1, tracker.SubscriptionCount);
                Assert.DoesNotContain(
                    "first",
                    cut.FindAll("[data-atlas-drag-kind=document-tab]")
                        .Select(tab => tab.GetAttribute("data-atlas-item-id")));
                Assert.Single(cut.FindAll(".atlas-v2-content-outlet"));
                Assert.True(
                    cut.FindAll("*").Count < nodesBefore,
                    "Closing a tab should remove its DOM subtree.");
            });

        tracker.RaiseChanged();
        Assert.Equal(1, tracker.ReceivedFor("second"));
        Assert.Equal(0, tracker.ReceivedFor("first"));
    }

    [Fact]
    public async Task Closing_a_tool_panel_disposes_its_content_and_toolbar_entry()
    {
        var item = new DockItem(
            new DockItemId("tool"),
            DockItemKind.Tool,
            new ContentReference("memory", "tool-resource"),
            DockItemCapabilityPresets.Tool,
            "Tool");
        var group = new GroupNode(
            new LayoutNodeId("tools"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { item },
            toolBarOrder: new[] { item.Id });
        var cut = RenderHost(workspace);

        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(1, tracker.Created);
                Assert.Equal(1, tracker.SubscriptionCount);
                Assert.Single(cut.FindAll("[data-atlas-toolbar-entry]"));
                Assert.Single(cut.FindAll("[data-atlas-panel-header]"));
            });

        var close = await workspace.ExecuteAsync(new CloseItemOperation(item.Id), Xunit.TestContext.Current.CancellationToken);

        Assert.True(close.Succeeded);
        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(1, tracker.Disposed);
                Assert.Equal(0, tracker.SubscriptionCount);
                Assert.Empty(cut.FindAll("[data-atlas-toolbar-entry]"));
                Assert.Empty(cut.FindAll("[data-atlas-panel-header]"));
                Assert.Empty(cut.FindAll(".atlas-v2-content-outlet"));
            });

        tracker.RaiseChanged();
        Assert.Equal(0, tracker.ReceivedFor("tool"));
    }

    private IAtlasWorkspace CreateWorkspace(
        LayoutNodeId rootNodeId,
        IEnumerable<LayoutNode> nodes,
        IEnumerable<DockItem> items,
        IEnumerable<DockItemId>? toolBarOrder = null)
    {
        var order = toolBarOrder?.ToArray() ?? Array.Empty<DockItemId>();
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId($"memory-{Guid.NewGuid():N}"),
            rootNodeId,
            nodes,
            items,
            toolBars: order.Length == 0
                ? null
                : new[]
                {
                    new ToolBarState(
                        LogicalSide.InlineStart,
                        ToolBarVisibility.Expanded),
                },
            toolBarOrders: order.Length == 0
                ? null
                : new[]
                {
                    new ToolBarOrderState(
                        LogicalRegion.InlineStartUpper,
                        order),
                });
        var result = Services.GetRequiredService<IAtlasWorkspaceFactory>().Create(definition);
        Assert.True(result.Succeeded, result.Error?.DiagnosticId);
        return result.Value!;
    }

    private IRenderedComponent<AtlasWorkspaceHost> RenderHost(IAtlasWorkspace workspace)
    {
        return Render<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.EnableInteractions, false));
    }

    private static DockItem Document(string id, string title)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("memory", $"{id}-resource"),
            DockItemCapabilityPresets.Document,
            title);
    }

    private sealed class MemoryTracker
    {
        private readonly Dictionary<string, int> received = new(StringComparer.Ordinal);

        public int Created { get; private set; }

        public int Disposed { get; private set; }

        public int SubscriptionCount { get; private set; }

        public event Action? Changed;

        public void Subscribe()
        {
            SubscriptionCount++;
        }

        public void Unsubscribe()
        {
            SubscriptionCount--;
        }

        public void RaiseChanged()
        {
            Changed?.Invoke();
        }

        public void RecordCreated()
        {
            Created++;
        }

        public void RecordDisposed()
        {
            Disposed++;
        }

        public void RecordReceived(string itemId)
        {
            received.TryGetValue(itemId, out var count);
            received[itemId] = count + 1;
        }

        public int ReceivedFor(string itemId)
        {
            return received.TryGetValue(itemId, out var count) ? count : 0;
        }
    }

    private sealed class MemoryProbeContent : AtlasContentComponent, IDisposable
    {
        [Inject]
        private MemoryTracker Tracker { get; set; } = null!;

        protected override void OnInitialized()
        {
            Tracker.RecordCreated();
            Tracker.Subscribe();
            Tracker.Changed += OnChanged;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "data-memory-probe", Content.ItemId.Value);
            builder.AddContent(2, Content.ItemId.Value);
            builder.CloseElement();
        }

        public void Dispose()
        {
            Tracker.Changed -= OnChanged;
            Tracker.Unsubscribe();
            Tracker.RecordDisposed();
        }

        private void OnChanged()
        {
            Tracker.RecordReceived(Content.ItemId.Value);
        }
    }
}
