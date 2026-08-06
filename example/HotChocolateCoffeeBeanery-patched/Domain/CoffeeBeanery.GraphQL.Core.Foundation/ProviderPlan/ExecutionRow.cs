namespace CoffeeBeanery.GraphQL.Core.Foundation.ProviderPlan;

/// <summary>
/// A single streamed row produced by an execution provider: raw column
/// values per entity, keyed by entity id.
/// </summary>
public sealed record ExecutionRow(
    IReadOnlyDictionary<ushort, object?[]> Entities
);
