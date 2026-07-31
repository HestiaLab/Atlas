namespace Atlas.Core.Items;

/// <summary>Determines whether two content references share one deduplication identity.</summary>
public interface IContentIdentityPolicy
{
    /// <summary>Determines whether two content references are equivalent for deduplication.</summary>
    bool AreEquivalent(ContentReference left, ContentReference right);
}
