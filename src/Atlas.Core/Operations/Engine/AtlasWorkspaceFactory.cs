using Atlas.Core.Definitions;
using Atlas.Core.Results;

namespace Atlas.Core.Operations.Engine;

internal sealed class AtlasWorkspaceFactory : IAtlasWorkspaceFactory
{
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
                resolvedOptions.History.MaxEntries);
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
