namespace Atlas.Core.Persistence;

/// <summary>
///     Categorizes persistence degradations without exposing document content.
/// </summary>
public enum WorkspacePersistenceDiagnosticKind
{
    /// <summary>
    ///     The configured storage adapter is unavailable.
    /// </summary>
    StorageUnavailable = 0,

    /// <summary>
    ///     The stored document fails validation.
    /// </summary>
    CorruptDocument = 1,

    /// <summary>
    ///     The current snapshot cannot be serialized.
    /// </summary>
    SerializationFailure = 2,
}
