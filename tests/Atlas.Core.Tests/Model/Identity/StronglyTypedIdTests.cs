using System.Reflection;
using Xunit;

namespace Atlas.Core.Tests.Model.Identity;

public sealed class StronglyTypedIdTests
{
    [Fact]
    public void Workspace_id_preserves_a_valid_value()
    {
        var id = new WorkspaceId("workspace-main");

        Assert.Equal("workspace-main", id.Value);
        Assert.Equal("workspace-main", id.ToString());
    }

    [Fact]
    public void Layout_node_id_preserves_a_valid_value()
    {
        var id = new LayoutNodeId("node-editor");

        Assert.Equal("node-editor", id.Value);
        Assert.Equal("node-editor", id.ToString());
    }

    [Fact]
    public void Dock_item_id_preserves_a_valid_value()
    {
        var id = new DockItemId("item-document-1");

        Assert.Equal("item-document-1", id.Value);
        Assert.Equal("item-document-1", id.ToString());
    }

    [Fact]
    public void Identifier_equality_is_ordinal()
    {
        var lower = new DockItemId("document");
        var same = new DockItemId("document");
        var upper = new DockItemId("Document");

        Assert.True(lower == same);
        Assert.False(lower != same);
        Assert.Equal(lower, same);
        Assert.Equal(lower.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(lower, upper);
    }

    [Fact]
    public void Identifier_types_are_not_interchangeable()
    {
        object workspaceId = new WorkspaceId("shared");
        object nodeId = new LayoutNodeId("shared");
        object itemId = new DockItemId("shared");

        Assert.NotEqual(workspaceId, nodeId);
        Assert.NotEqual(workspaceId, itemId);
        Assert.NotEqual(nodeId, itemId);
    }

    [Fact]
    public void Workspace_id_rejects_invalid_values()
    {
        AssertRejectsInvalidValues(value => new WorkspaceId(value));
    }

    [Fact]
    public void Layout_node_id_rejects_invalid_values()
    {
        AssertRejectsInvalidValues(value => new LayoutNodeId(value));
    }

    [Fact]
    public void Dock_item_id_rejects_invalid_values()
    {
        AssertRejectsInvalidValues(value => new DockItemId(value));
    }

    [Fact]
    public void Default_identifiers_do_not_expose_a_value()
    {
        Assert.Throws<InvalidOperationException>(() => default(WorkspaceId).Value);
        Assert.Throws<InvalidOperationException>(() => default(LayoutNodeId).Value);
        Assert.Throws<InvalidOperationException>(() => default(DockItemId).Value);
    }

    [Fact]
    public void Identifiers_do_not_define_string_conversion_operators()
    {
        AssertHasNoConversionOperators(typeof(WorkspaceId));
        AssertHasNoConversionOperators(typeof(LayoutNodeId));
        AssertHasNoConversionOperators(typeof(DockItemId));
    }

    private static void AssertRejectsInvalidValues<TIdentifier>(
        Func<string, TIdentifier> create)
    {
        Assert.Throws<ArgumentNullException>(() => create(null!));
        Assert.Throws<ArgumentException>(() => create(string.Empty));
        Assert.Throws<ArgumentException>(() => create(" \t\r\n"));
    }

    private static void AssertHasNoConversionOperators(Type identifierType)
    {
        var conversionOperators = identifierType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name is "op_Implicit" or "op_Explicit");

        Assert.Empty(conversionOperators);
    }
}
