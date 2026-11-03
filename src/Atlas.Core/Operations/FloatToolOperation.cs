using Atlas.Core.Placement;

namespace Atlas.Core.Operations;

/// <summary>
///     将工具条目浮出为独立浮动窗口。
/// </summary>
public sealed class FloatToolOperation : LayoutOperation
{
    /// <summary>
    ///     初始化浮出操作。
    /// </summary>
    /// <param name="itemId">要浮出的工具条目标识。</param>
    /// <param name="geometry">浮窗的宿主相对几何。</param>
    /// <param name="baseRevision">可选的乐观并发基线。</param>
    public FloatToolOperation(
        DockItemId itemId,
        FloatingWindowGeometry geometry,
        long? baseRevision = null)
        : base(LayoutOperationKind.FloatTool, baseRevision)
    {
        if (itemId == default)
        {
            throw new ArgumentException(
                "An item identifier cannot be the default value.",
                nameof(itemId));
        }

        if (geometry.IsDefault)
        {
            throw new ArgumentException(
                "A floating-window geometry cannot be the default value.",
                nameof(geometry));
        }

        ItemId = itemId;
        Geometry = geometry;
    }

    /// <summary>
    ///     获取要浮出的工具条目标识。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     获取浮窗的宿主相对几何。
    /// </summary>
    public FloatingWindowGeometry Geometry { get; }
}
