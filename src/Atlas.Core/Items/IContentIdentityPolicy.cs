namespace Atlas.Core.Items;

/// <summary>
///     确定两个内容引用是否共享同一去重标识。
/// </summary>
public interface IContentIdentityPolicy
{
    /// <summary>
    ///     确定两个内容引用在去重时是否等价。
    /// </summary>
    bool AreEquivalent(ContentReference left, ContentReference right);
}
