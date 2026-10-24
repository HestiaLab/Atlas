using Atlas.Core.Items;
using Atlas.Core.Operations;

namespace Atlas.Core;

/// <summary>
///     定义不可变的工作区创建期服务与历史行为。
/// </summary>
public sealed class AtlasWorkspaceOptions
{
    /// <summary>
    ///     创建不可变选项；历史行为默认为默认历史选项，内容身份策略与关闭
    ///     守卫解析器为可选注入。
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
    ///     获取有界历史行为。
    /// </summary>
    public WorkspaceHistoryOptions History { get; }

    /// <summary>
    ///     获取可选的自定义内容身份策略。
    /// </summary>
    public IContentIdentityPolicy? ContentIdentityPolicy { get; }

    /// <summary>
    ///     获取可选的条目关闭守卫解析器。
    /// </summary>
    public IDockItemCloseGuardResolver? CloseGuardResolver { get; }

    /// <summary>
    ///     获取或初始化可选回调；当 Changed/领域事件订阅者抛出异常时调用；
    ///     回调失败与事件分发隔离。
    /// </summary>
    public Action<Exception>? SubscriberFaultSink { get; init; }
}
