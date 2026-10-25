#nullable enable
using Atlas.Blazor.Components;
using Atlas.Blazor.Content;
using Atlas.Blazor.DependencyInjection;
using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class AutoHideFlyoutTests : TestContext
{
    private readonly ContentTracker tracker = new();

    public AutoHideFlyoutTests()
    {
        Services.AddAtlasWorkspace();
        Services.AddAtlasContent<TrackedAutoHideContent>("tool");
        Services.AddSingleton(tracker);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Auto_hidden_item_renders_as_toolbar_entry_with_hidden_flyout()
    {
        var item = Tool("hidden", "Hidden", iconKey: "pin");
        var group = EmptyToolGroup("left-tools", LogicalRegion.InlineStartUpper);
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { item },
            new[] { group },
            new[]
            {
                new AutoHideStripState(
                    WorkspaceEdge.InlineStart,
                    new[]
                    {
                        new AutoHideEntry(
                            item.Id,
                            new DockBack(
                                group.Id,
                                0,
                                WorkspaceEdge.InlineStart)),
                    }),
            });
        var cut = RenderHost(workspace);

        var entry = cut.Find("[data-atlas-auto-hide-entry]");
        Assert.Equal("Hidden", entry.TextContent.Trim());
        Assert.Equal("false", entry.GetAttribute("aria-expanded"));
        Assert.Equal(group.ToolbarRegion!.Value.ToString(), entry.GetAttribute("data-atlas-toolbar-region"));
        Assert.Equal("pin", cut.Find("[data-atlas-icon=pin]").GetAttribute("data-atlas-icon"));

        var panelId = entry.GetAttribute("aria-controls");
        Assert.False(string.IsNullOrWhiteSpace(panelId));
        var flyout = cut.Find("[data-atlas-auto-hide-flyout]");
        Assert.Equal("true", flyout.GetAttribute("aria-hidden"));
        Assert.True(flyout.HasAttribute("inert"));
        // 初始 render 不挂载内容；首次悬停后才创建出口。
        Assert.Empty(flyout.QuerySelectorAll(".atlas-v2-content-outlet"));
        Assert.Equal(0, tracker.CreatedFor("hidden-resource"));

        cut.Find("[data-atlas-auto-hide-entry]")
            .TriggerEvent("onmouseover", new MouseEventArgs());

        cut.WaitForAssertion(
            () =>
            {
                var outlet = cut.Find("[data-atlas-auto-hide-flyout] .atlas-v2-content-outlet");
                Assert.Equal(panelId, outlet.Id);
                Assert.Equal("hidden", outlet.GetAttribute("data-atlas-item-id"));
                Assert.Equal(1, tracker.CreatedFor("hidden-resource"));
            });
    }

    [Fact]
    public async Task Auto_hidden_content_mounts_once_on_focus_and_stays_after_close()
    {
        var item = Tool("hidden", "Hidden");
        var group = EmptyToolGroup("left-tools", LogicalRegion.InlineStartUpper);
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { item },
            new[] { group },
            new[]
            {
                new AutoHideStripState(
                    WorkspaceEdge.InlineStart,
                    new[]
                    {
                        new AutoHideEntry(
                            item.Id,
                            new DockBack(group.Id, 0, WorkspaceEdge.InlineStart)),
                    }),
            });
        var cut = RenderHost(workspace);
        Assert.Empty(cut.FindAll("[data-atlas-auto-hide-flyout] .atlas-v2-content-outlet"));

        cut.Find("[data-atlas-auto-hide-entry]")
            .TriggerEvent("onfocus", new FocusEventArgs());

        cut.WaitForAssertion(
            () =>
            {
                Assert.Single(cut.FindAll("[data-atlas-auto-hide-flyout] .atlas-v2-content-outlet"));
                Assert.Equal(1, tracker.CreatedFor("hidden-resource"));
            });

        // 收起由控制器切换 data-atlas-auto-hide-open；内容保持挂载。
        cut.Find("[data-atlas-auto-hide-entry]")
            .TriggerEvent("onfocus", new FocusEventArgs());

        Assert.Single(cut.FindAll("[data-atlas-auto-hide-flyout] .atlas-v2-content-outlet"));
        Assert.Equal(1, tracker.CreatedFor("hidden-resource"));
    }

    [Fact]
    public async Task Auto_hidden_content_mounts_per_host()
    {
        await using var first = CreateSingleHiddenWorkspace("first");
        await using var second = CreateSingleHiddenWorkspace("second");
        var firstCut = RenderHost(first);
        var secondCut = RenderHost(second);

        firstCut.Find("[data-atlas-auto-hide-entry]")
            .TriggerEvent("onmouseover", new MouseEventArgs());

        firstCut.WaitForAssertion(
            () => Assert.Equal(1, tracker.CreatedFor("hidden-first-resource")));
        Assert.Equal(0, tracker.CreatedFor("hidden-second-resource"));
        Assert.Empty(secondCut.FindAll("[data-atlas-auto-hide-flyout] .atlas-v2-content-outlet"));

        secondCut.Find("[data-atlas-auto-hide-entry]")
            .TriggerEvent("onmouseover", new MouseEventArgs());
        secondCut.WaitForAssertion(
            () => Assert.Equal(1, tracker.CreatedFor("hidden-second-resource")));

        firstCut.Find("[data-atlas-auto-hide-entry]")
            .TriggerEvent("onmouseover", new MouseEventArgs());
        Assert.Equal(1, tracker.CreatedFor("hidden-first-resource"));
    }

    [Fact]
    public async Task Auto_hidden_entries_render_on_the_toolbar_side_of_their_dock_back_group()
    {
        var leftItem = Tool("left-hidden", "Left hidden");
        var rightItem = Tool("right-hidden", "Right hidden");
        var leftGroup = EmptyToolGroup("left-tools", LogicalRegion.InlineStartUpper);
        var rightGroup = EmptyToolGroup("right-tools", LogicalRegion.InlineEndLower);
        var split = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5),
            new SplitConstraints(),
            leftGroup.Id,
            rightGroup.Id);
        await using var workspace = CreateWorkspace(
            split.Id,
            new LayoutNode[] { split, leftGroup, rightGroup },
            new[] { leftItem, rightItem },
            new[] { leftGroup, rightGroup },
            new[]
            {
                new AutoHideStripState(
                    WorkspaceEdge.InlineStart,
                    new[]
                    {
                        new AutoHideEntry(
                            leftItem.Id,
                            new DockBack(leftGroup.Id, 0, WorkspaceEdge.InlineStart)),
                    }),
                new AutoHideStripState(
                    WorkspaceEdge.InlineEnd,
                    new[]
                    {
                        new AutoHideEntry(
                            rightItem.Id,
                            new DockBack(rightGroup.Id, 0, WorkspaceEdge.InlineEnd)),
                    }),
            });
        var cut = RenderHost(workspace);

        var inlineStart = cut.Find("[data-atlas-toolbar-side=inline-start]");
        Assert.Single(inlineStart.QuerySelectorAll("[data-atlas-auto-hide-entry]"));
        Assert.Equal(
            "left-hidden",
            inlineStart.QuerySelector("[data-atlas-auto-hide-entry]")!
                .GetAttribute("data-atlas-item-id"));
        var inlineEnd = cut.Find("[data-atlas-toolbar-side=inline-end]");
        Assert.Single(inlineEnd.QuerySelectorAll("[data-atlas-auto-hide-entry]"));
        Assert.Equal(
            "right-hidden",
            inlineEnd.QuerySelector("[data-atlas-auto-hide-entry]")!
                .GetAttribute("data-atlas-item-id"));
    }

    [Fact]
    public async Task Auto_hide_contracts_match_the_dom_manifest()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "dom-metadata-manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        var item = Tool("hidden", "Hidden");
        var group = EmptyToolGroup("left-tools", LogicalRegion.InlineStartUpper);
        await using var workspace = CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { item },
            new[] { group },
            new[]
            {
                new AutoHideStripState(
                    WorkspaceEdge.InlineStart,
                    new[]
                    {
                        new AutoHideEntry(
                            item.Id,
                            new DockBack(group.Id, 0, WorkspaceEdge.InlineStart)),
                    }),
            });
        var cut = RenderHost(workspace);

        foreach (var contractName in new[] { "autoHideEntry", "autoHideFlyout" })
        {
            var contract = manifest.RootElement.GetProperty(contractName);
            var element = cut.Find(contract.GetProperty("selector").GetString()!);
            foreach (var attribute in contract
                .GetProperty("requiredAttributes")
                .EnumerateArray())
            {
                Assert.True(
                    element.HasAttribute(attribute.GetString()!),
                    $"{contractName} is missing {attribute.GetString()}.");
            }
        }
    }

    private IAtlasWorkspace CreateSingleHiddenWorkspace(string suffix)
    {
        var item = Tool($"hidden-{suffix}", $"Hidden {suffix}");
        var group = EmptyToolGroup(
            $"left-tools-{suffix}",
            LogicalRegion.InlineStartUpper);
        return CreateWorkspace(
            group.Id,
            new LayoutNode[] { group },
            new[] { item },
            new[] { group },
            new[]
            {
                new AutoHideStripState(
                    WorkspaceEdge.InlineStart,
                    new[]
                    {
                        new AutoHideEntry(
                            item.Id,
                            new DockBack(group.Id, 0, WorkspaceEdge.InlineStart)),
                    }),
            });
    }

    private IAtlasWorkspace CreateWorkspace(
        LayoutNodeId rootNodeId,
        IEnumerable<LayoutNode> nodes,
        IEnumerable<DockItem> items,
        IEnumerable<GroupNode> toolbarGroups,
        IEnumerable<AutoHideStripState> strips)
    {
        var materializedStrips = strips.ToArray();
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId($"auto-hide-{Guid.NewGuid():N}"),
            rootNodeId,
            nodes,
            items,
            toolBars: new[]
            {
                new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
                new ToolBarState(LogicalSide.InlineEnd, ToolBarVisibility.Expanded),
            },
            toolBarOrders: toolbarGroups.Select(
                group => new ToolBarOrderState(
                    group.ToolbarRegion!.Value,
                    group.ItemIds.Concat(
                        materializedStrips
                            .SelectMany(strip => strip.Entries)
                            .Where(entry => entry.DockBack.PreferredGroupId == group.Id)
                            .Select(entry => entry.ItemId)))),
            autoHideStrips: materializedStrips);
        var factory = Services.GetRequiredService<IAtlasWorkspaceFactory>();
        var result = factory.Create(definition);
        Assert.True(result.Succeeded, result.Error?.DiagnosticId);
        return result.Value!;
    }

    private IRenderedFragment RenderHost(IAtlasWorkspace workspace)
    {
        return RenderComponent<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace)
                .Add(component => component.EnableInteractions, false));
    }

    private static DockItem Tool(string id, string title, string? iconKey = null)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference("tool", $"{id}-resource"),
            DockItemCapabilityPresets.Tool,
            title,
            iconKey);
    }

    private static GroupNode EmptyToolGroup(string id, LogicalRegion region)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null,
            toolbarRegion: region);
    }

    private sealed class ContentTracker
    {
        private readonly Dictionary<string, int> created =
            new(StringComparer.Ordinal);

        public void Record(string resourceId)
        {
            created.TryGetValue(resourceId, out var count);
            created[resourceId] = count + 1;
        }

        public int CreatedFor(string resourceId)
        {
            return created.TryGetValue(resourceId, out var count) ? count : 0;
        }
    }

    private sealed class TrackedAutoHideContent : AtlasContentComponent
    {
        [Inject]
        private ContentTracker Tracker { get; set; } = null!;

        protected override void OnInitialized()
        {
            Tracker.Record(Content.Reference.ResourceId);
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(
                0,
                $"AUTO[{Content.Reference.ResourceId}]");
        }
    }
}
