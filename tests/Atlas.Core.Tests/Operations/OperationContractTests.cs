using System.Reflection;
using Atlas.Core.Changes;
using Atlas.Core.Operations;
using Atlas.Core.Results;
using Xunit;

namespace Atlas.Core.Tests.Operations;

public sealed class OperationContractTests
{
    [Fact]
    public void Operation_and_error_codes_have_stable_values()
    {
        Assert.Equal(0, (int)LayoutOperationKind.OpenItem);
        Assert.Equal(15, (int)LayoutOperationKind.ReorderToolBarEntry);
        Assert.Equal(1, (int)WorkspaceErrorCode.LayoutVersionIncompatible);
        Assert.Equal(14, (int)WorkspaceErrorCode.DragSessionExpired);
        Assert.Equal(15, (int)WorkspaceErrorCode.TargetNotFound);
        Assert.Equal(16, (int)WorkspaceErrorCode.DuplicateContentIdentity);
    }

    [Fact]
    public void Result_states_are_mutually_exclusive()
    {
        var change = LayoutChangeSet.Empty(4, LayoutChangeKind.Operation);
        var error = new WorkspaceError(
            WorkspaceErrorCode.StaleOperation,
            LayoutOperationKind.SelectItem,
            "ATLAS-OP-STALE",
            "Atlas.Operation.Stale");

        var success = WorkspaceResult<LayoutChangeSet>.Success(change);
        var noChange = WorkspaceResult<LayoutChangeSet>.NoChange(change);
        var failure = WorkspaceResult<LayoutChangeSet>.Failure(error);

        Assert.Equal(WorkspaceResultStatus.Success, success.Status);
        Assert.True(success.Succeeded);
        Assert.False(success.IsNoChange);
        Assert.Same(change, success.Value);
        Assert.Null(success.Error);

        Assert.Equal(WorkspaceResultStatus.NoChange, noChange.Status);
        Assert.True(noChange.Succeeded);
        Assert.True(noChange.IsNoChange);
        Assert.Same(change, noChange.Value);
        Assert.Null(noChange.Error);

        Assert.Equal(WorkspaceResultStatus.Failure, failure.Status);
        Assert.False(failure.Succeeded);
        Assert.False(failure.IsNoChange);
        Assert.Null(failure.Value);
        Assert.Same(error, failure.Error);
    }

    [Fact]
    public void Change_set_is_precise_and_defensively_copied()
    {
        var updatedNodes = new List<LayoutNodeId>
        {
            new("group"),
            new("group"),
        };
        var updatedItems = new List<DockItemId> { new("editor") };

        var change = new LayoutChangeSet(
            beforeRevision: 7,
            afterRevision: 8,
            LayoutChangeKind.Operation,
            updatedNodes: updatedNodes,
            updatedItems: updatedItems);

        updatedNodes.Clear();
        updatedItems.Clear();

        Assert.Equal(7, change.BeforeRevision);
        Assert.Equal(8, change.AfterRevision);
        Assert.Equal(LayoutChangeKind.Operation, change.ChangeKind);
        Assert.Equal(new[] { new LayoutNodeId("group") }, change.UpdatedNodes);
        Assert.Equal(new[] { new DockItemId("editor") }, change.UpdatedItems);
        Assert.Empty(change.AddedNodes);
        Assert.Empty(change.RemovedNodes);
    }

    [Fact]
    public void Public_contracts_are_read_only()
    {
        Assert.Empty(GetWritablePublicProperties(typeof(LayoutChangeSet)));
        Assert.Empty(GetWritablePublicProperties(typeof(WorkspaceChangedEventArgs)));
        Assert.Empty(GetWritablePublicProperties(typeof(WorkspaceError)));
        Assert.Empty(GetWritablePublicProperties(typeof(WorkspaceResult<LayoutChangeSet>)));
        Assert.Empty(typeof(LayoutOperation).GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }

    private static IEnumerable<PropertyInfo> GetWritablePublicProperties(Type type)
    {
        return type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanWrite);
    }
}
