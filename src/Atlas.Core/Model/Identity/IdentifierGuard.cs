namespace Atlas.Core;

/// <summary>
///     所有标识符值类型共用的构造期参数守卫：
///     统一拒绝 null 与空白，保证标识一旦存在即携带有效值。
/// </summary>
internal static class IdentifierGuard
{
    /// <summary>
    ///     标识符构造的单一校验关口：null 抛 <see cref="ArgumentNullException"/>，
    ///     空白抛 <see cref="ArgumentException"/>，通过后原样返回。
    /// </summary>
    /// <param name="value">待校验的标识符值。</param>
    /// <param name="parameterName">校验失败时用于异常参数的名称。</param>
    /// <returns>
    ///     原样返回通过校验的标识符值。
    /// </returns>
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
