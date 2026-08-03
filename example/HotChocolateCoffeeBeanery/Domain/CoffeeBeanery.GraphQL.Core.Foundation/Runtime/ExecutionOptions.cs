namespace CoffeeBeanery.GraphQL.Core.Foundation.Runtime;
public sealed record ExecutionOptions(bool EnableDiagnostics = false, int MaxDepth = 64);
