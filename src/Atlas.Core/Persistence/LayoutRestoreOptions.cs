using Atlas.Core.Items;

namespace Atlas.Core.Persistence;

/// <summary>Configures content resolution and target identity conflicts during restore.</summary>
public sealed class LayoutRestoreOptions
{
    /// <summary>Initializes restore options.</summary>
    public LayoutRestoreOptions(
        LayoutPersistenceLimits? limits = null,
        Func<ContentReference, bool>? isContentRegistered = null,
        MissingContentPolicy missingContentPolicy = MissingContentPolicy.PreservePlaceholder,
        IEnumerable<DockItemId>? reservedItemIds = null)
    {
        if (!Enum.IsDefined(typeof(MissingContentPolicy), missingContentPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(missingContentPolicy),
                missingContentPolicy,
                "Unknown missing-content policy.");
        }

        Limits = limits ?? new LayoutPersistenceLimits();
        IsContentRegistered = isContentRegistered;
        MissingContentPolicy = missingContentPolicy;
        var reserved = (reservedItemIds ?? Array.Empty<DockItemId>()).ToArray();
        if (reserved.Any(id => id == default) || reserved.Distinct().Count() != reserved.Length)
        {
            throw new ArgumentException(
                "Reserved identities must be non-default and unique.",
                nameof(reservedItemIds));
        }

        ReservedItemIds = Array.AsReadOnly(reserved);
    }

    /// <summary>Gets parser resource limits.</summary>
    public LayoutPersistenceLimits Limits { get; }

    /// <summary>Gets the optional pure content registration predicate.</summary>
    public Func<ContentReference, bool>? IsContentRegistered { get; }

    /// <summary>Gets missing-content behavior.</summary>
    public MissingContentPolicy MissingContentPolicy { get; }

    /// <summary>Gets target-scoped identities that must be preserved.</summary>
    public IReadOnlyList<DockItemId> ReservedItemIds { get; }
}
