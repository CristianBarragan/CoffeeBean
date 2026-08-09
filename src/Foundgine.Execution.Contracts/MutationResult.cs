using Foundgine.Metadata;

namespace Foundgine.Execution.Contracts;

/// <summary>
/// The outcome of one operation within a <see cref="ProviderMutationPlan"/> —
/// one <see cref="MutationResult"/> per <see cref="ProviderMutationPlan.Operations"/>
/// entry, in the same order, returned once every operation in the plan has
/// committed as a single atomic unit (see
/// <see cref="Foundgine.Providers.SqlExecutionProvider.ExecuteMutationAsync"/>).
/// </summary>
public sealed record MutationResult(
    EntityId EntityId,
    int RowsAffected
);
