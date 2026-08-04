// using CoffeeBeanery.GraphQL.Core.Foundation.ExecutionPlan;
//
// namespace CoffeeBeanery.GraphQL.Core.Foundation.Provider;
//
// public sealed record ProviderBoundaryNode(
//     ExecutionPlanNode Source,
//     ProviderKind Provider
// ) : ExecutionPlanNode;
//
// public enum ProviderKind : byte
// {
//     Sql,
//     Graph,
//     Cache
// }
//
// public interface IExecutionProvider
// {
//     ProviderKind Kind { get; }
//
//     ValueTask<ExecutionResult> ExecuteAsync(
//         ExecutionPlanNode plan,
//         CancellationToken cancellationToken);
// }
//
// public sealed record ExecutionResult(
//     IReadOnlyList<ResultRow> Rows
// );
//
// public sealed record ResultRow(
//     IReadOnlyDictionary<string, IExecutionResult?> Values
// );
//
// public interface IExecutionResult
// {
//     int Count { get; }
//
//     object? GetValue(
//         int row,
//         int field);
// }
//
