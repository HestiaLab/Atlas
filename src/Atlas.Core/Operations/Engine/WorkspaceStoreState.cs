namespace Atlas.Core.Operations.Engine;

internal sealed class WorkspaceStoreState
{
    internal WorkspaceStoreState(
        WorkspaceSnapshot snapshot,
        IReadOnlyList<WorkspacePatch>? undo = null,
        IReadOnlyList<WorkspacePatch>? redo = null)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        Undo = undo ?? Array.Empty<WorkspacePatch>();
        Redo = redo ?? Array.Empty<WorkspacePatch>();
    }

    internal WorkspaceSnapshot Snapshot { get; }

    internal IReadOnlyList<WorkspacePatch> Undo { get; }

    internal IReadOnlyList<WorkspacePatch> Redo { get; }
}
