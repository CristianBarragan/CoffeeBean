namespace CoffeeBeanery.GraphQL.Core.Foundation.Diagnostics;
public sealed record DiagnosticEvent(string Name, DateTimeOffset Timestamp, IReadOnlyDictionary<string,object?> Data);
