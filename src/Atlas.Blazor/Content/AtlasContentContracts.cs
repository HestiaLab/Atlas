using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Results;
using Microsoft.AspNetCore.Components;

namespace Atlas.Blazor.Content;

/// <summary>
///     为已渲染内容提供绑定到固定项的请求命令。
///     每条命令将对应 LayoutOperation 原子提交到工作区；
///     预期失败以 WorkspaceResult 状态返回，不抛异常。
/// </summary>
public sealed class AtlasContentCommands
{
    // 命令提交的目标工作区。
    private readonly IAtlasWorkspace workspace;

    // 命令绑定的固定项；所有操作都作用于它。
    private readonly DockItemId itemId;

    internal AtlasContentCommands(IAtlasWorkspace workspace, DockItemId itemId)
    {
        this.workspace = workspace;
        this.itemId = itemId;
    }

    /// <summary>
    ///     提交 SelectItemOperation：选中当前项，使其成为所在组的活动项。
    /// </summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> ActivateAsync(
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(new SelectItemOperation(itemId), cancellationToken);
    }

    /// <summary>
    ///     提交 CloseItemOperation：是否关闭由工作区按 CanClose 能力与关闭守卫裁决，
    ///     拒绝时返回失败结果。
    /// </summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> CloseAsync(
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(new CloseItemOperation(itemId), cancellationToken);
    }

    /// <summary>
    ///     提交 UpdateItemPresentationOperation 更新标题与图标元数据；
    ///     title 为 null 时恢复标识默认值，iconKey 为 null 时清除图标。
    /// </summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> UpdatePresentationAsync(
        string? title,
        string? iconKey,
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(
            new UpdateItemPresentationOperation(itemId, title, iconKey),
            cancellationToken);
    }
}

/// <summary>
///     出口渲染器收到的不可变内容上下文：携带工作区与项标识、不可变项快照、
///     绑定命令，以及按引用解析逐项业务状态的入口。
/// </summary>
public sealed class AtlasContentContext
{
    // 逐项业务状态作用域；为 null 时状态获取回退到每次调用工厂。
    private readonly IAtlasContentStateScope? stateScope;

    internal AtlasContentContext(IAtlasWorkspace workspace, DockItem item)
        : this(workspace, item, stateScope: null)
    {
    }

    internal AtlasContentContext(
        IAtlasWorkspace workspace,
        DockItem item,
        IAtlasContentStateScope? stateScope)
    {
        WorkspaceId = workspace.Id;
        ItemId = item.Id;
        Reference = item.Content;
        Item = item;
        Commands = new AtlasContentCommands(workspace, item.Id);
        this.stateScope = stateScope;
    }

    /// <summary>
    ///     获取用于划分共享状态的宿主工作区作用域。
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     获取当前 UI 出现标识。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     获取渲染器与资源标识。
    /// </summary>
    public ContentReference Reference { get; }

    /// <summary>
    ///     获取已渲染修订处的不可变项。
    /// </summary>
    public DockItem Item { get; }

    /// <summary>
    ///     获取绑定到当前项的命令。
    /// </summary>
    public AtlasContentCommands Commands { get; }

    /// <summary>
    ///     获取为 <see cref="Reference"/> 解析的逐项业务状态；
    ///     当尚未创建任何状态或没有可用状态作用域时为 <see langword="null"/>。
    ///     查询从不创建状态。
    /// </summary>
    public TState? GetState<TState>()
        where TState : class
    {
        if (stateScope is null)
        {
            return null;
        }

        return stateScope.TryGetState<TState>(Reference, out var state) ? state : null;
    }

    /// <summary>
    ///     获取为 <see cref="Reference"/> 解析的逐项业务状态，
    ///     需要时通过 <paramref name="factory"/> 创建。
    ///     没有状态作用域时工厂在每次调用时运行（向后兼容的回退）。
    /// </summary>
    public TState GetOrCreateState<TState>(Func<TState> factory)
        where TState : class
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        if (stateScope is null)
        {
            return factory();
        }

        return stateScope.GetOrCreate(Reference, factory);
    }
}

/// <summary>
///     由内容提供程序解析的 Atlas 内容基类；
///     Content 参数由出口渲染时自动注入（DI 与 Map 来源均如此），派生组件无需自行获取。
/// </summary>
public abstract class AtlasContentComponent : ComponentBase
{
    /// <summary>
    ///     出口在渲染时注入的项上下文；[EditorRequired] 表示它由 Atlas 渲染管道提供，
    ///     而非可选参数。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public AtlasContentContext Content { get; set; } = null!;
}

/// <summary>
///     为组空状态提供不可变上下文。
/// </summary>
public sealed class AtlasEmptyStateContext
{
    internal AtlasEmptyStateContext(
        WorkspaceId workspaceId,
        LayoutNodeId groupId,
        DockItemKind groupKind,
        GroupRetentionPolicy retentionPolicy)
    {
        WorkspaceId = workspaceId;
        GroupId = groupId;
        GroupKind = groupKind;
        RetentionPolicy = retentionPolicy;
    }

    /// <summary>
    ///     获取工作区标识。
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     获取空组标识。
    /// </summary>
    public LayoutNodeId GroupId { get; }

    /// <summary>
    ///     获取组项种类。
    /// </summary>
    public DockItemKind GroupKind { get; }

    /// <summary>
    ///     获取组保留策略。
    /// </summary>
    public GroupRetentionPolicy RetentionPolicy { get; }
}

/// <summary>
///     向头部模板提供不可变项数据。
/// </summary>
public sealed class AtlasItemHeaderContext
{
    internal AtlasItemHeaderContext(DockItem item, bool isSelected)
    {
        Item = item;
        IsSelected = isSelected;
    }

    /// <summary>
    ///     获取已渲染的项。
    /// </summary>
    public DockItem Item { get; }

    /// <summary>
    ///     获取项是否在其组中被选中。
    /// </summary>
    public bool IsSelected { get; }
}
