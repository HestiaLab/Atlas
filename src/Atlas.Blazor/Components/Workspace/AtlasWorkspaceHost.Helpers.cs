using Atlas.Blazor.Content;
using Atlas.Blazor.Interop;
using Atlas.Core;
using Atlas.Core.Changes;
using Atlas.Core.Definitions;
using Atlas.Core.Events;
using Atlas.Core.Items;
using Atlas.Core.Layout;
using Atlas.Core.Operations;
using Atlas.Core.Placement;
using Atlas.Core.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using System.Globalization;

namespace Atlas.Blazor.Components;

/// <summary>
///     <see cref="AtlasWorkspaceHost"/> 的渲染辅助逻辑（partial）：
///     分割条呈现解析、子树隐藏判定与 CSS 样式派生。
/// </summary>
public sealed partial class AtlasWorkspaceHost
{
    private AtlasSplitterPresentation ResolveSplitterPresentation(LayoutNodeId splitId)
    {
        if (declarativeMode
            && declarativeSplitterPresentations.TryGetValue(splitId, out var presentation))
        {
            return presentation;
        }

        return SplitterPresentation?.Invoke(splitId)
            ?? new AtlasSplitterPresentation();
    }

    private static string SplitClass(SplitAxis axis)
    {
        return axis == SplitAxis.InlineChildren
            ? "atlas-v2-split atlas-v2-split-inline"
            : "atlas-v2-split atlas-v2-split-block";
    }

    private static string SplitStyle(SplitBasis basis)
    {
        string first;
        string second;
        if (basis.Kind == SplitBasisKind.Proportional)
        {
            first = $"minmax(0,{basis.Value.ToString("R", CultureInfo.InvariantCulture)}fr)";
            second =
                $"minmax(0,{(1d - basis.Value).ToString("R", CultureInfo.InvariantCulture)}fr)";
        }
        else if (basis.Anchor == SplitAnchor.First)
        {
            first = $"{basis.Value.ToString("R", CultureInfo.InvariantCulture)}px";
            second = "minmax(0,1fr)";
        }
        else
        {
            first = "minmax(0,1fr)";
            second = $"{basis.Value.ToString("R", CultureInfo.InvariantCulture)}px";
        }

        return $"--atlas-first-track:{first};--atlas-second-track:{second}";
    }

    private static bool IsSubtreeEffectivelyHidden(
        WorkspaceSnapshot snapshot,
        LayoutNodeId nodeId)
    {
        if (!snapshot.Nodes.TryGetValue(nodeId, out var node))
        {
            return true;
        }

        if (node is GroupNode group)
        {
            return group.Kind == DockItemKind.Tool
                && group.Visibility == GroupVisibility.Collapsed;
        }

        if (node is SplitNode split)
        {
            return IsSubtreeEffectivelyHidden(snapshot, split.FirstId)
                && IsSubtreeEffectivelyHidden(snapshot, split.SecondId);
        }

        return false;
    }

    private static string CollapseAdjustedStyle(
        bool firstCollapsed,
        bool secondCollapsed)
    {
        var first = firstCollapsed ? "0px" : "minmax(0,1fr)";
        var second = secondCollapsed ? "0px" : "minmax(0,1fr)";
        return $"--atlas-first-track:{first};--atlas-second-track:{second}";
    }

    private static string SplitRatio(SplitBasis basis)
    {
        return basis.Kind == SplitBasisKind.Proportional
            ? Invariant(basis.Value)
            : "0.5";
    }

    private static string Invariant(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string CssClasses(string required, string? optional)
    {
        return optional is null ? required : $"{required} {optional}";
    }

}
