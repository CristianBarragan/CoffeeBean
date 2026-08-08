namespace CoffeeBeanery.GraphQL.Core.Foundation.Runtime;
public sealed record ExecutionResult(bool Success, object? Data, IReadOnlyList<string> Errors);
