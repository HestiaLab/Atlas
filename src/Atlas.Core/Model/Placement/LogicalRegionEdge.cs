namespace Atlas.Core.Placement;

/// <summary>
///     逻辑工具区域到工作区边缘的稳定映射，供 dock-back 回退与自动隐藏还原使用。
/// </summary>
internal static class LogicalRegionEdge
{
    internal static bool IsDefined(LogicalRegion region)
    {
        return region is LogicalRegion.InlineStartUpper
            or LogicalRegion.InlineStartLower
            or LogicalRegion.InlineEndUpper
            or LogicalRegion.InlineEndLower
            or LogicalRegion.BlockEndInlineStart
            or LogicalRegion.BlockEndInlineEnd;
    }

    internal static WorkspaceEdge ToEdge(LogicalRegion region)
    {
        return region switch
        {
            LogicalRegion.InlineStartUpper or LogicalRegion.InlineStartLower =>
                WorkspaceEdge.InlineStart,
            LogicalRegion.InlineEndUpper or LogicalRegion.InlineEndLower =>
                WorkspaceEdge.InlineEnd,
            LogicalRegion.BlockEndInlineStart or LogicalRegion.BlockEndInlineEnd =>
                WorkspaceEdge.BlockEnd,
            _ => throw new ArgumentOutOfRangeException(
                nameof(region),
                region,
                "Unknown logical region."),
        };
    }
}
