namespace Atlas.Core.Items;

/// <summary>
///     标识停靠条目的行为种类；决定承载它的 Group、允许的能力集与文档状态
///     规则。
/// </summary>
public enum DockItemKind
{
    /// <summary>
    ///     由工具 Group 承载的工具窗口条目。
    /// </summary>
    Tool = 0,

    /// <summary>
    ///     由文档 Group 承载的文档条目。
    /// </summary>
    Document = 1,
}
