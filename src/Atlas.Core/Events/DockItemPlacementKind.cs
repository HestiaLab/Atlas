namespace Atlas.Core.Events;

/// <summary>Identifies the committed placement represented by an event snapshot.</summary>
public enum DockItemPlacementKind
{
    /// <summary>The item is placed in a layout group.</summary>
    Group = 0,

    /// <summary>The item is placed in an auto-hide strip.</summary>
    AutoHide = 1,
}
