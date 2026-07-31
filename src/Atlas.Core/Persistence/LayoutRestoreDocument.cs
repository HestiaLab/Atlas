namespace Atlas.Core.Persistence;

/// <summary>Contains a validated immutable candidate produced by a layout serializer.</summary>
public sealed class LayoutRestoreDocument
{
    internal LayoutRestoreDocument(
        WorkspaceSnapshot snapshot,
        string sourceSchemaVersion,
        IEnumerable<string>? warnings = null)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        SourceSchemaVersion = IdentifierGuard.RequireValue(
            sourceSchemaVersion,
            nameof(sourceSchemaVersion));
        Warnings = Array.AsReadOnly((warnings ?? Array.Empty<string>()).ToArray());
    }

    /// <summary>Gets the validated candidate snapshot.</summary>
    public WorkspaceSnapshot Snapshot { get; }

    /// <summary>Gets the source schema version.</summary>
    public string SourceSchemaVersion { get; }

    /// <summary>Gets deterministic non-fatal migration or repair warnings.</summary>
    public IReadOnlyList<string> Warnings { get; }
}
