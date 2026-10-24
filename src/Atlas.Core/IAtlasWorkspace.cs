using Atlas.Core.Changes;
using Atlas.Core.Events;
using Atlas.Core.Operations;
using Atlas.Core.Results;

namespace Atlas.Core;

/// <summary>
///     提供对单个隔离 Atlas 工作区的原子访问。
/// </summary>
public interface IAtlasWorkspace : IAsyncDisposable
{
    /// <summary>
    ///     获取稳定的工作区标识符。
    /// </summary>
    WorkspaceId Id { get; }

    /// <summary>
    ///     获取最新已提交的不可变快照。
    /// </summary>
    WorkspaceSnapshot Current { get; }

    /// <summary>
    ///     获取一个值，指示已提交操作是否可撤销。
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    ///     获取一个值，指示已撤销操作是否可重做。
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    ///     获取由已提交变更派生的强类型事件。
    /// </summary>
    IAtlasWorkspaceDomainEvents DomainEvents { get; }

    /// <summary>
    ///     在新快照提交后发生。
    /// </summary>
    event EventHandler<WorkspaceChangedEventArgs>? Changed;

    /// <summary>
    ///     原子执行一个操作。
    /// </summary>
    /// <param name="operation">不可变操作请求。</param>
    /// <param name="cancellationToken">用于取消待处理异步工作的令牌。</param>
    /// <returns>
    ///     已提交的变更、无变化结果或预期失败。
    /// </returns>
    ValueTask<WorkspaceResult<LayoutChangeSet>> ExecuteAsync(
        LayoutOperation operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     在已提交历史中后退一步。
    /// </summary>
    /// <param name="cancellationToken">用于取消待处理异步工作的令牌。</param>
    /// <returns>
    ///     已提交的撤销变更、无变化结果或预期失败。
    /// </returns>
    ValueTask<WorkspaceResult<LayoutChangeSet>> UndoAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     在先前已撤销的历史中前进一步。
    /// </summary>
    /// <param name="cancellationToken">用于取消待处理异步工作的令牌。</param>
    /// <returns>
    ///     已提交的重做变更、无变化结果或预期失败。
    /// </returns>
    ValueTask<WorkspaceResult<LayoutChangeSet>> RedoAsync(
        CancellationToken cancellationToken = default);
}
