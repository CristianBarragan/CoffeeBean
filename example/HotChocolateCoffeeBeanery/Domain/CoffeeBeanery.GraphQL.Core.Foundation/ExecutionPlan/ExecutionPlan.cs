
namespace CoffeeBeanery.GraphQL.Core.Foundation.ExecutionPlan;

public abstract record ExecutionPlan
{
    public required ExecutionPlanNode Root { get; init; }
}
