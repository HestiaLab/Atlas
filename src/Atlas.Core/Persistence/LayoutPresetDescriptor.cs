namespace Atlas.Core.Persistence;

/// <summary>Describes one named persisted workspace snapshot.</summary>
public sealed class LayoutPresetDescriptor
{
    /// <summary>Initializes preset metadata.</summary>
    public LayoutPresetDescriptor(
        string name,
        string schemaVersion,
        long savedRevision,
        DateTimeOffset? savedAt = null)
    {
        Name = RequireName(name);
        SchemaVersion = IdentifierGuard.RequireValue(schemaVersion, nameof(schemaVersion));
        if (savedRevision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(savedRevision),
                savedRevision,
                "A saved revision cannot be negative.");
        }

        SavedRevision = savedRevision;
        SavedAt = savedAt;
    }

    /// <summary>Gets the ordinal preset name.</summary>
    public string Name { get; }

    /// <summary>Gets the persisted schema version.</summary>
    public string SchemaVersion { get; }

    /// <summary>Gets the captured runtime revision.</summary>
    public long SavedRevision { get; }

    /// <summary>Gets the optional capture timestamp.</summary>
    public DateTimeOffset? SavedAt { get; }

    internal static string RequireName(string? name)
    {
        var value = IdentifierGuard.RequireValue(name, nameof(name));
        if (value.Length > 128
            || value.Any(char.IsControl)
            || value.Contains('/')
            || value.Contains('\\')
            || value.StartsWith("__atlas_", StringComparison.Ordinal))
        {
            throw new ArgumentException("The preset name is not a valid storage key.", nameof(name));
        }

        return value;
    }
}
