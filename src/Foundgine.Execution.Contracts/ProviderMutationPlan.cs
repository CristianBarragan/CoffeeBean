using Foundgine.Metadata;

namespace Foundgine.Execution.Contracts;

/// <summary>
/// Physical, provider-specific mutation node — the mutation counterpart of
/// <see cref="ProviderNode"/>. A provider planner (e.g.
/// <see cref="Foundgine.Providers.SqlPlanCompiler"/>) turns each
/// <see cref="Foundgine.Builders.MutationOperation"/> into one of these,
/// choosing a concrete strategy for a specific backend.
/// </summary>
public abstract record ProviderMutationNode;

/// <summary>Physical counterpart of an <c>INSERT</c>: writes one new row.</summary>
public sealed record SqlInsertNode(
    EntityMetadata Entity,
    IReadOnlyList<MutationColumn> Columns
) : ProviderMutationNode;

/// <summary>
/// Physical counterpart of an <c>UPDATE</c>: writes <see cref="Columns"/> on
/// every row matching <see cref="Filter"/>. Unlike <see cref="SqlInsertNode"/>,
/// <see cref="Filter"/> is required — an unconditional update is rejected
/// upstream by <see cref="Foundgine.Planning.MutationPlanner"/> and again
/// here by <see cref="Foundgine.Providers.SqlPlanCompiler"/>, since Foundgine
/// never mutates every row by accident.
/// </summary>
public sealed record SqlUpdateNode(
    EntityMetadata Entity,
    IReadOnlyList<MutationColumn> Columns,
    FilterExpression Filter
) : ProviderMutationNode;

/// <summary>Physical counterpart of a <c>DELETE</c>: removes every row matching <see cref="Filter"/>.</summary>
public sealed record SqlDeleteNode(
    EntityMetadata Entity,
    FilterExpression Filter
) : ProviderMutationNode;

/// <summary>
/// Root of a physical, single-provider mutation execution plan: every
/// operation in <see cref="Operations"/> executes as one atomic unit against
/// the provider (see <see cref="Foundgine.Providers.SqlExecutionProvider"/>,
/// which wraps them in a single transaction) — this is what lets a caller
/// express "create a Customer, an Account, and a Transaction together" as
/// one plan instead of three independently-committed calls.
/// </summary>
public sealed record ProviderMutationPlan(
    IReadOnlyList<ProviderMutationNode> Operations
);
