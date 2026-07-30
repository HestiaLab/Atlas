namespace Atlas.Core.Validation;

internal sealed class InvariantViolation
{
    internal InvariantViolation(InvariantViolationCode code, string message)
    {
        Code = code;
        Message = message;
    }

    internal InvariantViolationCode Code { get; }

    internal string Message { get; }
}
