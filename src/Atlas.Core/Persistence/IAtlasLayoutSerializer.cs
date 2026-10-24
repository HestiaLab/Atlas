using Atlas.Core.Results;

namespace Atlas.Core.Persistence;

/// <summary>
///     序列化并验证持久化的 Atlas 工作区文档。
/// </summary>
public interface IAtlasLayoutSerializer
{
    /// <summary>
    ///     把不可变快照序列化为规范 V2 XML。
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     快照违反已提交状态不变量，或超出默认持久化大小限制。
    /// </exception>
    string Serialize(WorkspaceSnapshot snapshot);

    /// <summary>
    ///     把不可信的 V1 或 V2 文档解析为经验证的恢复候选。
    /// </summary>
    WorkspaceResult<LayoutRestoreDocument> Deserialize(
        string xml,
        WorkspaceId targetWorkspaceId,
        LayoutPersistenceLimits? limits = null);

    /// <summary>
    ///     使用显式恢复选项解析：资源限制、内容注册谓词、缺失内容策略与保留
    ///     标识重映射，并返回处理后的恢复候选。
    /// </summary>
    WorkspaceResult<LayoutRestoreDocument> DeserializeWithOptions(
        string xml,
        WorkspaceId targetWorkspaceId,
        LayoutRestoreOptions options);
}
