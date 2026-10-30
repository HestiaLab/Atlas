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
using Atlas.Core.Placement;
using Bunit;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class ContentLifetimeSpikeTests : BunitContext
{
    public ContentLifetimeSpikeTests()
    {
        Services.AddAtlasWorkspace();
        Services.AddAtlasContent<StatefulToolContent>("stateful-tool");
        JSInterop.Mode = JSRuntimeMode.Loose;
        StatefulToolContent.Reset();
    }

    [Fact]
    public async Task Cross_group_move_remounts_component_but_restores_external_business_state()
    {
        await using var workspace = CreateWorkspace();
        var cut = Render<AtlasWorkspaceHost>(
            parameters => parameters
                .Add(component => component.Workspace, workspace));
        Assert.True(StatefulToolContent.Created == 1, cut.Markup);
        StatefulToolContent.BusinessState["resource"] = 42;

        var result = await workspace.ExecuteAsync(new MoveToolOperation(
                new DockItemId("tool"),
                new LayoutNodeId("target")), Xunit.TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(2, StatefulToolContent.Created);
                Assert.Equal(1, StatefulToolContent.Disposed);
                Assert.Equal("42", cut.Find(".stateful-tool").TextContent);
            });
    }

    private static IAtlasWorkspace CreateWorkspace()
    {
        var item = new DockItem(
            new DockItemId("tool"),
            DockItemKind.Tool,
            new ContentReference("stateful-tool", "resource"),
            DockItemCapabilityPresets.Tool);
        var source = new GroupNode(
            new LayoutNodeId("source"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            new[] { item.Id },
            item.Id,
            toolbarRegion: LogicalRegion.InlineStartUpper);
        var target = new GroupNode(
            new LayoutNodeId("target"),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            Array.Empty<DockItemId>(),
            selectedItemId: null,
            toolbarRegion: LogicalRegion.InlineEndUpper);
        var root = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5),
            new SplitConstraints(),
            source.Id,
            target.Id);
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId("lifetime-spike"),
            root.Id,
            new LayoutNode[] { root, source, target },
            new[] { item },
            toolBars: new[]
            {
                new ToolBarState(LogicalSide.InlineStart, ToolBarVisibility.Expanded),
                new ToolBarState(LogicalSide.InlineEnd, ToolBarVisibility.Expanded),
            },
            toolBarOrders: new[]
            {
                new ToolBarOrderState(LogicalRegion.InlineStartUpper, new[] { item.Id }),
                new ToolBarOrderState(LogicalRegion.InlineEndUpper, Array.Empty<DockItemId>()),
            });
        var result = new AtlasWorkspaceFactory().Create(definition);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private sealed class StatefulToolContent : AtlasContentComponent, IDisposable
    {
        internal static Dictionary<string, int> BusinessState { get; } =
            new(StringComparer.Ordinal);

        internal static int Created { get; private set; }

        internal static int Disposed { get; private set; }

        protected override void OnInitialized()
        {
            Created++;
            BusinessState.TryAdd(Content.Reference.ResourceId, 0);
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "stateful-tool");
            builder.AddContent(2, BusinessState[Content.Reference.ResourceId]);
            builder.CloseElement();
        }

        public void Dispose()
        {
            Disposed++;
        }

        internal static void Reset()
        {
            Created = 0;
            Disposed = 0;
            BusinessState.Clear();
        }
    }
}
