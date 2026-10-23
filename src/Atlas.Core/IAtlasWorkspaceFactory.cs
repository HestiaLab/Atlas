using Atlas.Core.Definitions;
using Atlas.Core.Results;

namespace Atlas.Core;

/// <summary>
///     Creates fully validated, isolated Atlas workspaces.
/// </summary>
public interface IAtlasWorkspaceFactory
{
    /// <summary>
    ///     Creates a workspace from an immutable definition.
    /// </summary>
    WorkspaceResult<IAtlasWorkspace> Create(
        AtlasWorkspaceDefinition definition,
        AtlasWorkspaceOptions? options = null);
}
