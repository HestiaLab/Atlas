using Atlas.Core.Layout;

namespace Atlas.Core.Operations;

/// <summary>
///     Changes the committed visibility of a tool group.
/// </summary>
public sealed class SetGroupVisibilityOperation : LayoutOperation
{
    /// <summary>
    ///     Initializes a group visibility operation.
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
    ///     Gets the target group.
    /// </summary>
    public LayoutNodeId GroupId { get; }

    /// <summary>
    ///     Gets the requested committed visibility.
    /// </summary>
    public GroupVisibility Visibility { get; }
}
