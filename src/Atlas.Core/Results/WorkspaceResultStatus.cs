namespace Atlas.Core.Results;

/// <summary>
///     标识操作是已提交、未变更还是按预期失败。
/// </summary>
public enum WorkspaceResultStatus
{
    /// <summary>
    ///     操作提交了新修订。
    /// </summary>
    Success = 0,

    /// <summary>
    ///     操作在未更改快照的情况下完成。
    /// </summary>
    NoChange = 1,

    /// <summary>
    ///     操作因预期的工作区错误而失败。
    /// </summary>
    Failure = 2,
}
