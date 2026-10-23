namespace Atlas.Core.Persistence;

/// <summary>
///     Immutable document written to storage by automatic persistence: schema
///     version, revision captured at the time, and serialized XML.
/// </summary>
public sealed class WorkspaceLayoutDocument
{
    /// <summary>
    ///     Creates the document; the revision must be non-negative, and the schema
    ///     version and XML must be non-empty.
    /// </summary>
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

    /// <summary>
    ///     Gets the schema version.
    /// </summary>
    public string SchemaVersion { get; }

    /// <summary>
    ///     Gets the captured runtime revision.
    /// </summary>
    public long SavedRevision { get; }

    /// <summary>
    ///     Gets the persisted XML.
    /// </summary>
    public string Xml { get; }
}
