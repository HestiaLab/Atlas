namespace Atlas.Core.Validation;

/// <summary>
///     描述一次工作区不变式校验中发现的违规。
/// </summary>
internal sealed class InvariantViolation
{
    internal InvariantViolation(InvariantViolationCode code, string message)
    {
        Code = code;
        Message = message;
    }

    /// <summary>
    ///     获取违规的分类代码。
    /// </summary>
    internal InvariantViolationCode Code { get; }

    /// <summary>
    ///     获取人类可读的违规说明。
    /// </summary>
    internal string Message { get; }
}
