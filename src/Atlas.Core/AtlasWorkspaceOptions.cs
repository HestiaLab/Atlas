using Atlas.Core.Items;
using Atlas.Core.Operations;

namespace Atlas.Core;

/// <summary>
///     定义不可变的创建时工作区服务与历史行为。
/// </summary>
public sealed class AtlasWorkspaceOptions
{
    /// <summary>
    ///     构造不可变选项；历史缺省为默认历史选项，
    ///     内容标识策略与关闭守卫解析器可选注入。
    /// </summary>
    public AtlasWorkspaceOptions(
        WorkspaceHistoryOptions? history = null,
        IContentIdentityPolicy? contentIdentityPolicy = null,
        IDockItemCloseGuardResolver? closeGuardResolver = null)
    {
        History = history ?? new WorkspaceHistoryOptions();
        ContentIdentityPolicy = contentIdentityPolicy;
        CloseGuardResolver = closeGuardResolver;
    }

    /// <summary>
    ///     获取有界的历史行为。
    /// </summary>
    public WorkspaceHistoryOptions History { get; }

    /// <summary>
    ///     获取可选的自定义内容标识策略。
    /// </summary>
    public IContentIdentityPolicy? ContentIdentityPolicy { get; }

    /// <summary>
    ///     获取可选的项关闭守卫解析器。
    /// </summary>
    public IDockItemCloseGuardResolver? CloseGuardResolver { get; }
}
