using Atlas.Core.Definitions;

namespace Atlas.Core.Operations;

/// <summary>
///     Atomically reconciles committed workspace state with a declarative definition.
/// </summary>
public sealed class ReconcileDefinitionOperation : LayoutOperation
{
    /// <summary>
    ///     Initializes a definition reconcile operation.
    /// </summary>
    public ReconcileDefinitionOperation(
        AtlasWorkspaceDefinition definition,
        long? baseRevision = null)
        : base(LayoutOperationKind.ReconcileDefinition, baseRevision)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>
    ///     Gets the immutable declarative definition.
    /// </summary>
    public AtlasWorkspaceDefinition Definition { get; }
}
