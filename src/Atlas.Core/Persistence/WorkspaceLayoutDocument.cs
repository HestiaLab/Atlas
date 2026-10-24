namespace Atlas.Core.Persistence;

/// <summary>
///     自动持久化写入存储的不可变文档：schema 版本、捕获时的修订号与序列化 XML。
/// </summary>
public sealed class WorkspaceLayoutDocument
{
    /// <summary>
    ///     创建文档；修订号必须非负，schema 版本与 XML 不得为空。
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
    ///     获取 schema 版本。
    /// </summary>
    public string SchemaVersion { get; }

    /// <summary>
    ///     获取捕获时的运行时修订号。
    /// </summary>
    public long SavedRevision { get; }

    /// <summary>
    ///     获取持久化的 XML。
    /// </summary>
    public string Xml { get; }
}
