namespace Atlas.Core.Results;

/// <summary>
///     Typed operation result: success or no-change carries a value; failure carries
///     an expected error.
/// </summary>
public sealed class WorkspaceResult<T>
{
    private WorkspaceResult(WorkspaceResultStatus status, T? value, WorkspaceError? error)
    {
        Status = status;
        Value = value;
        Error = error;
    }

    /// <summary>
    ///     Gets the result category.
    /// </summary>
    public WorkspaceResultStatus Status { get; }

    /// <summary>
    ///     Gets a value indicating whether the operation completed without an
    ///     expected failure.
    /// </summary>
    public bool Succeeded => Status is WorkspaceResultStatus.Success or WorkspaceResultStatus.NoChange;

    /// <summary>
    ///     Gets a value indicating whether the operation completed without committing
    ///     a new revision.
    /// </summary>
    public bool IsNoChange => Status == WorkspaceResultStatus.NoChange;

    /// <summary>
    ///     Gets the result value for success and no-change results.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    ///     Gets the expected error when the operation did not succeed.
    /// </summary>
    public WorkspaceError? Error { get; }

    /// <summary>
    ///     Creates a successfully completed operation result.
    /// </summary>
    /// <param name="value">Value produced by the operation.</param>
    /// <returns>
    ///     A success result carrying the supplied value.
    /// </returns>
    internal static WorkspaceResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new WorkspaceResult<T>(WorkspaceResultStatus.Success, value, error: null);
    }

    /// <summary>
    ///     Creates a result that completed without committing a new revision.
    /// </summary>
    /// <param name="value">Value produced by the operation.</param>
    /// <returns>
    ///     A result carrying the supplied value without a new revision.
    /// </returns>
    internal static WorkspaceResult<T> NoChange(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new WorkspaceResult<T>(WorkspaceResultStatus.NoChange, value, error: null);
    }

    /// <summary>
    ///     Creates a result carrying an expected failure.
    /// </summary>
    /// <param name="error">Expected failure description.</param>
    /// <returns>
    ///     A result carrying the expected failure and no value.
    /// </returns>
    internal static WorkspaceResult<T> Failure(WorkspaceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new WorkspaceResult<T>(WorkspaceResultStatus.Failure, value: default, error);
    }
}
