namespace Atlas.Core.Persistence;

/// <summary>
///     Immutable restore candidate produced and validated by the layout serializer:
///     candidate snapshot, source version, non-fatal warnings and policy outcomes
///     (remap/placeholder/drop).
/// </summary>
public sealed class LayoutRestoreDocument
{
    internal LayoutRestoreDocument(
        WorkspaceSnapshot snapshot,
        string sourceSchemaVersion,
        IEnumerable<string>? warnings = null,
        IEnumerable<IdentityRemap>? identityRemaps = null,
        IEnumerable<DockItemId>? placeholderItems = null,
        IEnumerable<DockItemId>? droppedItems = null)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        SourceSchemaVersion = IdentifierGuard.RequireValue(
            sourceSchemaVersion,
            nameof(sourceSchemaVersion));
        Warnings = Array.AsReadOnly((warnings ?? Array.Empty<string>()).ToArray());
        IdentityRemaps = Array.AsReadOnly(
            (identityRemaps ?? Array.Empty<IdentityRemap>()).ToArray());
        PlaceholderItems = Array.AsReadOnly(
            (placeholderItems ?? Array.Empty<DockItemId>()).ToArray());
        DroppedItems = Array.AsReadOnly(
            (droppedItems ?? Array.Empty<DockItemId>()).ToArray());
    }

    /// <summary>
    ///     Gets the validated candidate snapshot.
    /// </summary>
    public WorkspaceSnapshot Snapshot { get; }

    /// <summary>
    ///     Gets the source schema version.
    /// </summary>
    public string SourceSchemaVersion { get; }

    /// <summary>
    ///     Gets the non-fatal migration warnings produced by parsing; currently only
    ///     the V1 migration produces warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    ///     Gets the identity remaps applied to the snapshot (avoiding reserved
    ///     identity conflicts in the target workspace).
    /// </summary>
    public IReadOnlyList<IdentityRemap> IdentityRemaps { get; }

    /// <summary>
    ///     Gets the items retained as placeholders because their content is not
    ///     registered.
    /// </summary>
    public IReadOnlyList<DockItemId> PlaceholderItems { get; }

    /// <summary>
    ///     Gets the unregistered items removed by policy.
    /// </summary>
    public IReadOnlyList<DockItemId> DroppedItems { get; }
}
