namespace Atlas.Core.Results;

/// <summary>
/// Identifies a stable expected workspace failure.
/// </summary>
public enum WorkspaceErrorCode
{
    /// <summary>The stored layout uses an unsupported schema version.</summary>
    LayoutVersionIncompatible = 1,

    /// <summary>The requested content identity is already registered.</summary>
    ContentAlreadyRegistered = 2,

    /// <summary>The supplied layout data is structurally invalid.</summary>
    InvalidLayoutData = 3,

    /// <summary>The supplied state contains a duplicate tab identity.</summary>
    DuplicateTabId = 4,

    /// <summary>The requested dock item does not exist.</summary>
    ItemNotFound = 5,

    /// <summary>The operation targets an obsolete workspace revision.</summary>
    StaleOperation = 6,

    /// <summary>The requested content identity is not registered.</summary>
    ContentNotRegistered = 7,

    /// <summary>The target is disallowed by placement policy.</summary>
    TargetRejectedByPolicy = 8,

    /// <summary>The item capability set disallows the operation.</summary>
    CapabilityRejected = 9,

    /// <summary>An asynchronous close guard cancelled the operation.</summary>
    CloseCancelledByGuard = 10,

    /// <summary>The configured persistence provider is unavailable.</summary>
    PersistenceUnavailable = 11,

    /// <summary>The workspace cannot continue because it is faulted.</summary>
    WorkspaceFaulted = 12,

    /// <summary>The interaction request violates the frozen protocol.</summary>
    InteractionProtocolInvalid = 13,

    /// <summary>The referenced drag session is no longer active.</summary>
    DragSessionExpired = 14,
}
