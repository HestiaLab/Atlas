using Atlas.Core.Layout;

namespace Atlas.Core.Operations;

/// <summary>
///     更改工具组的已提交可见性。
/// </summary>
public sealed class SetGroupVisibilityOperation : LayoutOperation
{
    /// <summary>
    ///     初始化组可见性操作。
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
    ///     获取目标组。
    /// </summary>
    public LayoutNodeId GroupId { get; }

    /// <summary>
    ///     获取请求的已提交可见性。
    /// </summary>
    public GroupVisibility Visibility { get; }
}
