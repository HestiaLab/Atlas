using Atlas.Core.Layout;

namespace Atlas.Core.Operations;

internal sealed class SetGroupVisibilityOperation : LayoutOperation
{
    internal SetGroupVisibilityOperation(
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

    internal LayoutNodeId GroupId { get; }

    internal GroupVisibility Visibility { get; }
}
