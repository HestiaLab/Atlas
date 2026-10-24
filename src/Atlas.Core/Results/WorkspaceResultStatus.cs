namespace Atlas.Core.Results;

/// <summary>
///     标识操作是已提交、无变化，还是按预期失败。
/// </summary>
public enum WorkspaceResultStatus
{
    /// <summary>
    ///     操作提交了新修订。
    /// </summary>
    Success = 0,

    /// <summary>
    ///     操作完成但未改变快照。
    /// </summary>
    NoChange = 1,

    /// <summary>
    ///     操作以预期的工作区错误失败。
    /// </summary>
    Failure = 2,
}
