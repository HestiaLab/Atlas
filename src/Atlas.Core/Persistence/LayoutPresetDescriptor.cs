namespace Atlas.Core.Persistence;

/// <summary>
///     命名工作区快照的持久化元数据。
/// </summary>
public sealed class LayoutPresetDescriptor
{
    /// <summary>
    ///     创建预设元数据；名称会作为存储键校验（拒绝 null、控制字符、路径
    ///     分隔符与保留前缀），修订号必须非负。
    /// </summary>
    public LayoutPresetDescriptor(
        string name,
        string schemaVersion,
        long savedRevision,
        DateTimeOffset? savedAt = null)
    {
        Name = RequireName(name);
        SchemaVersion = IdentifierGuard.RequireValue(schemaVersion, nameof(schemaVersion));
        if (savedRevision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(savedRevision),
                savedRevision,
                "A saved revision cannot be negative.");
        }

        SavedRevision = savedRevision;
        SavedAt = savedAt;
    }

    /// <summary>
    ///     获取序数预设名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     获取持久化的 schema 版本。
    /// </summary>
    public string SchemaVersion { get; }

    /// <summary>
    ///     获取捕获时的运行时修订号。
    /// </summary>
    public long SavedRevision { get; }

    /// <summary>
    ///     获取可选的捕获时间戳。
    /// </summary>
    public DateTimeOffset? SavedAt { get; }

    /// <summary>
    ///     校验预设名称并返回规范化后的存储键；
    ///     拒绝空值、控制字符、路径分隔符与内部保留前缀。
    /// </summary>
    /// <param name="name">待校验的预设名称。</param>
    /// <returns>
    ///     可作为存储键的规范化名称。
    /// </returns>
    internal static string RequireName(string? name)
    {
        var value = IdentifierGuard.RequireValue(name, nameof(name));
        if (value.Length > 128
            || value.Any(char.IsControl)
            || value.Contains('/')
            || value.Contains('\\')
            || value.StartsWith("__atlas_", StringComparison.Ordinal))
        {
            throw new ArgumentException("The preset name is not a valid storage key.", nameof(name));
        }

        return value;
    }
}
