using Atlas.Core.Items;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     默认内容标识策略：内容引用按值相等即视为同一内容。
/// </summary>
internal sealed class DefaultContentIdentityPolicy : IContentIdentityPolicy
{
    /// <summary>
    ///     获取策略的无状态共享单例；按值比较内容引用，无需每实例持有状态。
    /// </summary>
    internal static DefaultContentIdentityPolicy Instance { get; } = new();

    private DefaultContentIdentityPolicy()
    {
    }

    /// <summary>
    ///     判断两个内容引用是否标识同一内容。
    /// </summary>
    /// <param name="left">参与比较的内容引用。</param>
    /// <param name="right">参与比较的内容引用。</param>
    /// <returns>
    ///     两个引用按值相等时返回 <see langword="true"/>。
    /// </returns>
    public bool AreEquivalent(ContentReference left, ContentReference right)
    {
        return left == right;
    }
}
