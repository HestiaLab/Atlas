#nullable enable
using Atlas.Blazor.Components;
using Atlas.Blazor.DependencyInjection;
using Atlas.Core;
using Atlas.Core.Definitions;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class TabOverflowTests : TestContext
{
    public TabOverflowTests()
    {
        Services.AddAtlasWorkspace();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Scroll_mode_is_the_default_and_has_no_overflow_menu()
    {
        await using var workspace = CreateWorkspace(EditorOverflowMode.Scroll);
        var cut = RenderHost(workspace);

        var strip = cut.Find(".atlas-v2-tab-strip");
        Assert.Equal("scroll", strip.GetAttribute("data-atlas-overflow-mode"));
        Assert.Empty(cut.FindAll("[data-atlas-overflow-menu-button]"));
    }

    [Fact]
    public async Task Menu_mode_exposes_button_that_lists_and_activates_tabs()
    {
        await using var workspace = CreateWorkspace(EditorOverflowMode.Menu);
        var cut = RenderHost(workspace);

        var strip = cut.Find(".atlas-v2-tab-strip");
        Assert.Equal("menu", strip.GetAttribute("data-atlas-overflow-mode"));
        var button = cut.Find("[data-atlas-overflow-menu-button]");
        Assert.Equal("menu", button.GetAttribute("aria-haspopup"));
        Assert.Equal("false", button.GetAttribute("aria-expanded"));
        Assert.False(string.IsNullOrWhiteSpace(button.GetAttribute("aria-controls")));
        Assert.Empty(cut.FindAll("[data-atlas-tab-menu]"));

        button.Click();

        Assert.Equal("true", button.GetAttribute("aria-expanded"));
        var menu = cut.Find("[data-atlas-tab-menu]");
        Assert.Equal("menu", menu.GetAttribute("role"));
        var items = cut.FindAll("[data-atlas-tab-menu-item]");
        Assert.Equal(3, items.Count);
        Assert.Equal(
            new[] { "first", "second", "third" },
            items.Select(item => item.GetAttribute("data-atlas-item-id")));
        Assert.All(items, item => Assert.Equal("menuitem", item.GetAttribute("role")));

        items[1].Click();

        cut.WaitForAssertion(
            () =>
            {
                Assert.Equal(
                    "true",
                    cut.FindAll("[data-atlas-drag-kind=document-tab]")[1]
                        .GetAttribute("aria-selected"));
                Assert.Empty(cut.FindAll("[data-atlas-tab-menu]"));
                Assert.Equal("false", cut.Find("[data-atlas-overflow-menu-button]")
                    .GetAttribute("aria-expanded"));
            });
    }

    [Fact]
    public async Task Wrap_mode_marks_the_strip_and_has_no_overflow_menu()
    {
        await using var workspace = CreateWorkspace(EditorOverflowMode.Wrap);
        var cut = RenderHost(workspace);

        var strip = cut.Find(".atlas-v2-tab-strip");
        Assert.Equal("wrap", strip.GetAttribute("data-atlas-overflow-mode"));
        Assert.Empty(cut.FindAll("[data-atlas-overflow-menu-button]"));
    }

    [Fact]
    public async Task Context_menu_closes_when_the_backdrop_is_clicked()
    {
        await using var workspace = CreateWorkspace(EditorOverflowMode.Scroll);
        var cut = RenderHost(workspace);

        OpenContextMenu(cut, 0, 30, 30);
        Assert.Single(cut.FindAll("[data-atlas-tab-context-menu]"));

        cut.Find("[data-atlas-popup-backdrop]").Click();

        cut.WaitForAssertion(
            () => Assert.Empty(cut.FindAll("[data-atlas-tab-context-menu]")));
    }

    [Fact]
    public async Task Context_menu_exposes_four_close_commands_for_a_closable_tab()
    {
        await using var workspace = CreateWorkspace(EditorOverflowMode.Scroll);
        var cut = RenderHost(workspace);

        OpenContextMenu(cut, 0, 120, 80);

        var menu = cut.Find("[data-atlas-tab-context-menu]");
        Assert.Equal("menu", menu.GetAttribute("role"));
        Assert.Contains("120px", menu.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("80px", menu.GetAttribute("style"), StringComparison.Ordinal);
        var commands = cut.FindAll("[data-atlas-menu-command]");
        Assert.Equal(
            new[] { "close", "close-others", "close-right", "close-all" },
            commands.Select(command => command.GetAttribute("data-atlas-menu-command")));
        Assert.All(commands, command => Assert.False(command.HasAttribute("disabled")));

        menu.TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "Escape", Code = "Escape" });

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-atlas-tab-context-menu]")));
    }

    [Fact]
    public async Task Context_menu_disables_close_commands_for_a_non_closable_tab()
    {
        await using var workspace = CreateWorkspace(
            EditorOverflowMode.Scroll,
            CreateDocument("first", "First", canClose: true),
            CreateDocument("second", "Second", canClose: false),
            CreateDocument("third", "Third", canClose: true));
        var cut = RenderHost(workspace);

        OpenContextMenu(cut, 1, 40, 40);

        var commands = cut.FindAll("[data-atlas-menu-command]");
        Assert.Equal(4, commands.Count);
        Assert.All(commands, command => Assert.True(command.HasAttribute("disabled")));
    }

    [Fact]
    public async Task Close_others_keeps_the_target_and_non_closable_tabs()
    {
        await using var workspace = CreateWorkspace(
            EditorOverflowMode.Scroll,
            CreateDocument("first", "First", canClose: true),
            CreateDocument("second", "Second", canClose: true),
            CreateDocument("keep", "Keep", canClose: false));
        var cut = RenderHost(workspace);

        OpenContextMenu(cut, 0, 20, 20);
        cut.Find("[data-atlas-menu-command=close-others]").Click();

        cut.WaitForAssertion(
            () =>
            {
                var remaining = cut.FindAll("[data-atlas-drag-kind=document-tab]")
                    .Select(tab => tab.GetAttribute("data-atlas-item-id"))
                    .ToArray();
                Assert.Equal(new[] { "first", "keep" }, remaining);
                Assert.Empty(cut.FindAll("[data-atlas-tab-context-menu]"));
            });
    }

    [Fact]
    public async Task Close_right_closes_only_tabs_to_the_right_of_the_target()
    {
        await using var workspace = CreateWorkspace(
            EditorOverflowMode.Scroll,
            CreateDocument("first", "First", canClose: true),
            CreateDocument("second", "Second", canClose: true),
            CreateDocument("third", "Third", canClose: true));
        var cut = RenderHost(workspace);

        OpenContextMenu(cut, 0, 20, 20);
        cut.Find("[data-atlas-menu-command=close-right]").Click();

        cut.WaitForAssertion(
            () =>
            {
                var remaining = cut.FindAll("[data-atlas-drag-kind=document-tab]")
                    .Select(tab => tab.GetAttribute("data-atlas-item-id"))
                    .ToArray();
                Assert.Equal(new[] { "first" }, remaining);
            });
    }

    [Fact]
    public async Task Close_all_closes_every_closable_tab_and_leaves_empty_state()
    {
        await using var workspace = CreateWorkspace(
            EditorOverflowMode.Scroll,
            CreateDocument("first", "First", canClose: true),
            CreateDocument("second", "Second", canClose: true));
        var cut = RenderHost(workspace);

        OpenContextMenu(cut, 1, 20, 20);
        cut.Find("[data-atlas-menu-command=close-all]").Click();

        cut.WaitForAssertion(
            () =>
            {
                Assert.Empty(cut.FindAll("[data-atlas-drag-kind=document-tab]"));
                Assert.Contains(
                    "No document content",
                    cut.Find(".atlas-v2-empty-state").TextContent,
                    StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task Tab_overflow_and_context_menu_contracts_match_the_dom_manifest()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "dom-metadata-manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        await using var workspace = CreateWorkspace(EditorOverflowMode.Menu);
        var cut = RenderHost(workspace);

        AssertManifestContract(cut, manifest, "tabStrip");
        AssertManifestContract(cut, manifest, "tabOverflowButton");

        cut.Find("[data-atlas-overflow-menu-button]").Click();
        AssertManifestContract(cut, manifest, "tabMenu");
        cut.Find("[data-atlas-tab-menu]")
            .TriggerEvent("onkeydown", new KeyboardEventArgs { Key = "Escape", Code = "Escape" });

        OpenContextMenu(cut, 0, 10, 10);
        AssertManifestContract(cut, manifest, "tabContextMenu");
    }

    private static void AssertManifestContract(
        IRenderedFragment cut,
        JsonDocument manifest,
        string contractName)
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

    private static void OpenContextMenu(
        IRenderedFragment cut,
        int tabIndex,
        double x,
        double y)
    {
        cut.FindAll("[data-atlas-drag-kind=document-tab]")[tabIndex]
            .TriggerEvent(
                "oncontextmenu",
                new MouseEventArgs { ClientX = x, ClientY = y });
    }

    private IAtlasWorkspace CreateWorkspace(
        EditorOverflowMode mode,
        params DockItem[] items)
    {
        var materialized = items.Length == 0
            ? new[]
            {
                CreateDocument("first", "First", canClose: true),
                CreateDocument("second", "Second", canClose: true),
                CreateDocument("third", "Third", canClose: true),
            }
            : items;
        var group = new GroupNode(
            new LayoutNodeId("documents"),
            DockItemKind.Document,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            materialized.Select(item => item.Id),
            materialized[0].Id,
            new EditorGroupState(mode));
        var definition = new AtlasWorkspaceDefinition(
            new WorkspaceId($"overflow-{mode}-{Guid.NewGuid():N}"),
            group.Id,
            new LayoutNode[] { group },
            materialized);
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

    private static DockItem CreateDocument(
        string id,
        string title,
        bool canClose)
    {
        var capabilities = DockItemCapabilities.CanMove
            | DockItemCapabilities.CanSplit
            | DockItemCapabilities.CanPinDocument;
        if (canClose)
        {
            capabilities |= DockItemCapabilities.CanClose;
        }

        return new DockItem(
            new DockItemId(id),
            DockItemKind.Document,
            new ContentReference("document", $"{id}-resource"),
            capabilities,
            title);
    }
}
