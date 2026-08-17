namespace Atlas.Core.Persistence;

/// <summary>
///     说明持久化项标识被替换的原因。
/// </summary>
public enum IdentityRemapReason
{
    /// <summary>
    ///     目标作用域已占用该标识，恢复时改名以规避冲突。
    /// </summary>
    Conflict = 0,

    /// <summary>
    ///     文档被显式克隆到另一个作用域。
    /// </summary>
    CloneImport = 1,

    /// <summary>
    ///     遗留标识符需要规范化。
    /// </summary>
    LegacyMigration = 2,
}
