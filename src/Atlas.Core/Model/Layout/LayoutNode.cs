namespace Atlas.Core.Layout;

/// <summary>
///     布局树节点的抽象基类；树由 <see cref="GroupNode"/> 叶节点与
///     <see cref="SplitNode"/> 分割节点组成，构造时校验节点标识符不是默认值。
/// </summary>
public abstract class LayoutNode
{
    private protected LayoutNode(LayoutNodeId id)
    {
        if (id == default)
        {
            throw new ArgumentException(
                "A layout node identifier cannot be the default value.",
                nameof(id));
        }

        Id = id;
    }

    /// <summary>
    ///     工作区范围内的节点标识符。
    /// </summary>
    public LayoutNodeId Id { get; }
}
