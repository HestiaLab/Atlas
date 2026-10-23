using Atlas.Core.Items;

namespace Atlas.Core.Persistence;

/// <summary>
///     Configures parsing limits, content registration checks, the missing content
///     policy and reserved identities used during restore.
/// </summary>
public sealed class LayoutRestoreOptions
{
    /// <summary>
    ///     Creates the options; the missing content policy must be a defined
    ///     enumeration value, and reserved identities must be non-default and unique.
    /// </summary>
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

    /// <summary>
    ///     Gets the parser resource limits.
    /// </summary>
    public LayoutPersistenceLimits Limits { get; }

    /// <summary>
    ///     Gets the optional side-effect-free content registration predicate.
    /// </summary>
    public Func<ContentReference, bool>? IsContentRegistered { get; }

    /// <summary>
    ///     Gets the missing content behavior.
    /// </summary>
    public MissingContentPolicy MissingContentPolicy { get; }

    /// <summary>
    ///     Gets the identities in the target scope that must be preserved.
    /// </summary>
    public IReadOnlyList<DockItemId> ReservedItemIds { get; }
}
