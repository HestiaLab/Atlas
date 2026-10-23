namespace Atlas.Core.Results;

/// <summary>
///     Identifies a stable expected workspace failure.
/// </summary>
public enum WorkspaceErrorCode
{
    /// <summary>
    ///     The stored layout uses an unsupported schema version.
    /// </summary>
    LayoutVersionIncompatible = 1,

    /// <summary>
    ///     The supplied layout data is invalid.
    /// </summary>
    InvalidLayoutData = 3,

    /// <summary>
    ///     The supplied state contains duplicate tab identifiers.
    /// </summary>
    DuplicateTabId = 4,

    /// <summary>
    ///     The requested dock item does not exist.
    /// </summary>
    ItemNotFound = 5,

    /// <summary>
    ///     The operation targets a stale workspace revision.
    /// </summary>
    StaleOperation = 6,

    /// <summary>
    ///     The requested content identifier is not registered.
    /// </summary>
    ContentNotRegistered = 7,

    /// <summary>
    ///     The target is rejected by the placement policy.
    /// </summary>
    TargetRejectedByPolicy = 8,

    /// <summary>
    ///     The item capability set forbids the operation.
    /// </summary>
    CapabilityRejected = 9,

    /// <summary>
    ///     The asynchronous close guard cancelled the operation.
    /// </summary>
    CloseCancelledByGuard = 10,

    /// <summary>
    ///     The configured persistence provider is unavailable.
    /// </summary>
    PersistenceUnavailable = 11,

    /// <summary>
    ///     The workspace is faulted and cannot continue.
    /// </summary>
    WorkspaceFaulted = 12,

    /// <summary>
    ///     The requested layout target does not exist.
    /// </summary>
    TargetNotFound = 15,

    /// <summary>
    ///     The requested content identity duplicates an existing workspace item.
    /// </summary>
    DuplicateContentIdentity = 16,
}
