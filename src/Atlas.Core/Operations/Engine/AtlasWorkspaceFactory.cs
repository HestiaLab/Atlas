using Atlas.Core.Definitions;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

/// <summary>
///     工作区工厂实现：将声明式定义物化为初始快照，并装配默认操作规划器创建工作区实例。
/// </summary>
internal sealed class AtlasWorkspaceFactory : IAtlasWorkspaceFactory
{
    /// <summary>
    ///     从声明式定义创建可用的工作区实例；定义无效时返回结构化失败结果。
    /// </summary>
    /// <param name="definition">声明式工作区定义。</param>
    /// <param name="options">可选的创建工作区选项。</param>
    /// <returns>
    ///     创建成功返回工作区实例；定义无效返回失败结果。
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
                resolvedOptions.History);
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
                LayoutOperationKind.ResetWorkspace,
                "ATLAS-DEFINITION-INVALID",
                "Atlas.Definition.Invalid"));
    }
}
