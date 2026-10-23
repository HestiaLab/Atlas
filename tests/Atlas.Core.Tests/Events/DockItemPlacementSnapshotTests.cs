using Atlas.Core.Events;
using Atlas.Core.Layout;
using Atlas.Core.Placement;
using Xunit;

namespace Atlas.Core.Tests.Events;

public sealed class DockItemPlacementSnapshotTests
{
    [Fact]
    public void Group_placement_exposes_group_identity_and_index()
    {
        var groupId = new LayoutNodeId("group");

        var placement = new DockItemPlacementSnapshot(groupId, 2);

        Assert.Equal(DockItemPlacementKind.Group, placement.Kind);
        Assert.Equal(groupId, placement.GroupId);
        Assert.Null(placement.AutoHideEdge);
        Assert.Equal(2, placement.Index);
    }

    [Fact]
    public void Auto_hide_placement_exposes_edge_and_index()
    {
        var placement = new DockItemPlacementSnapshot(WorkspaceEdge.InlineEnd, 1);

        Assert.Equal(DockItemPlacementKind.AutoHide, placement.Kind);
        Assert.Null(placement.GroupId);
        Assert.Equal(WorkspaceEdge.InlineEnd, placement.AutoHideEdge);
        Assert.Equal(1, placement.Index);
    }

    [Fact]
    public void Placement_rejects_unknown_edge_and_negative_index()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DockItemPlacementSnapshot(WorkspaceEdge.BlockEnd, -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DockItemPlacementSnapshot((WorkspaceEdge)99, 0));
    }
}
