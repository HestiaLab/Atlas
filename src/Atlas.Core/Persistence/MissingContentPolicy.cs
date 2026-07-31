namespace Atlas.Core.Persistence;

/// <summary>Controls restore behavior for unregistered content references.</summary>
public enum MissingContentPolicy
{
    /// <summary>Preserves placement with conservative placeholder capabilities.</summary>
    PreservePlaceholder = 0,

    /// <summary>Removes unregistered items and their placements.</summary>
    DropUnknownItems = 1,

    /// <summary>Rejects the complete restore.</summary>
    FailRestore = 2,
}
