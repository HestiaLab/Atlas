namespace Atlas.Core.Persistence;

/// <summary>
///     自动持久化写入存储的不可变文档：架构版本、捕获时的修订号与序列化 XML。
/// </summary>
public sealed class WorkspaceLayoutDocument
{
    /// <summary>
    ///     构造文档；修订号必须非负，架构版本与 XML 必须非空。
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
    ///     获取架构版本。
    /// </summary>
    public string SchemaVersion { get; }

    /// <summary>
    ///     获取捕获的运行时修订号。
    /// </summary>
    public long SavedRevision { get; }

    /// <summary>
    ///     获取持久化的 XML。
    /// </summary>
    public string Xml { get; }
}
