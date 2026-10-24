namespace Atlas.Core.Events;

/// <summary>
///     公开由已提交工作区变更派生的强类型事件。
/// </summary>
public interface IAtlasWorkspaceDomainEvents
{
    /// <summary>
    ///     在文档条目打开时发生。
    /// </summary>
    event EventHandler<ItemOpenedEventArgs>? ItemOpened;

    /// <summary>
    ///     在文档条目关闭时发生。
    /// </summary>
    event EventHandler<ItemClosedEventArgs>? ItemClosed;

    /// <summary>
    ///     在 Group 中选中条目时发生。
    /// </summary>
    event EventHandler<ItemActivatedEventArgs>? ItemActivated;

    /// <summary>
    ///     在创建工具条目时发生。
    /// </summary>
    event EventHandler<ToolCreatedEventArgs>? ToolCreated;

    /// <summary>
    ///     在移除工具条目时发生。
    /// </summary>
    event EventHandler<ToolRemovedEventArgs>? ToolRemoved;

    /// <summary>
    ///     在持久化工作区恢复后发生。
    /// </summary>
    event EventHandler<WorkspaceRestoredEventArgs>? WorkspaceRestored;
}
