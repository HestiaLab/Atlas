namespace Atlas.Core.Persistence;

/// <summary>Contains one automatically persisted workspace document.</summary>
public sealed class WorkspaceLayoutDocument
{
    /// <summary>Initializes an automatic workspace document.</summary>
    public WorkspaceLayoutDocument(
        string schemaVersion,
        long savedRevision,
        string xml)
    {
        SchemaVersion = IdentifierGuard.RequireValue(schemaVersion, nameof(schemaVersion));
        if (savedRevision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(savedRevision),
                savedRevision,
                "A saved revision cannot be negative.");
        }

        SavedRevision = savedRevision;
        Xml = IdentifierGuard.RequireValue(xml, nameof(xml));
    }

    /// <summary>Gets the schema version.</summary>
    public string SchemaVersion { get; }

    /// <summary>Gets the captured runtime revision.</summary>
    public long SavedRevision { get; }

    /// <summary>Gets the persisted XML.</summary>
    public string Xml { get; }
}
