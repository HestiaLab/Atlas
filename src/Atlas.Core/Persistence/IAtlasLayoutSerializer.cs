using Atlas.Core.Results;

namespace Atlas.Core.Persistence;

/// <summary>Serializes and validates persisted Atlas workspace documents.</summary>
public interface IAtlasLayoutSerializer
{
    /// <summary>Serializes one immutable snapshot as canonical V2 XML.</summary>
    string Serialize(WorkspaceSnapshot snapshot);

    /// <summary>Parses an untrusted V1 or V2 document into a validated restore candidate.</summary>
    WorkspaceResult<LayoutRestoreDocument> Deserialize(
        string xml,
        WorkspaceId targetWorkspaceId,
        LayoutPersistenceLimits? limits = null);

    /// <summary>Parses with explicit content and identity restore policies.</summary>
    WorkspaceResult<LayoutRestoreDocument> DeserializeWithOptions(
        string xml,
        WorkspaceId targetWorkspaceId,
        LayoutRestoreOptions options);
}
