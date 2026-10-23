namespace Atlas.Core.Diagnostics;

/// <summary>
///     跨规划器与持久化路径共享的稳定诊断 ID/message key 目录。
/// </summary>
internal static class WorkspaceDiagnostics
{
    internal static class Ids
    {
        internal const string ItemMissing = "ATLAS-ITEM-MISSING";
        internal const string DefinitionInvalid = "ATLAS-DEFINITION-INVALID";
        internal const string PersistenceInvalid = "ATLAS-PERSIST-INVALID";
    }

    internal static class Messages
    {
        internal const string ItemNotFound = "Atlas.Item.NotFound";
        internal const string DefinitionInvalid = "Atlas.Definition.Invalid";
        internal const string PersistenceUnavailable = "Atlas.Persistence.Unavailable";
        internal const string PersistenceInvalid = "Atlas.Persistence.InvalidLayoutData";
    }
}
