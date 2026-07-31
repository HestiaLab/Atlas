namespace Atlas.Core.Persistence;

/// <summary>Classifies persistence degradation without exposing document contents.</summary>
public enum WorkspacePersistenceDiagnosticKind
{
    /// <summary>The configured storage adapter was unavailable.</summary>
    StorageUnavailable = 0,

    /// <summary>A stored document could not be validated.</summary>
    CorruptDocument = 1,

    /// <summary>The current snapshot could not be serialized.</summary>
    SerializationFailure = 2,
}
