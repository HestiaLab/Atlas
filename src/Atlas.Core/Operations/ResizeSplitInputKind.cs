namespace Atlas.Core.Operations;

/// <summary>Identifies the input source of a committed split resize.</summary>
public enum ResizeSplitInputKind
{
    /// <summary>The resize is the terminal commit of a pointer interaction.</summary>
    Pointer = 0,

    /// <summary>The resize is one step in a keyboard interaction burst.</summary>
    Keyboard = 1,
}
