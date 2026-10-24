using Atlas.Core.Changes;

namespace Atlas.Core.Persistence;

/// <summary>
///     报告一次成功提交的工作区恢复。
/// </summary>
public sealed class LayoutRestoreResult
{
    internal LayoutRestoreResult(
        LayoutChangeSet changeSet,
        LayoutRestoreDocument document)
    {
        ChangeSet = changeSet ?? throw new ArgumentNullException(nameof(changeSet));
        ArgumentNullException.ThrowIfNull(document);
        SourceSchemaVersion = document.SourceSchemaVersion;
        Warnings = document.Warnings;
        IdentityRemaps = document.IdentityRemaps;
        PlaceholderItems = document.PlaceholderItems;
        DroppedItems = document.DroppedItems;
    }

    /// <summary>
    ///     获取单次原子恢复变更集。
    /// </summary>
    public LayoutChangeSet ChangeSet { get; }

    /// <summary>
    ///     获取已提交的目标修订号。
    /// </summary>
    public long TargetRevision => ChangeSet.AfterRevision;

    /// <summary>
    ///     获取源 schema 版本。
    /// </summary>
    public string SourceSchemaVersion { get; }

    /// <summary>
    ///     获取非致命迁移警告。
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    ///     获取提交前应用的标识重映射。
    /// </summary>
    public IReadOnlyList<IdentityRemap> IdentityRemaps { get; }

    /// <summary>
    ///     获取恢复后保留为占位符的条目。
    /// </summary>
    public IReadOnlyList<DockItemId> PlaceholderItems { get; }

    /// <summary>
    ///     获取被缺失内容策略移除的条目。
    /// </summary>
    public IReadOnlyList<DockItemId> DroppedItems { get; }
}
