using Foundgine.Execution.Contracts;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

namespace Foundgine.Providers;

/// <summary>
/// SQL execution provider. Not yet implemented — the provider plan/context
/// contracts and dependency wiring are in place, but this provider does not
/// yet translate a <see cref="ProviderPlan"/> into SQL and execute it against
/// a database. Tracked as part of the "implement the SQL provider" milestone.
/// </summary>
public sealed class SqlExecutionProvider : IExecutionProvider
{
    public ProviderKind Kind => ProviderKind.Sql;

    // Intentionally not an `async IAsyncEnumerable<T>` iterator: an iterator
    // method can't throw until it's first enumerated, which forces a dead
    // `yield break` after the throw just to satisfy the compiler. Returning
    // the exception from a plain method surfaces the "not implemented" state
    // immediately and honestly, with no unreachable code.
    public IAsyncEnumerable<ExecutionRow> ExecuteAsync(
        ProviderPlan plan,
        ExecutionContext context,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            $"{nameof(SqlExecutionProvider)} does not yet translate a {nameof(ProviderPlan)} " +
            "into SQL and execute it. This provider is architecturally wired up but not " +
            "implemented yet.");
}
