using Atlas.Core.Items;

namespace Atlas.Core.Operations.Engine;

internal sealed class DefaultContentIdentityPolicy : IContentIdentityPolicy
{
    internal static DefaultContentIdentityPolicy Instance { get; } = new();

    private DefaultContentIdentityPolicy()
    {
    }

    public bool AreEquivalent(ContentReference left, ContentReference right)
    {
        return left == right;
    }
}
