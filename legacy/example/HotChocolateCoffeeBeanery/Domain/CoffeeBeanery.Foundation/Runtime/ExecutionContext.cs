namespace CoffeeBeanery.GraphQL.Core.Foundation.Runtime;
public sealed record ExecutionContext(Guid ExecutionId, IReadOnlyDictionary<string, object?> Variables);
