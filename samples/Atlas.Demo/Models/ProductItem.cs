namespace Atlas.Demo.Models;

/// <summary>
/// One row of the parent-owned product collection shared between the action
/// panel and the table adapter.
/// </summary>
public sealed record ProductItem(string Id, string Name, decimal Price);
