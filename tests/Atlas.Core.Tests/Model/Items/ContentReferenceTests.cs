using Atlas.Core.Items;
using Xunit;

namespace Atlas.Core.Tests.Model.Items;

public sealed class ContentReferenceTests
{
    [Fact]
    public void Reference_preserves_kind_and_resource_identity()
    {
        var reference = new ContentReference("document-editor", "/src/main.cs");

        Assert.Equal("document-editor", reference.Kind);
        Assert.Equal("/src/main.cs", reference.ResourceId);
    }

    [Fact]
    public void Reference_rejects_invalid_kind()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ContentReference(null!, "resource"));
        Assert.Throws<ArgumentException>(
            () => new ContentReference(string.Empty, "resource"));
        Assert.Throws<ArgumentException>(
            () => new ContentReference(" \t", "resource"));
    }

    [Fact]
    public void Reference_rejects_invalid_resource_identity()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ContentReference("kind", null!));
        Assert.Throws<ArgumentException>(
            () => new ContentReference("kind", string.Empty));
        Assert.Throws<ArgumentException>(
            () => new ContentReference("kind", "\r\n"));
    }

    [Fact]
    public void Reference_equality_is_ordinal_for_both_parts()
    {
        var first = new ContentReference("editor", "resource");
        var same = new ContentReference("editor", "resource");
        var differentKindCase = new ContentReference("Editor", "resource");
        var differentResourceCase = new ContentReference("editor", "Resource");

        Assert.True(first == same);
        Assert.False(first != same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(first, differentKindCase);
        Assert.NotEqual(first, differentResourceCase);
    }

    [Fact]
    public void Default_reference_does_not_expose_identity_parts()
    {
        Assert.Throws<InvalidOperationException>(() => default(ContentReference).Kind);
        Assert.Throws<InvalidOperationException>(() => default(ContentReference).ResourceId);
    }
}
