namespace Atlas.Core;

internal static class IdentifierGuard
{
    public static string RequireValue(string? value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identifier values cannot be empty or whitespace.", parameterName);
        }

        return value;
    }
}
