namespace Atlas.Core.Items;

/// <summary>Defines how opening an item handles an existing content identity.</summary>
public enum ContentDeduplicationMode
{
    /// <summary>Activates the existing item without creating another occurrence.</summary>
    ActivateExisting = 0,

    /// <summary>Allows multiple items to reference the same content identity.</summary>
    AllowMultiple = 1,

    /// <summary>Reuses the existing item identity and placement with replacement content.</summary>
    ReplaceExisting = 2,

    /// <summary>Rejects an existing content identity with a typed failure.</summary>
    RejectDuplicate = 3,
}
