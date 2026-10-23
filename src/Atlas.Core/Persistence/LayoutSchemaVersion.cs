namespace Atlas.Core.Persistence;

/// <summary>
///     Version constants for the Atlas workspace persistence schema.
/// </summary>
public static class LayoutSchemaVersion
{
    /// <summary>
    ///     The schema version written by the current serializer and the only
    ///     version accepted by the current reader.
    /// </summary>
    public static string Current { get; } = "2.0";
}
