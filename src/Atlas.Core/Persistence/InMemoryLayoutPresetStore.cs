namespace Atlas.Core.Persistence;

/// <summary>
///     提供隔离的内存序号预设存储。
/// </summary>
public sealed class InMemoryLayoutPresetStore : ILayoutPresetStore
{
    // 文档字典的互斥锁。
    private readonly object gate = new();

    // 预设名（序数比较）→ 预设文档的内存字典。
    private readonly Dictionary<string, LayoutPresetDocument> documents =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask SaveAsync(
        string name,
        LayoutPresetDocument document,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validated = LayoutPresetDescriptor.RequireName(name);
        ArgumentNullException.ThrowIfNull(document);
        if (!StringComparer.Ordinal.Equals(validated, document.Descriptor.Name))
        {
            throw new ArgumentException(
                "The storage key must match the document descriptor.",
                nameof(name));
        }

        lock (gate)
        {
            documents[validated] = document;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<LayoutPresetDocument?> TryLoadAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validated = LayoutPresetDescriptor.RequireName(name);
        lock (gate)
        {
            documents.TryGetValue(validated, out var document);
            return ValueTask.FromResult(document);
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var validated = LayoutPresetDescriptor.RequireName(name);
        lock (gate)
        {
            return ValueTask.FromResult(documents.Remove(validated));
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<LayoutPresetDescriptor>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            IReadOnlyList<LayoutPresetDescriptor> result = Array.AsReadOnly(
                documents.Values
                    .Select(document => document.Descriptor)
                    .OrderBy(descriptor => descriptor.Name, StringComparer.Ordinal)
                    .ToArray());
            return ValueTask.FromResult(result);
        }
    }
}
