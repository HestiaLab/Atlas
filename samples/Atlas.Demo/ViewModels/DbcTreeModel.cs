using Atlas.Demo.Services;

namespace Atlas.Demo.ViewModels;

/// <summary>
/// PanelModel for the DBC Explorer tree. Owns folder/file selection and
/// exposes upload/create operations backed by the shared
/// <see cref="DbcFileStore"/>. Raises <see cref="StateChanged"/> when the
/// selection or the underlying store changes.
/// </summary>
public sealed class DbcTreeModel
{
    private readonly DbcFileStore _store;
    private string? _selectedFolder;
    private string? _selectedFile;

    public DbcTreeModel(DbcFileStore store)
    {
        _store = store;
        _store.Changed += OnStoreChanged;
    }

    public IReadOnlyList<DbcFileStore.DbcFolder> Folders => _store.Folders;

    public string? SelectedFolder => _selectedFolder;

    public string? SelectedFile => _selectedFile;

    public event Action? StateChanged;

    public void Select(string? folder, string? file)
    {
        _selectedFolder = folder;
        _selectedFile = file;
        StateChanged?.Invoke();
    }

    public void NewFolder(string name)
    {
        _store.CreateFolder(name);
    }

    public void Upload(string folder, string fileName, string content)
    {
        _store.AddFile(folder, fileName, content);
    }

    public IReadOnlyList<DbcFileStore.DbcFile> FilesFor(string folder)
    {
        return _store.FilesIn(folder);
    }

    private void OnStoreChanged()
    {
        StateChanged?.Invoke();
    }
}
