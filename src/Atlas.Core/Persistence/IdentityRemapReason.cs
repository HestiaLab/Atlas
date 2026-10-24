namespace Atlas.Core.Persistence;

/// <summary>
///     说明持久化条目标识符被替换的原因。
/// </summary>
public enum IdentityRemapReason
{
    /// <summary>
    ///     目标作用域已使用该标识符，因此恢复时重命名以避免冲突。
    /// </summary>
    Conflict = 0,

    /// <summary>
    ///     文档被显式克隆到另一个作用域。
    /// </summary>
    CloneImport = 1,

    /// <summary>
    ///     旧版标识符需要规范化。
    /// </summary>
    LegacyMigration = 2,
}
