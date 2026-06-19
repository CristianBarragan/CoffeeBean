namespace CoffeeBeanery.GraphQL.Core.Runtime;

public sealed class _ExecutionContext
{
    public _ExecutionPlan Plan { get; init; } = null!;
    public Dictionary<int, object> NodeState { get; } = new();
}