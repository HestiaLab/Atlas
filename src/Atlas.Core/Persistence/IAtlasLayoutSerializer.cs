using Atlas.Core.Results;

namespace Atlas.Core.Persistence;

/// <summary>
///     序列化并验证持久化的 Atlas 工作区文档。
/// </summary>
public interface IAtlasLayoutSerializer
{
    /// <summary>
    ///     将一个不可变快照序列化为规范的 V2 XML。
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     快照违反已提交状态不变量，或超过默认持久化大小限制。
    /// </exception>
    string Serialize(WorkspaceSnapshot snapshot);

    /// <summary>
    ///     将不受信任的 V1 或 V2 文档解析为经过验证的恢复候选项。
    /// </summary>
    WorkspaceResult<LayoutRestoreDocument> Deserialize(
        string xml,
        WorkspaceId targetWorkspaceId,
        LayoutPersistenceLimits? limits = null);

    /// <summary>
    ///     按显式恢复选项解析：以选项中的资源限制解析文档，再应用内容注册判定、
    ///     缺失内容策略与保留标识重映射，输出处理后的恢复候选项。
    /// </summary>
    WorkspaceResult<LayoutRestoreDocument> DeserializeWithOptions(
        string xml,
        WorkspaceId targetWorkspaceId,
        LayoutRestoreOptions options);
}
