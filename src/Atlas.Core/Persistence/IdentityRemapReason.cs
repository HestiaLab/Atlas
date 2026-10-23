namespace Atlas.Core.Persistence;

/// <summary>
///     Explains why a persisted item identifier was replaced.
/// </summary>
public enum IdentityRemapReason
{
    /// <summary>
    ///     The target scope already uses the identifier, so it is renamed during
    ///     restore to avoid the conflict.
    /// </summary>
    Conflict = 0,

    /// <summary>
    ///     The document was explicitly cloned into another scope.
    /// </summary>
    CloneImport = 1,

    /// <summary>
    ///     A legacy identifier requires normalization.
    /// </summary>
    LegacyMigration = 2,
}
