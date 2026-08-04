using Atlas.Demo.Models.Dbc;
using Atlas.Demo.Services;

namespace Atlas.Demo.ViewModels;

/// <summary>
/// PanelModel for the DBC editor document. Tracks the open file's raw text
/// and parsed document and supports text/table view switching. Saving
/// re-parses the edited content and writes it back through the shared
/// <see cref="DbcFileStore"/>.
/// </summary>
public sealed class DbcEditorModel
{
    public enum ViewMode
    {
        Text,
        Table,
    }

    private readonly DbcFileStore? _store;

    public DbcEditorModel(DbcFileStore? store = null)
    {
        _store = store;
    }

    public string? Folder { get; private set; }

    public string? FileName { get; private set; }

    public string? RawContent { get; private set; }

    public DbcDocument? Document { get; private set; }

    public ViewMode Mode { get; private set; } = ViewMode.Table;

    public event Action? StateChanged;

    public void Open(string folder, string file, string content, DbcDocument? parsed)
    {
        Folder = folder;
        FileName = file;
        RawContent = content;
        Document = parsed;
        Notify();
    }

    public void SetMode(ViewMode mode)
    {
        if (Mode == mode)
        {
            return;
        }

        Mode = mode;
        Notify();
    }

    public void Save(string newContent)
    {
        if (_store is null || Folder is null || FileName is null)
        {
            throw new InvalidOperationException(
                "Cannot save without a store and an open file.");
        }

        _store.UpdateFileContent(Folder, FileName, newContent);

        var updated = _store.Find(Folder, FileName);
        if (updated is not null)
        {
            RawContent = updated.Content;
            Document = updated.Parsed;
        }

        Notify();
    }

    private void Notify()
    {
        StateChanged?.Invoke();
    }
}
