namespace Atlas.Core.Diagnostics;

/// <summary>
///     跨规划器与持久化路径共享的稳定诊断 ID/message key 目录。
/// </summary>
internal static class WorkspaceDiagnostics
{
    internal static class Ids
    {
        internal const string AutoRead = "ATLAS-AUTO-READ";
        internal const string AutoSerialize = "ATLAS-AUTO-SERIALIZE";
        internal const string AutoWrite = "ATLAS-AUTO-WRITE";
        internal const string CloseCapability = "ATLAS-CLOSE-CAPABILITY";
        internal const string CloseGuard = "ATLAS-CLOSE-GUARD";
        internal const string CloseGuardFault = "ATLAS-CLOSE-GUARD-FAULT";
        internal const string DefinitionInvalid = "ATLAS-DEFINITION-INVALID";
        internal const string DockCapability = "ATLAS-DOCK-CAPABILITY";
        internal const string DockIdCollision = "ATLAS-DOCK-ID-COLLISION";
        internal const string DockSourceRejected = "ATLAS-DOCK-SOURCE-REJECTED";
        internal const string DockTargetMissing = "ATLAS-DOCK-TARGET-MISSING";
        internal const string DockTargetRejected = "ATLAS-DOCK-TARGET-REJECTED";
        internal const string GroupMissing = "ATLAS-GROUP-MISSING";
        internal const string GroupVisibilityRejected = "ATLAS-GROUP-VISIBILITY-REJECTED";
        internal const string ItemMissing = "ATLAS-ITEM-MISSING";
        internal const string MoveCapability = "ATLAS-MOVE-CAPABILITY";
        internal const string MoveIndexRejected = "ATLAS-MOVE-INDEX-REJECTED";
        internal const string MoveTargetMissing = "ATLAS-MOVE-TARGET-MISSING";
        internal const string MoveTargetRejected = "ATLAS-MOVE-TARGET-REJECTED";
        internal const string OpStale = "ATLAS-OP-STALE";
        internal const string OpenContentDuplicate = "ATLAS-OPEN-CONTENT-DUPLICATE";
        internal const string OpenDuplicatePlacement = "ATLAS-OPEN-DUPLICATE-PLACEMENT";
        internal const string OpenIdDuplicate = "ATLAS-OPEN-ID-DUPLICATE";
        internal const string OpenIndexRejected = "ATLAS-OPEN-INDEX-REJECTED";
        internal const string OpenPinnedPrefix = "ATLAS-OPEN-PINNED-PREFIX";
        internal const string OpenTargetMissing = "ATLAS-OPEN-TARGET-MISSING";
        internal const string OpenTargetRejected = "ATLAS-OPEN-TARGET-REJECTED";
        internal const string PersistenceInvalid = "ATLAS-PERSIST-INVALID";
        internal const string PersistInvariant = "ATLAS-PERSIST-INVARIANT";
        internal const string PersistLimit = "ATLAS-PERSIST-LIMIT";
        internal const string PersistRoot = "ATLAS-PERSIST-ROOT";
        internal const string PersistSchema = "ATLAS-PERSIST-SCHEMA";
        internal const string PersistVersion = "ATLAS-PERSIST-VERSION";
        internal const string PersistWorkspace = "ATLAS-PERSIST-WORKSPACE";
        internal const string PresetDelete = "ATLAS-PRESET-DELETE";
        internal const string PresetList = "ATLAS-PRESET-LIST";
        internal const string PresetMissing = "ATLAS-PRESET-MISSING";
        internal const string PresetName = "ATLAS-PRESET-NAME";
        internal const string PresetRead = "ATLAS-PRESET-READ";
        internal const string PresetSerialize = "ATLAS-PRESET-SERIALIZE";
        internal const string PresetWrite = "ATLAS-PRESET-WRITE";
        internal const string ReorderCapability = "ATLAS-REORDER-CAPABILITY";
        internal const string ReorderGroupMissing = "ATLAS-REORDER-GROUP-MISSING";
        internal const string ReorderGroupRejected = "ATLAS-REORDER-GROUP-REJECTED";
        internal const string ReorderTarget = "ATLAS-REORDER-TARGET";
        internal const string RestoreContent = "ATLAS-RESTORE-CONTENT";
        internal const string RestorePolicy = "ATLAS-RESTORE-POLICY";
        internal const string RestoreWorkspace = "ATLAS-RESTORE-WORKSPACE";
        internal const string SelectNoGroup = "ATLAS-SELECT-NO-GROUP";
        internal const string SplitMissing = "ATLAS-SPLIT-MISSING";
        internal const string SplitTargetRejected = "ATLAS-SPLIT-TARGET-REJECTED";
        internal const string ToolbarItemRejected = "ATLAS-TOOLBAR-ITEM-REJECTED";
        internal const string ToolbarMissing = "ATLAS-TOOLBAR-MISSING";
        internal const string ToolbarRegionMissing = "ATLAS-TOOLBAR-REGION-MISSING";
        internal const string ToolbarTargetRejected = "ATLAS-TOOLBAR-TARGET-REJECTED";
        internal const string V1Invariant = "ATLAS-V1-INVARIANT";
        internal const string V1NarrowContract = "ATLAS-V1-NARROW-CONTRACT";
        internal const string V1Shape = "ATLAS-V1-SHAPE";
        internal const string V1SizeIgnored = "ATLAS-V1-SIZE-IGNORED";
        internal const string V1TopologyFallback = "ATLAS-V1-TOPOLOGY-FALLBACK";
    }

    internal static class Messages
    {
        internal const string CloseCancelledByGuard = "Atlas.Close.CancelledByGuard";
        internal const string CloseCapabilityRejected = "Atlas.Close.CapabilityRejected";
        internal const string CloseGuardFaulted = "Atlas.Close.GuardFaulted";
        internal const string DefinitionInvalid = "Atlas.Definition.Invalid";
        internal const string DockCapabilityRejected = "Atlas.Dock.CapabilityRejected";
        internal const string DockSourceRejected = "Atlas.Dock.SourceRejected";
        internal const string DockTargetNotFound = "Atlas.Dock.TargetNotFound";
        internal const string DockTargetRejected = "Atlas.Dock.TargetRejected";
        internal const string GroupNotFound = "Atlas.Group.NotFound";
        internal const string GroupVisibilityRejected = "Atlas.Group.VisibilityRejected";
        internal const string GroupVisibilityRejectedOnEmptyGroup = "Atlas.Group.VisibilityRejectedOnEmptyGroup";
        internal const string ItemNotFound = "Atlas.Item.NotFound";
        internal const string MoveCapabilityRejected = "Atlas.Move.CapabilityRejected";
        internal const string MoveTargetNotFound = "Atlas.Move.TargetNotFound";
        internal const string MoveTargetRejected = "Atlas.Move.TargetRejected";
        internal const string OpenDuplicateContentIdentity = "Atlas.Open.DuplicateContentIdentity";
        internal const string OpenDuplicateItemId = "Atlas.Open.DuplicateItemId";
        internal const string OpenTargetNotFound = "Atlas.Open.TargetNotFound";
        internal const string OpenTargetRejected = "Atlas.Open.TargetRejected";
        internal const string OperationStale = "Atlas.Operation.Stale";
        internal const string PersistenceContentNotRegistered = "Atlas.Persistence.ContentNotRegistered";
        internal const string PersistenceInvalid = "Atlas.Persistence.InvalidLayoutData";
        internal const string PersistenceUnavailable = "Atlas.Persistence.Unavailable";
        internal const string PersistenceVersionIncompatible = "Atlas.Persistence.VersionIncompatible";
        internal const string PresetNotFound = "Atlas.Preset.NotFound";
        internal const string ReorderCapabilityRejected = "Atlas.Reorder.CapabilityRejected";
        internal const string ReorderGroupNotFound = "Atlas.Reorder.GroupNotFound";
        internal const string ReorderTargetRejected = "Atlas.Reorder.TargetRejected";
        internal const string RestoreWorkspaceMismatch = "Atlas.Restore.WorkspaceMismatch";
        internal const string SelectNoGroup = "Atlas.Select.NoGroup";
        internal const string SplitNotFound = "Atlas.Split.NotFound";
        internal const string SplitTargetRejected = "Atlas.Split.TargetRejected";
        internal const string ToolBarItemRejected = "Atlas.ToolBar.ItemRejected";
        internal const string ToolBarNotFound = "Atlas.ToolBar.NotFound";
        internal const string ToolBarRegionNotFound = "Atlas.ToolBar.RegionNotFound";
        internal const string ToolBarTargetRejected = "Atlas.ToolBar.TargetRejected";
    }
}
