using Foundgine.Core.Execution.Mutation;

namespace Foundgine.Providers.Storage.Sql.Mutation.Postgres;

/// <summary>
///     Provider-facing boundary for PostgreSQL mutation batching.
///     Physical grouping consumes already-derived execution levels.
/// </summary>
public sealed record PostgresMutationBatchBoundary(
    MutationExecutionLevels DependencyLevels)
{
    public static PostgresMutationBatchBoundary From(ExecutionMutationIR ir)
    {
        return new PostgresMutationBatchBoundary(MutationExecutionLevels.From(ir));
    }
}