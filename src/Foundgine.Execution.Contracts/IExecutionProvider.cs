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

    /// <summary>
    /// Executes a physical mutation plan as a single atomic unit, returning
    /// one <see cref="MutationResult"/> per operation in
    /// <see cref="ProviderMutationPlan.Operations"/> order. Not streamed like
    /// <see cref="ExecuteAsync"/> — a mutation's outcome (rows affected) is
    /// known only once every write in the plan has either all committed or
    /// all rolled back.
    /// </summary>
    Task<IReadOnlyList<MutationResult>> ExecuteMutationAsync(
        ProviderMutationPlan plan,
        ExecutionContext context,
        CancellationToken cancellationToken = default);
}
