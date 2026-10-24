namespace Atlas.Core.Items;

/// <summary>
///     判断两个内容引用是否共享同一去重身份。
/// </summary>
public interface IContentIdentityPolicy
{
    /// <summary>
    ///     判断两个内容引用在去重时是否等价。
    /// </summary>
    bool AreEquivalent(ContentReference left, ContentReference right);
}
