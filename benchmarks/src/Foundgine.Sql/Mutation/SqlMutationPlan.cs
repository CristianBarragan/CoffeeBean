using Foundgine.Execution.Mutation;
using Foundgine.Metadata;
using Foundgine.Abstractions;
using Foundgine.Planning.Mutation;
using Foundgine.Sql.Query;

namespace Foundgine.Sql.Mutation;

public sealed record SqlMutationPlan(
    string CommandText,
    IReadOnlyList<SqlParameterBinding> Parameters,
    IReadOnlyList<MutationReturnBinding> ReturnedFields) : ProviderMutationPlan;

public sealed record MutationReturnBinding(
    FieldId FieldId,
    string ResultName);


public sealed record SqlMutationBatchPlan(
    IReadOnlyList<SqlMutationPlan> Operations,
    IReadOnlyList<Foundgine.Planning.Mutation.MutationDependency> Dependencies)
    : Foundgine.Execution.Mutation.ProviderMutationBatchPlan(Operations)
{
    public new IReadOnlyList<SqlMutationPlan> Operations { get; init; } = Operations;
}
