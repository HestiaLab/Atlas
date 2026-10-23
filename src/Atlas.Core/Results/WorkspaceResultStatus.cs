namespace Atlas.Core.Results;

/// <summary>
///     Identifies whether an operation committed, made no change, or failed as expected.
/// </summary>
public enum WorkspaceResultStatus
{
    /// <summary>
    ///     The operation committed a new revision.
    /// </summary>
    Success = 0,

    /// <summary>
    ///     The operation completed without changing the snapshot.
    /// </summary>
    NoChange = 1,

    /// <summary>
    ///     The operation failed with an expected workspace error.
    /// </summary>
    Failure = 2,
}
