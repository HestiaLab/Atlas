using Atlas.Core.Results;

namespace Atlas.Core.Persistence;

/// <summary>
///     Serializes and validates persisted Atlas workspace documents.
/// </summary>
public interface IAtlasLayoutSerializer
{
    /// <summary>
    ///     Serializes an immutable snapshot as canonical V2 XML.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     The snapshot violates committed-state invariants or exceeds the default
    ///     persistence size limits.
    /// </exception>
    string Serialize(WorkspaceSnapshot snapshot);

    /// <summary>
    ///     Parses an untrusted V1 or V2 document into a validated restore candidate.
    /// </summary>
    WorkspaceResult<LayoutRestoreDocument> Deserialize(
        string xml,
        WorkspaceId targetWorkspaceId,
        LayoutPersistenceLimits? limits = null);

    /// <summary>
    ///     Parses with explicit restore options: resource limits, content registration
    ///     predicate, missing-content policy, and reserved identity remapping, and
    ///     returns the processed restore candidate.
    /// </summary>
    WorkspaceResult<LayoutRestoreDocument> DeserializeWithOptions(
        string xml,
        WorkspaceId targetWorkspaceId,
        LayoutRestoreOptions options);
}
