namespace Atlas.Core.Results;

/// <summary>
///     类型化的操作结果：成功或无变更时携带值，失败时携带预期错误。
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
    ///     获取结果分类。
    /// </summary>
    public WorkspaceResultStatus Status { get; }

    /// <summary>
    ///     获取操作是否在未发生预期失败的情况下完成。
    /// </summary>
    public bool Succeeded => Status is WorkspaceResultStatus.Success or WorkspaceResultStatus.NoChange;

    /// <summary>
    ///     获取操作是否在未提交新修订的情况下完成。
    /// </summary>
    public bool IsNoChange => Status == WorkspaceResultStatus.NoChange;

    /// <summary>
    ///     获取成功及无变更结果的结果值。
    /// </summary>
    public T? Value { get; }

    /// <summary>
    ///     获取操作未成功时的预期失败。
    /// </summary>
    public WorkspaceError? Error { get; }

    /// <summary>
    ///     创建成功完成的操作结果。
    /// </summary>
    /// <param name="value">操作产生的值。</param>
    /// <returns>
    ///     携带所给值的成功结果。
    /// </returns>
    internal static WorkspaceResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new WorkspaceResult<T>(WorkspaceResultStatus.Success, value, error: null);
    }

    /// <summary>
    ///     创建未提交新修订的操作结果。
    /// </summary>
    /// <param name="value">操作产生的值。</param>
    /// <returns>
    ///     携带所给值且未提交新修订的结果。
    /// </returns>
    internal static WorkspaceResult<T> NoChange(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new WorkspaceResult<T>(WorkspaceResultStatus.NoChange, value, error: null);
    }

    /// <summary>
    ///     创建携带预期失败的操作结果。
    /// </summary>
    /// <param name="error">预期失败描述。</param>
    /// <returns>
    ///     携带预期失败且无值的结果。
    /// </returns>
    internal static WorkspaceResult<T> Failure(WorkspaceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new WorkspaceResult<T>(WorkspaceResultStatus.Failure, value: default, error);
    }
}
