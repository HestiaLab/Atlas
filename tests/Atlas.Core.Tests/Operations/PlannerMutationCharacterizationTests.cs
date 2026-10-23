using System.Security.Cryptography;
using System.Text;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Persistence;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

/// <summary>
///     锁定规划器候选构造的行为特征：一段移动/重排/打开序列的最终 XML
///     必须与重构前一致，用于验证快照编辑助手是纯机械替换。
/// </summary>
public sealed class PlannerMutationCharacterizationTests
{
    [Fact]
    public async Task Tool_move_reorder_open_sequence_keeps_layout_xml_stable()
    {
        var first = CreateToolGroup(
            "first",
            LogicalRegion.InlineStartUpper,
            "t1",
            "t2",
            "t3");
        var second = CreateToolGroup(
            "second",
            LogicalRegion.InlineEndUpper,
            "t4");
        var root = new SplitNode(
            new LayoutNodeId("root"),
            SplitAxis.InlineChildren,
            SplitBasis.Proportional(0.5),
            new SplitConstraints(),
            first.Id,
            second.Id);
        var snapshot = new WorkspaceSnapshot(
            new WorkspaceId("workspace"),
            revision: 0,
            root.Id,
            new[]
            {
                new KeyValuePair<LayoutNodeId, LayoutNode>(root.Id, root),
                new KeyValuePair<LayoutNodeId, LayoutNode>(first.Id, first),
                new KeyValuePair<LayoutNodeId, LayoutNode>(second.Id, second),
            },
            new[]
            {
                new KeyValuePair<DockItemId, DockItem>(new DockItemId("t1"), CreateTool("t1")),
                new KeyValuePair<DockItemId, DockItem>(new DockItemId("t2"), CreateTool("t2")),
                new KeyValuePair<DockItemId, DockItem>(new DockItemId("t3"), CreateTool("t3")),
                new KeyValuePair<DockItemId, DockItem>(new DockItemId("t4"), CreateTool("t4")),
            },
            Array.Empty<KeyValuePair<LogicalSide, ToolBarState>>(),
            new[]
            {
                new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    LogicalRegion.InlineStartUpper,
                    new ToolBarOrderState(
                        LogicalRegion.InlineStartUpper,
                        new[]
                        {
                            new DockItemId("t1"),
                            new DockItemId("t2"),
                            new DockItemId("t3"),
                        })),
                new KeyValuePair<LogicalRegion, ToolBarOrderState>(
                    LogicalRegion.InlineEndUpper,
                    new ToolBarOrderState(
                        LogicalRegion.InlineEndUpper,
                        new[] { new DockItemId("t4") })),
            },
            Array.Empty<KeyValuePair<WorkspaceEdge, AutoHideStripState>>());
        await using var workspace = new AtlasWorkspace(
            snapshot,
            new DefaultWorkspaceOperationPlanner());

        var move = await workspace.ExecuteAsync(
            new MoveToolOperation(new DockItemId("t2"), second.Id, targetIndex: 0));
        Assert.Equal(WorkspaceResultStatus.Success, move.Status);
        Assert.Equal(
            new[] { new DockItemId("t1"), new DockItemId("t3") },
            CurrentGroup(workspace, first.Id).ItemIds);
        Assert.Equal(
            new[] { new DockItemId("t2"), new DockItemId("t4") },
            CurrentGroup(workspace, second.Id).ItemIds);
        AssertOrder(workspace, LogicalRegion.InlineStartUpper, "t1", "t3");
        AssertOrder(workspace, LogicalRegion.InlineEndUpper, "t2", "t4");

        var reorder = await workspace.ExecuteAsync(
            new ReorderItemOperation(new DockItemId("t1"), first.Id, targetIndex: 1));
        Assert.Equal(WorkspaceResultStatus.Success, reorder.Status);
        Assert.Equal(
            new[] { new DockItemId("t3"), new DockItemId("t1") },
            CurrentGroup(workspace, first.Id).ItemIds);
        AssertOrder(workspace, LogicalRegion.InlineStartUpper, "t3", "t1");

        var open = await workspace.ExecuteAsync(
            new OpenItemOperation(
                CreateTool("t5"),
                second.Id,
                ContentDeduplicationMode.AllowMultiple));
        Assert.Equal(WorkspaceResultStatus.Success, open.Status);
        Assert.Equal(
            new[]
            {
                new DockItemId("t2"),
                new DockItemId("t4"),
                new DockItemId("t5"),
            },
            CurrentGroup(workspace, second.Id).ItemIds);
        AssertOrder(workspace, LogicalRegion.InlineEndUpper, "t2", "t4", "t5");

        var serializer = new AtlasXmlLayoutSerializer();
        var xml = serializer.Serialize(workspace.Current);
        // XmlWriter emits platform newlines; normalize so the golden hash is portable.
        var normalized = xml.Replace("\r\n", "\n", StringComparison.Ordinal);
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        Assert.True(
            hash == "D444D5279455D8EAB9A88CA50F9CF99A41E6CDC795E6437D9574095056DCF952",
            $"Actual hash: {hash}\nLayout XML:\n{xml}");

        Assert.Equal(WorkspaceResultStatus.Success, (await workspace.UndoAsync()).Status);
        Assert.Equal(
            new[] { new DockItemId("t2"), new DockItemId("t4") },
            CurrentGroup(workspace, second.Id).ItemIds);
        Assert.Equal(WorkspaceResultStatus.Success, (await workspace.UndoAsync()).Status);
        Assert.Equal(
            new[] { new DockItemId("t1"), new DockItemId("t3") },
            CurrentGroup(workspace, first.Id).ItemIds);
        Assert.Equal(WorkspaceResultStatus.Success, (await workspace.RedoAsync()).Status);
        Assert.Equal(WorkspaceResultStatus.Success, (await workspace.RedoAsync()).Status);
        Assert.Equal(xml, serializer.Serialize(workspace.Current));
    }

    private static GroupNode CurrentGroup(AtlasWorkspace workspace, LayoutNodeId groupId)
    {
        return Assert.IsType<GroupNode>(workspace.Current.Nodes[groupId]);
    }

    private static void AssertOrder(
        AtlasWorkspace workspace,
        LogicalRegion region,
        params string[] itemIds)
    {
        Assert.Equal(
            itemIds.Select(id => new DockItemId(id)),
            workspace.Current.ToolBarOrders[region].ItemIds);
    }

    private static GroupNode CreateToolGroup(
        string id,
        LogicalRegion region,
        params string[] itemIds)
    {
        return new GroupNode(
            new LayoutNodeId(id),
            DockItemKind.Tool,
            GroupRetentionPolicy.Persistent,
            GroupVisibility.Expanded,
            itemIds.Select(itemId => new DockItemId(itemId)),
            new DockItemId(itemIds[0]),
            logicalRegion: region);
    }

    private static DockItem CreateTool(string id)
    {
        return new DockItem(
            new DockItemId(id),
            DockItemKind.Tool,
            new ContentReference("tool", id),
            DockItemCapabilityPresets.Tool);
    }
}
