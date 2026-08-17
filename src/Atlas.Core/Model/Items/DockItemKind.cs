namespace Atlas.Core.Items;

/// <summary>
///     标识停靠项的行为种类；决定承载组、允许的能力集与文档状态规则。
/// </summary>
public enum DockItemKind
{
    /// <summary>
    ///     由工具组承载的工具窗口项。
    /// </summary>
    Tool = 0,

    /// <summary>
    ///     由文档组承载的文档项。
    /// </summary>
    Document = 1,
}
