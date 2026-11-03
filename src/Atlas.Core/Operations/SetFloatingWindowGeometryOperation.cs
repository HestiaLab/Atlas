using Atlas.Core.Placement;

namespace Atlas.Core.Operations;

/// <summary>
///     提交浮动窗口的移动或缩放几何。
/// </summary>
public sealed class SetFloatingWindowGeometryOperation : LayoutOperation
{
    /// <summary>
    ///     初始化浮窗几何提交操作。
    /// </summary>
    /// <param name="itemId">浮动窗口所属的工具条目标识。</param>
    /// <param name="geometry">新的宿主相对几何。</param>
    /// <param name="baseRevision">可选的乐观并发基线。</param>
    public SetFloatingWindowGeometryOperation(
        DockItemId itemId,
        FloatingWindowGeometry geometry,
        long? baseRevision = null)
        : base(LayoutOperationKind.SetFloatingWindowGeometry, baseRevision)
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
    ///     获取浮动窗口所属的工具条目标识。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     获取新的宿主相对几何。
    /// </summary>
    public FloatingWindowGeometry Geometry { get; }
}
