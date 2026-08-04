#nullable enable
using Atlas.Demo.Models.Dbc;
using Atlas.Demo.Services;
using Atlas.Demo.ViewModels;
using Xunit;

namespace Atlas.Demo.Tests;

public sealed class DbcTreeModelTests
{
    private const string ValidDbc =
        "VERSION \"\"\nBU_: XXX\n\nBO_ 100 EmptyMsg: 1 XXX\n";

    [Fact]
    public void NewFolder_and_Upload_raise_StateChanged()
    {
        var store = new DbcFileStore();
        var model = new DbcTreeModel(store);
        var changed = 0;
        model.StateChanged += () => changed++;

        model.NewFolder("can-a");
        model.Upload("can-a", "a.dbc", ValidDbc);

        Assert.Equal(2, changed);
        Assert.Single(model.Folders);
        Assert.Single(model.FilesFor("can-a"));
    }

    [Fact]
    public void Select_updates_selection_and_raises_StateChanged()
    {
        var store = new DbcFileStore();
        store.CreateFolder("can-a");
        store.AddFile("can-a", "a.dbc", ValidDbc);
        var model = new DbcTreeModel(store);
        var changed = 0;
        model.StateChanged += () => changed++;

        model.Select("can-a", "a.dbc");

        Assert.Equal("can-a", model.SelectedFolder);
        Assert.Equal("a.dbc", model.SelectedFile);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Store_changes_are_forwarded_to_StateChanged()
    {
        var store = new DbcFileStore();
        var model = new DbcTreeModel(store);
        var changed = 0;
        model.StateChanged += () => changed++;

        store.CreateFolder("can-a");

        Assert.Equal(1, changed);
    }
}

public sealed class DbcEditorModelTests
{
    private const string ValidDbc =
        "VERSION \"\"\nBU_: XXX\n\nBO_ 100 EmptyMsg: 1 XXX\n";

    [Fact]
    public void Open_sets_file_state_and_raises_StateChanged()
    {
        var model = new DbcEditorModel();
        var changed = 0;
        model.StateChanged += () => changed++;

        var parsed = DbcParser.Parse(ValidDbc);
        model.Open("can-a", "a.dbc", ValidDbc, parsed);

        Assert.Equal("can-a", model.Folder);
        Assert.Equal("a.dbc", model.FileName);
        Assert.Equal(ValidDbc, model.RawContent);
        Assert.NotNull(model.Document);
        Assert.Equal(ViewModels.DbcEditorModel.ViewMode.Table, model.Mode);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void SetMode_switches_and_notifies()
    {
        var model = new DbcEditorModel();
        model.Open("can-a", "a.dbc", ValidDbc, DbcParser.Parse(ValidDbc));
        var changed = 0;
        model.StateChanged += () => changed++;

        model.SetMode(ViewModels.DbcEditorModel.ViewMode.Text);

        Assert.Equal(ViewModels.DbcEditorModel.ViewMode.Text, model.Mode);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Save_reparses_content_and_writes_back_to_store()
    {
        var store = new DbcFileStore();
        store.CreateFolder("can-a");
        store.AddFile("can-a", "a.dbc", ValidDbc);
        var model = new DbcEditorModel(store);
        var file = store.Find("can-a", "a.dbc")!;
        model.Open("can-a", file.Name, file.Content, file.Parsed);

        var edited =
            "VERSION \"\"\nBU_: XXX\n\nBO_ 100 EmptyMsg: 1 XXX\n\nBO_ 200 SecondMsg: 2 XXX\n";
        model.Save(edited);

        var updated = store.Find("can-a", "a.dbc")!;
        Assert.Equal(2, updated.Parsed!.Messages.Count);
        Assert.Equal(edited, updated.Content);
    }
}

public sealed class DbcPropertyModelTests
{
    private const string ValidDbc =
        "VERSION \"\"\nBU_: XXX\n\nBO_ 100 EmptyMsg: 1 XXX\n";

    [Fact]
    public void ShowMessage_populates_property_state()
    {
        var message = new DbcMessage(
            100u,
            "EmptyMsg",
            1,
            "XXX",
            Array.Empty<DbcSignal>());
        var model = new DbcPropertyModel();

        model.ShowMessage(message);

        Assert.Equal("Message", model.TargetType);
        Assert.Equal("100", model.Id);
        Assert.Equal("EmptyMsg", model.Name);
        Assert.Equal("1", model.Length);
        Assert.Equal("XXX", model.Transmitter);
    }

    [Fact]
    public void ShowSignal_populates_property_state()
    {
        var signal = new DbcSignal(
            "Speed",
            32,
            16,
            isBigEndian: true,
            isSigned: false,
            1.0,
            0.0,
            0.0,
            65535.0,
            "kph",
            "XXX");
        var model = new DbcPropertyModel();

        model.ShowSignal(signal);

        Assert.Equal("Signal", model.TargetType);
        Assert.Equal("Speed", model.Name);
        Assert.Equal("32", model.StartBit);
        Assert.Equal("16", model.Length);
        Assert.Equal("1", model.Factor);
        Assert.Equal("0", model.Offset);
        Assert.Equal("0", model.Min);
        Assert.Equal("65535", model.Max);
    }

    [Fact]
    public void Apply_raises_StateChanged()
    {
        var model = new DbcPropertyModel();
        var changed = 0;
        model.StateChanged += () => changed++;

        model.Apply();

        Assert.Equal(1, changed);
    }
}
