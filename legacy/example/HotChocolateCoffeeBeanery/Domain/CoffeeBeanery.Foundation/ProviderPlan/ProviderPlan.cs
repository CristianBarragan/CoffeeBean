namespace CoffeeBeanery.GraphQL.Core.Foundation.ProviderPlan;

/// <summary>Root of a physical, single-provider execution plan.</summary>
public sealed record ProviderPlan(
    ProviderNode Root
);
