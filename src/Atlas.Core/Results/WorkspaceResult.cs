namespace Atlas.Core.Results;

/// <summary>
/// Represents a typed operation outcome.
/// </summary>
public sealed class WorkspaceResult<T>
{
    private WorkspaceResult(WorkspaceResultStatus status, T? value, WorkspaceError? error)
    {
        Status = status;
        Value = value;
        Error = error;
    }

    /// <summary>Gets the outcome classification.</summary>
    public WorkspaceResultStatus Status { get; }

    /// <summary>Gets whether the operation completed without an expected failure.</summary>
    public bool Succeeded => Status is WorkspaceResultStatus.Success or WorkspaceResultStatus.NoChange;

    /// <summary>Gets whether the operation completed without committing a new revision.</summary>
    public bool IsNoChange => Status == WorkspaceResultStatus.NoChange;

    /// <summary>Gets the result value for successful and no-change outcomes.</summary>
    public T? Value { get; }

    /// <summary>Gets the expected failure when the operation did not succeed.</summary>
    public WorkspaceError? Error { get; }

    internal static WorkspaceResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new WorkspaceResult<T>(WorkspaceResultStatus.Success, value, error: null);
    }

    internal static WorkspaceResult<T> NoChange(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new WorkspaceResult<T>(WorkspaceResultStatus.NoChange, value, error: null);
    }

    internal static WorkspaceResult<T> Failure(WorkspaceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new WorkspaceResult<T>(WorkspaceResultStatus.Failure, value: default, error);
    }
}
