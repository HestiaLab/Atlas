using Atlas.Blazor.Components;
using Atlas.Blazor.DependencyInjection;
using Atlas.Core;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Showcase.Components;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Showcase.Tests;

public sealed class ShowcaseWorkspaceTests : BunitContext
{
    public ShowcaseWorkspaceTests()
    {
        Services.AddAtlasWorkspace();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Panel_metadata_is_unique_and_maps_every_group()
    {
        Assert.Equal(8, ShowcasePanels.All.Count);
        Assert.Equal(8, ShowcasePanels.CatalogOrder.Count);
        Assert.Equal(8, ShowcasePanels.All.Select(panel => panel.Key).Distinct().Count());
        Assert.Equal(8, ShowcasePanels.All.Select(panel => panel.ItemId).Distinct().Count());
        Assert.Equal(
            new[] { "01", "02", "03", "04", "05", "06", "07", "08" },
            ShowcasePanels.CatalogOrder.Select(panel => panel.Index));

        foreach (var panel in ShowcasePanels.All)
        {
            Assert.Contains(panel.DefaultGroupId, ShowcasePanels.Groups.Select(group => group.Id));
            Assert.Contains(panel.Region, ShowcasePanels.Groups.Select(group => group.Region));
            Assert.Contains(panel.ContentKind, ShowcaseContentKinds.All);
        }

        Assert.All(
            ShowcasePanels.Groups,
            group => Assert.NotEmpty(ShowcasePanels.ForGroup(group.Id)));
        Assert.Equal(9, ShowcaseContentKinds.All.Count);
        Assert.Equal(2, ShowcaseDocuments.All.Count);
        Assert.All(
            ShowcaseDocuments.All,
            document => Assert.Equal(ShowcaseContentKinds.Guide, document.ContentKind));
    }

    [Fact]
    public void Standard_definition_contains_all_panels_and_documents()
    {
        var definition = ShowcaseWorkspaceDefinition.Create("standard-test");

        Assert.Equal(10, definition.Items.Count);
        Assert.Equal(7, definition.Nodes.OfType<GroupNode>().Count());
        Assert.Equal(
            8,
            definition.Items.Count(item => item.Kind == DockItemKind.Tool));
        Assert.Equal(
            2,
            definition.Items.Count(item => item.Kind == DockItemKind.Document));
        Assert.Equal(
            2,
            definition.Items.Count(item => item.Content.Kind == ShowcaseContentKinds.Guide));
        Assert.Equal(
            2,
            definition.Items
                .Where(item => item.Content.Kind == ShowcaseContentKinds.Guide)
                .Select(item => item.Content.ResourceId)
                .Distinct()
                .Count());
    }

    [Fact]
    public void Compact_definition_uses_one_tool_group_and_both_documents()
    {
        var definition = ShowcaseWorkspaceDefinition.Create("compact-test", compact: true);

        var groups = definition.Nodes.OfType<GroupNode>().ToArray();
        Assert.Equal(2, groups.Length);
        var toolGroup = Assert.Single(
            groups,
            group => group.Kind == DockItemKind.Tool);
        Assert.Equal("compact-tools", toolGroup.Id.Value);
        Assert.Equal(8, toolGroup.ItemIds.Count);
        Assert.Single(groups, group => group.Kind == DockItemKind.Document);
        Assert.Equal(10, definition.Items.Count);
    }

    [Fact]
    public void Catalog_row_activates_panel_and_reports_status()
    {
        var cut = Render<ShowcaseWorkspace>();

        cut.WaitForAssertion(
            () => Assert.Equal(8, cut.FindAll(".showcase-list-button").Count));

        cut.FindAll(".showcase-list-button")
            .Single(button => button.TextContent.Contains("内容注册", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(
            () => Assert.Contains(
                "已打开 内容注册",
                cut.Find(".showcase-statusbar").TextContent,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task External_layout_change_reports_status_and_command_message_wins()
    {
        var cut = Render<ShowcaseWorkspace>();
        cut.WaitForAssertion(
            () => Assert.Equal(8, cut.FindAll(".showcase-list-button").Count));

        var workspace = cut.FindComponent<AtlasWorkspaceHost>().Instance.Workspace;
        Assert.NotNull(workspace);

        await workspace!.ExecuteAsync(new SelectItemOperation(new DockItemId("showcase-splitter")), Xunit.TestContext.Current.CancellationToken);

        cut.WaitForAssertion(
            () => Assert.Contains(
                "布局已更新",
                cut.Find(".showcase-statusbar").TextContent,
                StringComparison.Ordinal));

        cut.Find("button[aria-label='撤销布局更改']").Click();

        cut.WaitForAssertion(
            () =>
            {
                var status = cut.Find(".showcase-statusbar").TextContent;
                Assert.Contains("已撤销上次更改", status, StringComparison.Ordinal);
                Assert.DoesNotContain("布局已更新", status, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Panel_manager_moves_panel_and_keeps_state_in_sync()
    {
        var cut = Render<ShowcaseWorkspace>();
        cut.WaitForAssertion(
            () => Assert.Equal(8, cut.FindAll(".showcase-list-button").Count));

        cut.Find("button[aria-haspopup=\"dialog\"]").Click();
        cut.WaitForAssertion(
            () => Assert.Equal(8, cut.FindAll(".showcase-panel-manager-row").Count));

        var firstRow = cut.FindAll(".showcase-panel-manager-row")[0];
        firstRow.QuerySelector("select")!.Change("right-lower");

        cut.WaitForAssertion(
            () => Assert.Contains(
                "已将 教学目录 移动到右下",
                cut.Find(".showcase-statusbar").TextContent,
                StringComparison.Ordinal));

        cut.Find("button[aria-haspopup=\"dialog\"]").Click();
        cut.WaitForAssertion(
            () => Assert.Equal(8, cut.FindAll(".showcase-panel-manager-row").Count));
        var movedRow = cut.FindAll(".showcase-panel-manager-row")[0];
        Assert.Contains("当前显示", movedRow.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "右下",
            movedRow.QuerySelectorAll("option").Select(option => option.TextContent));
    }

    [Fact]
    public async Task Panel_manager_closes_on_escape()
    {
        var cut = Render<ShowcaseWorkspace>();
        cut.WaitForAssertion(
            () => Assert.Equal(8, cut.FindAll(".showcase-list-button").Count));

        cut.Find("button[aria-haspopup=\"dialog\"]").Click();
        cut.WaitForAssertion(
            () => Assert.Single(cut.FindAll(".showcase-panel-manager")));

        await cut.Find(".showcase-panel-manager").TriggerEventAsync(
            "onkeydown",
            new KeyboardEventArgs { Key = "Escape" });

        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll(".showcase-panel-manager")));
    }

    [Fact]
    public void Persistence_capture_enables_restore()
    {
        var cut = Render<ShowcaseWorkspace>();
        cut.WaitForAssertion(
            () => Assert.Equal(8, cut.FindAll(".showcase-list-button").Count));

        var capture = cut.FindAll("button")
            .Single(button => button.TextContent.Trim() == "捕获");
        var restore = cut.FindAll("button")
            .Single(button => button.TextContent.Trim() == "恢复");
        Assert.True(restore.HasAttribute("disabled"));

        capture.Click();
        cut.WaitForAssertion(
            () => Assert.False(
                cut.FindAll("button")
                    .Single(button => button.TextContent.Trim() == "恢复")
                    .HasAttribute("disabled")));

        cut.FindAll("button")
            .Single(button => button.TextContent.Trim() == "恢复")
            .Click();
        cut.WaitForAssertion(
            () => Assert.Contains(
                "布局已恢复",
                cut.Find(".showcase-statusbar").TextContent,
                StringComparison.Ordinal));
    }
}
