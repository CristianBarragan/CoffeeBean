namespace Foundgine.Execution.Contracts;

public enum ProviderKind : byte
{
    Sql,
    Graph,
    Cache
}

/// <summary>
/// Executes a single-provider physical plan. The provider only ever sees
/// its own ProviderPlan (e.g. SQL nodes for a SQL provider) — it has no
/// awareness of the logical QueryPlan or of other providers.
/// </summary>
public interface IExecutionProvider
{
    ProviderKind Kind { get; }

    IAsyncEnumerable<ExecutionRow> ExecuteAsync(
        ProviderPlan plan,
        ExecutionContext context,
        CancellationToken cancellationToken = default);
}
