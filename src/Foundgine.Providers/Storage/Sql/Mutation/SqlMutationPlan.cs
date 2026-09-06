using Foundgine.Core.Abstractions;
using Foundgine.Core.Execution.Mutation;
using Foundgine.Core.Semantic.Planning.Mutation;
using Foundgine.Providers.Storage.Sql.Query;

namespace Foundgine.Providers.Storage.Sql.Mutation;

public sealed record SqlMutationPlan(
    string CommandText,
    IReadOnlyList<SqlParameterBinding> Parameters,
    IReadOnlyList<MutationReturnBinding> ReturnedFields,
    string? FallbackCommandText = null) : ProviderMutationPlan;

public sealed record MutationReturnBinding(
    FieldId FieldId,
    string ResultName);

public sealed record SqlMutationBatchPlan(
    IReadOnlyList<SqlMutationPlan> Operations,
    IReadOnlyList<MutationDependency> Dependencies)
    : ProviderMutationBatchPlan(Operations)
{
    public new IReadOnlyList<SqlMutationPlan> Operations { get; init; } = Operations;
}