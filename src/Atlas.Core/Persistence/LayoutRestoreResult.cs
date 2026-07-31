using Atlas.Core.Changes;

namespace Atlas.Core.Persistence;

/// <summary>Reports one successfully committed workspace restore.</summary>
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

    /// <summary>Gets the single atomic restore change.</summary>
    public LayoutChangeSet ChangeSet { get; }

    /// <summary>Gets the committed target revision.</summary>
    public long TargetRevision => ChangeSet.AfterRevision;

    /// <summary>Gets the source schema version.</summary>
    public string SourceSchemaVersion { get; }

    /// <summary>Gets non-fatal migration warnings.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Gets identity remaps applied before commit.</summary>
    public IReadOnlyList<IdentityRemap> IdentityRemaps { get; }

    /// <summary>Gets safe placeholder items.</summary>
    public IReadOnlyList<DockItemId> PlaceholderItems { get; }

    /// <summary>Gets items removed by missing-content policy.</summary>
    public IReadOnlyList<DockItemId> DroppedItems { get; }
}
