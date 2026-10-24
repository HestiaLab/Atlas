using Atlas.Core.Layout;

namespace Atlas.Core.Operations;

/// <summary>
///     改变工具 Group 的已提交可见性。
/// </summary>
public sealed class SetGroupVisibilityOperation : LayoutOperation
{
    /// <summary>
    ///     初始化 Group 可见性操作。
    /// </summary>
    public SetGroupVisibilityOperation(
        LayoutNodeId groupId,
        GroupVisibility visibility,
        long? baseRevision = null)
        : base(LayoutOperationKind.SetGroupVisibility, baseRevision)
    {
        if (groupId == default)
        {
            throw new ArgumentException(
                "A group identifier cannot be the default value.",
                nameof(groupId));
        }

        if (visibility is not GroupVisibility.Expanded
            and not GroupVisibility.Collapsed)
        {
            throw new ArgumentOutOfRangeException(
                nameof(visibility),
                visibility,
                "Unknown group visibility.");
        }

        GroupId = groupId;
        Visibility = visibility;
    }

    /// <summary>
    ///     获取目标 Group。
    /// </summary>
    public LayoutNodeId GroupId { get; }

    /// <summary>
    ///     获取请求的已提交可见性。
    /// </summary>
    public GroupVisibility Visibility { get; }
}
