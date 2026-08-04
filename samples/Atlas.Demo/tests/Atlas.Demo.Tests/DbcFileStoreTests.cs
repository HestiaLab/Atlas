#nullable enable
using Atlas.Demo.Services;
using Xunit;

namespace Atlas.Demo.Tests;

public sealed class DbcFileStoreTests
{
    private const string ValidDbc =
        "VERSION \"\"\nBU_: XXX\n\nBO_ 100 EmptyMsg: 1 XXX\n";

    [Fact]
    public void CreateFolder_adds_folder_and_raises_changed()
    {
        var store = new DbcFileStore();
        var changed = 0;
        store.Changed += () => changed++;

        store.CreateFolder("can-a");

        Assert.Single(store.Folders);
        Assert.Equal("can-a", store.Folders[0].Name);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void CreateFolder_rejects_duplicate_name()
    {
        var store = new DbcFileStore();
        store.CreateFolder("can-a");

        Assert.Throws<InvalidOperationException>(
            () => store.CreateFolder("can-a"));
        Assert.Single(store.Folders);
    }

    [Fact]
    public void AddFile_parses_content_and_lists_files_sorted()
    {
        var store = new DbcFileStore();
        store.CreateFolder("can-a");

        store.AddFile("can-a", "z.dbc", ValidDbc);
        store.AddFile("can-a", "a.dbc", ValidDbc);

        var files = store.FilesIn("can-a");
        Assert.Equal(new[] { "a.dbc", "z.dbc" }, files.Select(f => f.Name));
        Assert.NotNull(files[0].Parsed);
        Assert.Single(files[0].Parsed!.Messages);
    }

    [Fact]
    public void UpdateFileContent_reparses_and_renames_survive()
    {
        var store = new DbcFileStore();
        store.CreateFolder("can-a");
        store.AddFile("can-a", "a.dbc", ValidDbc);

        var longer =
            "VERSION \"\"\nBU_: XXX\n\nBO_ 100 EmptyMsg: 1 XXX\n\nBO_ 200 SecondMsg: 2 XXX\n";
        store.UpdateFileContent("can-a", "a.dbc", longer);

        var file = store.Find("can-a", "a.dbc");
        Assert.NotNull(file);
        Assert.Equal(2, file!.Parsed!.Messages.Count);

        store.RenameFile("can-a", "a.dbc", "renamed.dbc");
        Assert.Null(store.Find("can-a", "a.dbc"));
        Assert.NotNull(store.Find("can-a", "renamed.dbc"));
    }

    [Fact]
    public void Find_returns_null_for_missing_folder_or_file()
    {
        var store = new DbcFileStore();
        store.CreateFolder("can-a");

        Assert.Null(store.Find("can-a", "missing.dbc"));
        Assert.Null(store.Find("missing-folder", "a.dbc"));
        Assert.Empty(store.FilesIn("missing-folder"));
    }
}
