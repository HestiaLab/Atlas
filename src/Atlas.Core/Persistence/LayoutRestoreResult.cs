using Atlas.Core.Changes;

namespace Atlas.Core.Persistence;

/// <summary>
///     Reports a successfully committed workspace restore.
/// </summary>
public sealed class LayoutRestoreResult
{
    internal LayoutRestoreResult(
        LayoutChangeSet changeSet,
        LayoutRestoreDocument document)
    {
        ChangeSet = changeSet ?? throw new ArgumentNullException(nameof(changeSet));
        ArgumentNullException.ThrowIfNull(document);
        SourceSchemaVersion = document.SourceSchemaVersion;
        Warnings = document.Warnings;
        IdentityRemaps = document.IdentityRemaps;
        PlaceholderItems = document.PlaceholderItems;
        DroppedItems = document.DroppedItems;
    }

    /// <summary>
    ///     Gets the single atomic restore change set.
    /// </summary>
    public LayoutChangeSet ChangeSet { get; }

    /// <summary>
    ///     Gets the committed target revision.
    /// </summary>
    public long TargetRevision => ChangeSet.AfterRevision;

    /// <summary>
    ///     Gets the source schema version.
    /// </summary>
    public string SourceSchemaVersion { get; }

    /// <summary>
    ///     Gets the non-fatal migration warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    ///     Gets the identity remaps applied before the commit.
    /// </summary>
    public IReadOnlyList<IdentityRemap> IdentityRemaps { get; }

    /// <summary>
    ///     Gets the items retained as placeholders after the restore.
    /// </summary>
    public IReadOnlyList<DockItemId> PlaceholderItems { get; }

    /// <summary>
    ///     Gets the items removed by the missing content policy.
    /// </summary>
    public IReadOnlyList<DockItemId> DroppedItems { get; }
}
