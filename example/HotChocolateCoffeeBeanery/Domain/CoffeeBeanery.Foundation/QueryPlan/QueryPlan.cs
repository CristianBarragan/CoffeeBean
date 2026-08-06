namespace CoffeeBeanery.GraphQL.Core.Foundation.QueryPlan;

/// <summary>Root of a logical, provider-agnostic query plan.</summary>
public sealed record QueryPlan(
    QueryNode Root
);
