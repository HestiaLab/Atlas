namespace Atlas.Core.Persistence;

/// <summary>Stores named layout documents without interpreting their contents.</summary>
public interface ILayoutPresetStore
{
    /// <summary>Saves or overwrites one ordinal name.</summary>
    ValueTask SaveAsync(
        string name,
        LayoutPresetDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>Loads one ordinal name, or null when it does not exist.</summary>
    ValueTask<LayoutPresetDocument?> TryLoadAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes one ordinal name and reports whether it existed.</summary>
    ValueTask<bool> DeleteAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>Lists stored user preset descriptors.</summary>
    ValueTask<IReadOnlyList<LayoutPresetDescriptor>> ListAsync(
        CancellationToken cancellationToken = default);
}
