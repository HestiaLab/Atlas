using Atlas.Core.Definitions;

namespace Atlas.Core.Operations;

/// <summary>
///     原子地将已提交工作区状态与声明式定义对账。
/// </summary>
public sealed class ReconcileDefinitionOperation : LayoutOperation
{
    /// <summary>
    ///     初始化定义对账操作。
    /// </summary>
    public ReconcileDefinitionOperation(
        AtlasWorkspaceDefinition definition,
        long? baseRevision = null)
        : base(LayoutOperationKind.ReconcileDefinition, baseRevision)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>
    ///     获取不可变的声明式定义。
    /// </summary>
    public AtlasWorkspaceDefinition Definition { get; }
}
