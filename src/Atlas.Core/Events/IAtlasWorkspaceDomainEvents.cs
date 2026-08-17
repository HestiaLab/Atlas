namespace Atlas.Core.Events;

/// <summary>
///     公开从已提交工作区变更派生的强类型事件。
/// </summary>
public interface IAtlasWorkspaceDomainEvents
{
    /// <summary>
    ///     当文档项被打开时发生。
    /// </summary>
    event EventHandler<ItemOpenedEventArgs>? ItemOpened;

    /// <summary>
    ///     当文档项被关闭时发生。
    /// </summary>
    event EventHandler<ItemClosedEventArgs>? ItemClosed;

    /// <summary>
    ///     当项在组中被选中时发生。
    /// </summary>
    event EventHandler<ItemActivatedEventArgs>? ItemActivated;

    /// <summary>
    ///     当工具项被创建时发生。
    /// </summary>
    event EventHandler<ToolCreatedEventArgs>? ToolCreated;

    /// <summary>
    ///     当工具项被移除时发生。
    /// </summary>
    event EventHandler<ToolRemovedEventArgs>? ToolRemoved;

    /// <summary>
    ///     在持久化的工作区恢复之后发生。
    /// </summary>
    event EventHandler<WorkspaceRestoredEventArgs>? WorkspaceRestored;
}
