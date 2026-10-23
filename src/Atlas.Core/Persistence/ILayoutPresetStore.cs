namespace Atlas.Core.Persistence;

/// <summary>
///     Stores named layout documents without interpreting their content.
/// </summary>
public interface ILayoutPresetStore
{
    /// <summary>
    ///     Saves or overwrites the preset for an ordinal name.
    /// </summary>
    ValueTask SaveAsync(
        string name,
        LayoutPresetDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads the preset for an ordinal name, returning null when it does not exist.
    /// </summary>
    ValueTask<LayoutPresetDocument?> TryLoadAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes the preset for an ordinal name and reports whether it existed.
    /// </summary>
    ValueTask<bool> DeleteAsync(
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the stored user preset descriptors.
    /// </summary>
    ValueTask<IReadOnlyList<LayoutPresetDescriptor>> ListAsync(
        CancellationToken cancellationToken = default);
}
