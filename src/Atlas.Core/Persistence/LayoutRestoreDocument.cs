namespace Atlas.Core.Persistence;

/// <summary>
///     由布局序列化器生成并验证的不可变恢复候选：候选快照、源版本、非致命
///     警告以及策略结果（重映射/占位/剔除）。
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
    ///     获取已验证的候选快照。
    /// </summary>
    public WorkspaceSnapshot Snapshot { get; }

    /// <summary>
    ///     获取源 schema 版本。
    /// </summary>
    public string SourceSchemaVersion { get; }

    /// <summary>
    ///     获取解析产生的非致命迁移警告；当前只有 V1 迁移会产生警告。
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    ///     获取应用到快照的标识重映射（避免目标工作区中的保留标识冲突）。
    /// </summary>
    public IReadOnlyList<IdentityRemap> IdentityRemaps { get; }

    /// <summary>
    ///     获取因内容未注册而保留为占位符的条目。
    /// </summary>
    public IReadOnlyList<DockItemId> PlaceholderItems { get; }

    /// <summary>
    ///     获取按策略移除的未注册条目。
    /// </summary>
    public IReadOnlyList<DockItemId> DroppedItems { get; }
}
