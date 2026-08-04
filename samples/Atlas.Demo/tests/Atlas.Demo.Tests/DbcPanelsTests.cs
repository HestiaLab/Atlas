#nullable enable
using Atlas.Demo.Components;
using Atlas.Demo.Services;
using Atlas.Demo.ViewModels;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Demo.Tests;

public sealed class DbcPanelsTests : TestContext
{
    private const string ValidDbc =
        "VERSION \"\"\nBU_: XXX\n\nBO_ 100 EmptyMsg: 1 XXX\n";

    public DbcPanelsTests()
    {
        Services.AddAtlasDemoServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void TreePanel_renders_folders_and_files_from_store()
    {
        var store = new DbcFileStore();
        store.CreateFolder("can-a");
        store.AddFile("can-a", "engine.dbc", ValidDbc);
        var model = new DbcTreeModel(store);

        var cut = RenderComponent<DbcTreePanel>(p => p.Add(x => x.Model, model));

        Assert.Contains("can-a", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("engine.dbc", cut.Markup, StringComparison.Ordinal);
        Assert.Single(cut.FindAll("[data-testid='dbc-tree']"));
    }

    [Fact]
    public void TreePanel_upload_adds_file_to_store()
    {
        var store = new DbcFileStore();
        var model = new DbcTreeModel(store);

        var cut = RenderComponent<DbcTreePanel>(p => p.Add(x => x.Model, model));

        // Upload targets the default "DBC" folder created on first upload.
        var input = cut.FindComponent<Microsoft.AspNetCore.Components.Forms.InputFile>();
        input.UploadFiles(InputFileContent.CreateFromText(ValidDbc, "engine.dbc"));

        Assert.Single(store.Folders);
        Assert.Single(store.FilesIn("DBC"));
        Assert.Equal("engine.dbc", store.Find("DBC", "engine.dbc")!.Name);
    }

    [Fact]
    public void EditorPanel_switches_between_text_and_table_views()
    {
        var store = new DbcFileStore();
        var model = new DbcEditorModel(store);
        var parsed = DbcParser.Parse(ValidDbc);
        model.Open("can-a", "a.dbc", ValidDbc, parsed);

        var cut = RenderComponent<DbcEditorPanel>(p => p.Add(x => x.Model, model));

        // Table view is the default; the message row renders.
        Assert.Contains("EmptyMsg", cut.Markup, StringComparison.Ordinal);

        cut.FindAll("button")[0].Click(); // Text
        Assert.Contains("VERSION", cut.Markup, StringComparison.Ordinal);

        cut.FindAll("button")[1].Click(); // Table
        Assert.Contains("EmptyMsg", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void EditorPanel_save_updates_store_content()
    {
        var store = new DbcFileStore();
        store.CreateFolder("can-a");
        store.AddFile("can-a", "a.dbc", ValidDbc);
        var model = new DbcEditorModel(store);
        var file = store.Find("can-a", "a.dbc")!;
        model.Open("can-a", file.Name, file.Content, file.Parsed);

        var cut = RenderComponent<DbcEditorPanel>(p => p.Add(x => x.Model, model));

        cut.FindAll("button")[0].Click(); // Text view
        var textarea = cut.Find("textarea");
        var edited =
            "VERSION \"\"\nBU_: XXX\n\nBO_ 100 EmptyMsg: 1 XXX\n\nBO_ 200 SecondMsg: 2 XXX\n";
        textarea.Change(edited);
        cut.FindAll("button")[2].Click(); // Save

        Assert.Equal(2, store.Find("can-a", "a.dbc")!.Parsed!.Messages.Count);
    }

    [Fact]
    public void PropertyPanel_shows_file_properties_and_apply_raises()
    {
        var store = new DbcFileStore();
        store.CreateFolder("can-a");
        store.AddFile("can-a", "a.dbc", ValidDbc);
        var model = new DbcPropertyModel();
        var file = store.Find("can-a", "a.dbc")!;
        model.ShowFile(file);
        var changed = 0;
        model.StateChanged += () => changed++;

        var cut = RenderComponent<DbcPropertyPanel>(p => p.Add(x => x.Model, model));

        Assert.Contains("File", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("a.dbc", cut.Markup, StringComparison.Ordinal);

        cut.Find("[data-testid='dbc-property-apply']").Click();

        Assert.Equal(1, changed);
    }
}
