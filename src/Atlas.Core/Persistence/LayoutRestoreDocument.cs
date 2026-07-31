namespace Atlas.Core.Persistence;

/// <summary>Contains a validated immutable candidate produced by a layout serializer.</summary>
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

    /// <summary>Gets the validated candidate snapshot.</summary>
    public WorkspaceSnapshot Snapshot { get; }

    /// <summary>Gets the source schema version.</summary>
    public string SourceSchemaVersion { get; }

    /// <summary>Gets deterministic non-fatal migration or repair warnings.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Gets deterministic target identity remaps.</summary>
    public IReadOnlyList<IdentityRemap> IdentityRemaps { get; }

    /// <summary>Gets items preserved as safe missing-content placeholders.</summary>
    public IReadOnlyList<DockItemId> PlaceholderItems { get; }

    /// <summary>Gets unregistered items removed by policy.</summary>
    public IReadOnlyList<DockItemId> DroppedItems { get; }
}
