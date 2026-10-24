namespace Atlas.Core.Operations;

/// <summary>
///     把文档移入编辑器 Group 或新的相邻分割。
/// </summary>
public sealed class DockDocumentOperation : LayoutOperation
{
    /// <summary>
    ///     初始化 <see cref="DockDocumentOperation"/> 类的新实例。
    /// </summary>
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

    /// <summary>
    ///     获取要移动的文档。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     获取锚定放置的编辑器 Group：中心区域合并到该 Group，方向区域则以它
    ///     为锚点创建新 Group 与分割。
    /// </summary>
    public LayoutNodeId TargetGroupId { get; }

    /// <summary>
    ///     获取逻辑放置区域。
    /// </summary>
    public DocumentDockZone Zone { get; }

    /// <summary>
    ///     获取方向放置创建的编辑器 Group 标识符；中心区域为
    ///     <see langword="null"/>。
    /// </summary>
    public LayoutNodeId? NewGroupId { get; }

    /// <summary>
    ///     获取方向放置创建的分割标识符；中心区域为
    ///     <see langword="null"/>。
    /// </summary>
    public LayoutNodeId? NewSplitId { get; }
}
