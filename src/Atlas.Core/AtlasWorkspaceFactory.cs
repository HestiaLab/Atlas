using Atlas.Core.Definitions;
using Atlas.Core.Diagnostics;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Results;

namespace Atlas.Core;

/// <summary>
///     公共工作区工厂：把声明式定义物化为初始快照，并使用默认操作规划器
///     创建工作区。
/// </summary>
public sealed class AtlasWorkspaceFactory : IAtlasWorkspaceFactory
{
    /// <summary>
    ///     从声明式定义创建可用的工作区；定义无效时返回类型化失败。
    /// </summary>
    /// <param name="definition">声明式工作区定义。</param>
    /// <param name="options">可选的工作区创建选项。</param>
    /// <returns>
    ///     成功时返回工作区；定义无效时返回类型化失败。
    /// </returns>
    public WorkspaceResult<IAtlasWorkspace> Create(
        AtlasWorkspaceDefinition definition,
        AtlasWorkspaceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var resolvedOptions = options ?? new AtlasWorkspaceOptions();
        try
        {
            var snapshot = WorkspaceDefinitionMaterializer.Create(
                definition,
                revision: 0);
            IAtlasWorkspace workspace = new AtlasWorkspace(
                snapshot,
                new DefaultWorkspaceOperationPlanner(resolvedOptions),
                resolvedOptions.History,
                subscriberFaultSink: resolvedOptions.SubscriberFaultSink);
            return WorkspaceResult<IAtlasWorkspace>.Success(workspace);
        }
        catch (ArgumentException)
        {
            return InvalidDefinition();
        }
    }

    private static WorkspaceResult<IAtlasWorkspace> InvalidDefinition()
    {
        return WorkspaceResult<IAtlasWorkspace>.Failure(
            new WorkspaceError(
                WorkspaceErrorCode.InvalidLayoutData,
                LayoutOperationKind.Factory,
                WorkspaceDiagnostics.Ids.DefinitionInvalid,
                WorkspaceDiagnostics.Messages.DefinitionInvalid));
    }
}
