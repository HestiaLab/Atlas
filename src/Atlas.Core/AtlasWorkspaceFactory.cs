using Atlas.Core.Definitions;
using Atlas.Core.Diagnostics;
using Atlas.Core.Operations;
using Atlas.Core.Operations.Engine;
using Atlas.Core.Results;

namespace Atlas.Core;

/// <summary>
///     Public workspace factory: materializes a declarative definition into an
///     initial snapshot and creates a workspace with the default operation planner.
/// </summary>
public sealed class AtlasWorkspaceFactory : IAtlasWorkspaceFactory
{
    /// <summary>
    ///     Creates a usable workspace from a declarative definition; returns a
    ///     typed failure when the definition is invalid.
    /// </summary>
    /// <param name="definition">Declarative workspace definition.</param>
    /// <param name="options">Optional workspace creation options.</param>
    /// <returns>
    ///     The workspace on success; a typed failure when the definition is invalid.
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
