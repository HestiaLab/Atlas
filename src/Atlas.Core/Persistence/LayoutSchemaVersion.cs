namespace Atlas.Core.Persistence;

/// <summary>
///     Atlas 工作区持久化 schema 的版本常量。
/// </summary>
public static class LayoutSchemaVersion
{
    /// <summary>
    ///     当前序列化器写出的 schema 版本，也是当前读取器唯一接受的版本。
    /// </summary>
    public static string Current { get; } = "2.0";
}
