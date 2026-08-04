using CoffeeBeanery.GraphQL.Core.Foundation.ExecutionPlan;

namespace CoffeeBeanery.GraphQL.Core.Foundation.ProviderPlan;

public sealed record ProviderBoundaryNode(
    IExecutionProvider Provider,
    ExecutionPlanNode Source
) : ExecutionPlanNode;
