using Atlas.Core.Definitions;
using Atlas.Core.Results;

namespace Atlas.Core;

/// <summary>
///     创建经过完全验证的隔离 Atlas 工作区。
/// </summary>
public interface IAtlasWorkspaceFactory
{
    /// <summary>
    ///     从不可变定义创建工作区。
    /// </summary>
    WorkspaceResult<IAtlasWorkspace> Create(
        AtlasWorkspaceDefinition definition,
        AtlasWorkspaceOptions? options = null);
}
