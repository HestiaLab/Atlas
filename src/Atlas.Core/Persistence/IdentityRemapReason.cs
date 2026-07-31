namespace Atlas.Core.Persistence;

/// <summary>Identifies why a persisted item identity was replaced.</summary>
public enum IdentityRemapReason
{
    /// <summary>The target runtime reserves the persisted identity.</summary>
    Conflict = 0,

    /// <summary>The document was explicitly cloned to another scope.</summary>
    CloneImport = 1,

    /// <summary>A legacy identifier required normalization.</summary>
    LegacyMigration = 2,
}
