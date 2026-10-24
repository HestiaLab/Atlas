namespace Atlas.Core.Persistence;

/// <summary>
///     结构化描述一次持久化降级：受影响的工作区、失败类别与稳定诊断 ID；只
///     暴露类别，绝不暴露文档内容。
/// </summary>
public sealed class WorkspacePersistenceDiagnostic
{
    /// <summary>
    ///     创建诊断；工作区标识必须非默认，类别必须是已定义的枚举值，诊断 ID
    ///     不得为空。
    /// </summary>
    public WorkspacePersistenceDiagnostic(
        WorkspaceId workspaceId,
        WorkspacePersistenceDiagnosticKind kind,
        string diagnosticId)
    {
        if (workspaceId == default)
        {
            throw new ArgumentException("A diagnostic requires a workspace identity.", nameof(workspaceId));
        }

        if (!Enum.IsDefined(typeof(WorkspacePersistenceDiagnosticKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown diagnostic kind.");
        }

        WorkspaceId = workspaceId;
        Kind = kind;
        DiagnosticId = IdentifierGuard.RequireValue(diagnosticId, nameof(diagnosticId));
    }

    /// <summary>
    ///     获取受影响工作区的标识符。
    /// </summary>
    public WorkspaceId WorkspaceId { get; }

    /// <summary>
    ///     获取降级类别。
    /// </summary>
    public WorkspacePersistenceDiagnosticKind Kind { get; }

    /// <summary>
    ///     获取稳定诊断标识符。
    /// </summary>
    public string DiagnosticId { get; }
}
