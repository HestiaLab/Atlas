#nullable enable
using Atlas.Blazor.Components;
using Atlas.Blazor.Content;
using Atlas.Blazor.DependencyInjection;
using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Bunit;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class ContentLifetimeTests : BunitContext
{
    private readonly ContentTracker tracker = new();

    public ContentLifetimeTests()
    {
        Services.AddAtlasWorkspace();
        Services.AddAtlasContent<TrackedContent>("tracked");
        Services.AddSingleton(tracker);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Default_unmounts_inactive_content()
    {
        await using var workspace = CreateWorkspace();
        var cut = RenderHost(workspace, options: null);

        Assert.Equal(1, tracker.Created);

        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("second")));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, tracker.Created);
            Assert.Equal(1, tracker.Disposed);
            Assert.Single(cut.FindAll(".atlas-v2-content-outlet"));
        });

        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("first")));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, tracker.Created);
            Assert.Equal(2, tracker.Disposed);
        });
    }

    [Fact]
    public async Task Keep_alive_within_group_preserves_instances_and_inerts_inactive_outlet()
    {
        await using var workspace = CreateWorkspace();
        var cut = RenderHost(
            workspace,
            new WorkspaceContentLifetimeOptions(
                AtlasContentLifetimeMode.KeepAliveWithinGroup,
                maxCachedItems: 2));

        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("second")));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, tracker.Created);
            Assert.Equal(0, tracker.Disposed);
            Assert.Equal(2, cut.FindAll(".atlas-v2-content-outlet").Count);
            var inactive = cut.Find("[data-atlas-item-id='first'].atlas-v2-content-outlet");
            Assert.True(inactive.HasAttribute("hidden"));
            Assert.Equal("true", inactive.GetAttribute("aria-hidden"));
            Assert.True(inactive.HasAttribute("inert"));
        });

        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("first")));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, tracker.Created);
            Assert.Equal(0, tracker.Disposed);
            var active = cut.Find("[data-atlas-item-id='first'].atlas-v2-content-outlet");
            Assert.False(active.HasAttribute("hidden"));
            Assert.False(active.HasAttribute("aria-hidden"));
            Assert.False(active.HasAttribute("inert"));
        });
    }

    [Fact]
    public async Task Keep_alive_cache_evicts_least_recent_inactive_instance_at_bound()
    {
        await using var workspace = CreateWorkspace();
        var cut = RenderHost(
            workspace,
            new WorkspaceContentLifetimeOptions(
                AtlasContentLifetimeMode.KeepAliveWithinGroup,
                maxCachedItems: 1));

        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("second")));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, tracker.Created);
            Assert.Equal(1, tracker.Disposed);
            Assert.Single(cut.FindAll(".atlas-v2-content-outlet"));
            Assert.Equal(
                "second",
                cut.Find(".atlas-v2-content-outlet").GetAttribute("data-atlas-item-id"));
        });
    }

    [Fact]
    public async Task Closing_cached_item_disposes_its_instance_once_without_remounting_active_item()
    {
        await using var workspace = CreateWorkspace();
        var cut = RenderHost(
            workspace,
            new WorkspaceContentLifetimeOptions(
                AtlasContentLifetimeMode.KeepAliveWithinGroup,
                maxCachedItems: 2));
        await workspace.ExecuteAsync(new SelectItemOperation(new DockItemId("second")));
        cut.WaitForAssertion(() => Assert.Equal(2, tracker.Created));

        var close = await workspace.ExecuteAsync(new CloseItemOperation(new DockItemId("first")));

        Assert.True(close.Succeeded);
        cut.WaitForAssertion(
            () => Assert.Equal((Created: 2, Disposed: 1),
                (tracker.Created, tracker.Disposed)));
    }

    [Fact]
    public void Cache_bound_must_be_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorkspaceContentLifetimeOptions(
                AtlasContentLifetimeMode.KeepAliveWithinGroup,
                maxCachedItems: 0));
    }

    private IRenderedComponent<AtlasWorkspaceHost> RenderHost(
        IAtlasWorkspace workspace,
        WorkspaceContentLifetimeOptions? options)
    {
        return Render<AtlasWorkspaceHost>(parameters =>
        {
            parameters
                .Add(component => component.Workspace, workspace);
            if (options is not null)
            {
                parameters.Add(component => component.ContentLifetimeOptions, options);
            }
        });
    }

    private static IAtlasWorkspace CreateWorkspace()
    {
        var first = Item("first");
        var second = Item("second");
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { first.Id, second.Id },
            first.Id);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("content-lifetime"),
            group.Id,
            new LayoutNode[] { group },
            new[] { first, second });
        var result = new AtlasWorkspaceFactory().Create(definition);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static DockItem Item(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("tracked", $"resource-{id}"),
            DockItemCapabilityPresets.Document);
    }

    private sealed class TrackedContent : AtlasContentComponent, IDisposable
    {
        [Microsoft.AspNetCore.Components.Inject]
        private ContentTracker Tracker { get; set; } = null!;

        protected override void OnInitialized()
        {
            Tracker.Created++;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, Content.ItemId.Value);
        }

        public void Dispose()
        {
            Tracker.Disposed++;
        }
    }

    private sealed class ContentTracker
    {
        internal int Created { get; set; }

        internal int Disposed { get; set; }
    }
}
