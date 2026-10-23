namespace Atlas.Core.Persistence;

/// <summary>
///     Persistence metadata for a named workspace snapshot.
/// </summary>
public sealed class LayoutPresetDescriptor
{
    /// <summary>
    ///     Creates the preset metadata; the name is validated as a storage key
    ///     (rejecting null, control characters, path separators and reserved
    ///     prefixes), and the revision must be non-negative.
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
    ///     Gets the ordinal preset name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the persisted schema version.
    /// </summary>
    public string SchemaVersion { get; }

    /// <summary>
    ///     Gets the captured runtime revision.
    /// </summary>
    public long SavedRevision { get; }

    /// <summary>
    ///     Gets the optional capture timestamp.
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
