namespace Atlas.Core.Operations;

/// <summary>Moves a document into an editor group or a new adjacent split.</summary>
public sealed class DockDocumentOperation : LayoutOperation
{
    /// <summary>Initializes a document docking operation.</summary>
    public DockDocumentOperation(
        DockItemId itemId,
        LayoutNodeId targetGroupId,
        DocumentDockZone zone,
        LayoutNodeId? newGroupId = null,
        LayoutNodeId? newSplitId = null,
        long? baseRevision = null)
        : base(
            zone == DocumentDockZone.Center
                ? LayoutOperationKind.MoveItem
                : LayoutOperationKind.SplitItem,
            baseRevision)
    {
        if (itemId == default)
        {
            throw new ArgumentException("An item identifier cannot be the default value.", nameof(itemId));
        }

        if (targetGroupId == default)
        {
            throw new ArgumentException("A target group identifier cannot be the default value.", nameof(targetGroupId));
        }

        if (zone is < DocumentDockZone.Center or > DocumentDockZone.BlockEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(zone), zone, "Unknown document dock zone.");
        }

        if (zone == DocumentDockZone.Center)
        {
            if (newGroupId is not null || newSplitId is not null)
            {
                throw new ArgumentException("A center dock cannot supply new node identifiers.", nameof(newGroupId));
            }
        }
        else
        {
            if (newGroupId is null || newGroupId == default)
            {
                throw new ArgumentException("A directional dock requires a new group identifier.", nameof(newGroupId));
            }

            if (newSplitId is null || newSplitId == default)
            {
                throw new ArgumentException("A directional dock requires a new split identifier.", nameof(newSplitId));
            }

            if (newGroupId == newSplitId || newGroupId == targetGroupId || newSplitId == targetGroupId)
            {
                throw new ArgumentException("Docking node identifiers must be distinct.", nameof(newGroupId));
            }
        }

        ItemId = itemId;
        TargetGroupId = targetGroupId;
        Zone = zone;
        NewGroupId = newGroupId;
        NewSplitId = newSplitId;
    }

    /// <summary>Gets the document to move.</summary>
    public DockItemId ItemId { get; }

    /// <summary>Gets the editor group under the resolved drop zone.</summary>
    public LayoutNodeId TargetGroupId { get; }

    /// <summary>Gets the logical drop zone.</summary>
    public DocumentDockZone Zone { get; }

    /// <summary>Gets the new editor group identifier for a directional drop.</summary>
    public LayoutNodeId? NewGroupId { get; }

    /// <summary>Gets the new split identifier for a directional drop.</summary>
    public LayoutNodeId? NewSplitId { get; }
}
