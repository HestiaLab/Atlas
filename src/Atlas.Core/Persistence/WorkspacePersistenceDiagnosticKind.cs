namespace Atlas.Core.Persistence;

/// <summary>
///     对持久化降级分类，且不暴露文档内容。
/// </summary>
public enum WorkspacePersistenceDiagnosticKind
{
    /// <summary>
    ///     配置的存储适配器不可用。
    /// </summary>
    StorageUnavailable = 0,

    /// <summary>
    ///     存储的文档未通过验证。
    /// </summary>
    CorruptDocument = 1,

    /// <summary>
    ///     当前快照无法序列化。
    /// </summary>
    SerializationFailure = 2,
}
