namespace Atlas.Showcase.Components;

/// <summary>
///     教学面板共享的代码片段。修改公共 API 时先同步这里，
///     再检查 <c>GuidePanel</c> 与各面板是否仍引用同一常量；
///     声明式片段以实际编译的 <c>Pages/DeclarativeExample.razor</c> 为准。
/// </summary>
internal static class ShowcaseSnippets
{
    internal const string Install = @"// csproj
<PackageReference Include=""Atlas.Blazor"" Version=""0.3.0"" />

// Program.cs（Blazor WASM）
builder.Services.AddAtlasWorkspace();";

    internal const string DeclarativeLayout = @"@* _Imports.razor
   @using Atlas.Blazor.Components
   @using Atlas.Core
   @using Atlas.Core.Items
   @using Atlas.Core.Layout
   @using Atlas.Core.Placement *@
<AtlasWorkspaceHost WorkspaceId=""@(new WorkspaceId(""demo""))"">
  <ChildContent>
    <AtlasSplitDefinition Id=""@(new LayoutNodeId(""root""))""
                          Axis=""SplitAxis.InlineChildren"">
      <AtlasToolGroupDefinition Id=""@(new LayoutNodeId(""tools""))""
                                LogicalRegion=""LogicalRegion.InlineStartUpper"">
        <AtlasToolItemDefinition Id=""@(new DockItemId(""explorer""))""
                                 ContentKind=""explorer""
                                 ResourceId=""explorer""
                                 Title=""Explorer"" />
      </AtlasToolGroupDefinition>
      <AtlasDocumentGroupDefinition Id=""@(new LayoutNodeId(""documents""))"">
        <AtlasDocumentItemDefinition Id=""@(new DockItemId(""readme""))""
                                     ContentKind=""markdown""
                                     ResourceId=""readme""
                                     Title=""Readme.md"" />
      </AtlasDocumentGroupDefinition>
    </AtlasSplitDefinition>
  </ChildContent>
</AtlasWorkspaceHost>";

    internal const string CreateWorkspace = @"var result = Factory.Create(
    ShowcaseWorkspaceDefinition.Create(""atlas-showcase""));
if (!result.Succeeded || result.Value is null)
{
    throw new InvalidOperationException(
        result.Error?.DiagnosticId ?? ""Init failed."");
}

var workspace = result.Value;
workspace.Changed += OnWorkspaceChanged;";

    internal const string ContentRoute = @"<AtlasContentRoute Kind=""explorer"">
  <ChildContent Context=""content"">
    <ExplorerPanel />
  </ChildContent>
</AtlasContentRoute>

// 另一种方式：Program.cs 中 DI 注册
builder.Services.AddAtlasContent<ExplorerPanel>(""explorer"");";

    internal const string Interaction = @"<AtlasWorkspaceHost Workspace=""workspace"" EnableInteractions=""true"">
  …
</AtlasWorkspaceHost>";

    internal const string DragDrop = @"// 无需配置：EnableInteractions=""true"" 时 AtlasWorkspaceHost
// 自动接线拖拽会话。
<AtlasWorkspaceHost Workspace=""workspace"" EnableInteractions=""true"">
  …
</AtlasWorkspaceHost>

// 程序化停靠（Core）：
await workspace.ExecuteAsync(new DockDocumentOperation(
    itemId, targetGroupId, DocumentDockZone.InlineStart));";

    internal const string SplitNode = @"var split = new SplitNode(
    new LayoutNodeId(""main""),
    SplitAxis.InlineChildren,
    SplitBasis.Proportional(0.5),
    new SplitConstraints(firstMinimum: 150, firstMaximum: 380),
    firstId,
    secondId);

// 调整大小提交（Core）：
await workspace.ExecuteAsync(new ResizeSplitOperation(
    splitId, SplitBasis.FixedPixels(240, SplitAnchor.First)));";

    internal const string KeyboardBinding = @"// 默认绑定（子集）
AtlasKeyboardCommand.NextTab             -> Ctrl+Tab
AtlasKeyboardCommand.CloseActiveDocument -> Ctrl+W / Ctrl+F4

// 自定义绑定：
var bindings = new[]
{
    new AtlasKeyboardBinding(
        new AtlasKeyboardChord(""KeyQ"", control: true),
        AtlasKeyboardCommand.CloseActiveDocument),
};
var map = new AtlasKeyboardCommandMap(bindings);

// Razor
<AtlasWorkspaceHost KeyboardCommands=""@map"">…</AtlasWorkspaceHost>";

    internal const string Theme = @"<!-- index.html —— 在 atlas.css 之后链接可选 IDE 主题 -->
<link rel=""stylesheet"" href=""_content/Atlas.Blazor/atlas-v2/atlas.css"" />
<link rel=""stylesheet"" href=""_content/Atlas.Blazor/atlas-v2/themes/ide.css"" />

/* 仍可覆盖公开 --atlas-* 变量（放在主题之后） */
.atlas-v2-workspace {
  --atlas-accent: #0b6bcb;
}";

    internal const string Persistence = @"var serializer = new AtlasXmlLayoutSerializer();
string xml = serializer.Serialize(workspace.Current);

var options = new LayoutRestoreOptions(
    isContentRegistered: reference => IsRegistered(reference.Kind),
    missingContentPolicy: MissingContentPolicy.PreservePlaceholder);
var document = serializer.DeserializeWithOptions(
    xml, workspace.Id, options);
if (!document.Succeeded || document.Value is null)
{
    return;
}

await workspace.ExecuteAsync(
    new RestoreWorkspaceOperation(document.Value));";
}
