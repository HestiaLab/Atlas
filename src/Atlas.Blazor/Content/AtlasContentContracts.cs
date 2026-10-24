using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Results;
using Microsoft.AspNetCore.Components;

namespace Atlas.Blazor.Content;

/// <summary>
///     为已渲染内容提供绑定到固定条目的请求命令。每个命令都会把对应的
///     LayoutOperation 原子提交到工作区；预期失败以 WorkspaceResult 状态
///     返回，绝不抛出异常。
/// </summary>
public sealed class AtlasContentCommands
{
    // 命令提交的目标工作区。
    private readonly IAtlasWorkspace workspace;

    // 命令作用的固定条目。
    private readonly DockItemId itemId;

    internal AtlasContentCommands(IAtlasWorkspace workspace, DockItemId itemId)
    {
        this.workspace = workspace;
        this.itemId = itemId;
    }

    /// <summary>
    ///     提交 SelectItemOperation：选中该条目，使其成为所属 Group 的活动
    ///     条目。
    /// </summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> ActivateAsync(
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(new SelectItemOperation(itemId), cancellationToken);
    }

    /// <summary>
    ///     提交 CloseItemOperation：是否继续关闭由工作区根据 CanClose 能力与
    ///     关闭守卫决定；拒绝时返回失败结果。
    /// </summary>
    public ValueTask<WorkspaceResult<LayoutChangeSet>> CloseAsync(
        CancellationToken cancellationToken = default)
    {
        return workspace.ExecuteAsync(new CloseItemOperation(itemId), cancellationToken);
    }

    /// <summary>
    ///     提交 UpdateItemPresentationOperation 以更新标题与图标元数据；null
    ///     标题恢复身份默认值，null iconKey 清除图标。
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
///     交给出口渲染器的不可变内容上下文：携带工作区与条目身份、不可变条目
///     快照、绑定命令，以及按引用访问业务状态的访问器。
/// </summary>
public sealed class AtlasContentContext
{
    // 按条目的业务状态作用域；为 null 时每次调用都回退为运行工厂。
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
    ///     获取用于分区共享状态的 Host 工作区作用域。
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     获取该出口的具体视图身份。
    /// </summary>
    public DockItemId ItemId { get; }

    /// <summary>
    ///     获取渲染器与资源身份。
    /// </summary>
    public ContentReference Reference { get; }

    /// <summary>
    ///     获取渲染修订处的不可变条目。
    /// </summary>
    public DockItem Item { get; }

    /// <summary>
    ///     获取绑定到该条目的命令。
    /// </summary>
    public AtlasContentCommands Commands { get; }

    /// <summary>
    ///     获取为 <see cref="Reference"/> 解析的按条目业务状态；未创建状态
    ///     或没有可用状态作用域时为 <see langword="null"/>。查询绝不会创建
    ///     状态。
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
    ///     获取为 <see cref="Reference"/> 解析的按条目业务状态，需要时通过
    ///     <paramref name="factory"/> 创建。没有状态作用域时每次调用都会
    ///     运行工厂（向后兼容回退）。
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
///     通过 Atlas 内容提供程序解析的内容基类；出口会为 DI 与 Map 两种来源
///     自动注入 Content 参数，因此派生组件无需自行获取。
/// </summary>
public abstract class AtlasContentComponent : ComponentBase
{
    /// <summary>
    ///     出口在渲染时注入的条目上下文；[EditorRequired] 表示它由 Atlas
    ///     渲染管线提供，而不是可选参数。
    /// </summary>
    [Parameter]
    [EditorRequired]
    public AtlasContentContext Content { get; set; } = null!;
}

/// <summary>
///     为 Group 空状态提供不可变上下文。
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
    ///     获取工作区标识符。
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     获取空 Group 标识符。
    /// </summary>
    public LayoutNodeId GroupId { get; }

    /// <summary>
    ///     获取 Group 条目种类。
    /// </summary>
    public DockItemKind GroupKind { get; }

    /// <summary>
    ///     获取 Group 保留策略。
    /// </summary>
    public GroupRetentionPolicy RetentionPolicy { get; }
}

/// <summary>
///     为标题模板提供不可变条目数据。
/// </summary>
public sealed class AtlasItemHeaderContext
{
    internal AtlasItemHeaderContext(DockItem item, bool isSelected)
    {
        Item = item;
        IsSelected = isSelected;
    }

    /// <summary>
    ///     获取已渲染条目。
    /// </summary>
    public DockItem Item { get; }

    /// <summary>
    ///     获取条目是否在其 Group 中选中。
    /// </summary>
    public bool IsSelected { get; }
}
