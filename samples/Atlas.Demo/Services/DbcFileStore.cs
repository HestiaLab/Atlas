using Atlas.Demo.Models.Dbc;

namespace Atlas.Demo.Services;

/// <summary>
/// In-memory DBC file tree used as the single source of truth for the demo.
/// Files are stored by folder/name; parsing happens on write so panels always
/// read a consistent <see cref="DbcFile.Parsed"/> document. All mutations raise
/// <see cref="Changed"/> so view models can propagate refreshes.
/// </summary>
public sealed class DbcFileStore
{
    public sealed record DbcFolder(string Name);

    public sealed record DbcFile(string Name, string Content, DbcDocument? Parsed);

    private readonly Dictionary<string, List<DbcFile>> _filesByFolder = new(StringComparer.Ordinal);

    public event Action? Changed;

    public IReadOnlyList<DbcFolder> Folders
    {
        get
        {
            return _filesByFolder.Keys
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => new DbcFolder(name))
                .ToArray();
        }
    }

    public void CreateFolder(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A folder name is required.", nameof(name));
        }

        if (_filesByFolder.ContainsKey(name))
        {
            throw new InvalidOperationException($"Folder '{name}' already exists.");
        }

        _filesByFolder.Add(name, new List<DbcFile>());
        Changed?.Invoke();
    }

    public void AddFile(string folder, string name, string content)
    {
        var files = RequireFolder(folder);
        if (files.Any(f => f.Name == name))
        {
            throw new InvalidOperationException($"File '{name}' already exists in '{folder}'.");
        }

        files.Add(CreateFile(name, content));
        Changed?.Invoke();
    }

    public void RenameFile(string folder, string oldName, string newName)
    {
        var files = RequireFolder(folder);
        var index = files.FindIndex(f => f.Name == oldName);
        if (index < 0)
        {
            throw new InvalidOperationException($"File '{oldName}' does not exist in '{folder}'.");
        }

        if (files.Any(f => f.Name == newName))
        {
            throw new InvalidOperationException($"File '{newName}' already exists in '{folder}'.");
        }

        var existing = files[index];
        files[index] = existing with { Name = newName };
        Changed?.Invoke();
    }

    public void UpdateFileContent(string folder, string name, string content)
    {
        var files = RequireFolder(folder);
        var index = files.FindIndex(f => f.Name == name);
        if (index < 0)
        {
            throw new InvalidOperationException($"File '{name}' does not exist in '{folder}'.");
        }

        files[index] = CreateFile(name, content);
        Changed?.Invoke();
    }

    public IReadOnlyList<DbcFile> FilesIn(string folder)
    {
        return _filesByFolder.TryGetValue(folder, out var files)
            ? files.OrderBy(f => f.Name, StringComparer.Ordinal).ToArray()
            : Array.Empty<DbcFile>();
    }

    public DbcFile? Find(string folder, string name)
    {
        return _filesByFolder.TryGetValue(folder, out var files)
            ? files.FirstOrDefault(f => f.Name == name)
            : null;
    }

    private List<DbcFile> RequireFolder(string folder)
    {
        if (!_filesByFolder.TryGetValue(folder, out var files))
        {
            throw new InvalidOperationException($"Folder '{folder}' does not exist.");
        }

        return files;
    }

    private static DbcFile CreateFile(string name, string content)
    {
        DbcDocument? parsed = null;
        try
        {
            parsed = DbcParser.Parse(content);
        }
        catch
        {
            // Keep the file addressable in the tree even when its content is
            // not yet valid DBC; panels can still show the raw text.
        }

        return new DbcFile(name, content, parsed);
    }
}
