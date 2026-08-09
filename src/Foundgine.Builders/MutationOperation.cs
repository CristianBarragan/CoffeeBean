using Foundgine.Metadata;

namespace Foundgine.Builders;

/// <summary>
/// The logical, provider-agnostic mutation tree — the mutation counterpart
/// of <see cref="QueryNode"/>. A mutation isn't a query: separate planner
/// (<see cref="Foundgine.Planning.MutationPlanner"/>), optimizer, and
/// executor from <see cref="QueryPlan"/>/<see cref="Foundgine.Execution.Contracts.ProviderPlan"/>.
///
/// This lives in <see cref="Foundgine.Builders"/>, not
/// <see cref="Foundgine.Planning"/>, for the same reason <see cref="QueryPlan"/>
/// does: <see cref="Foundgine.Providers.SqlPlanCompiler"/> needs to compile it
/// directly, and <c>Foundgine.Providers</c> and <c>Foundgine.Planning</c> are
/// architectural peers (see <c>ArchitectureTests</c>'s <c>AllowedReferences</c>:
/// <c>Foundgine.Providers</c> may reference <c>[Execution.Contracts, Builders]</c>,
/// never <c>Foundgine.Planning</c>) — neither takes a <c>ProjectReference</c> on
/// the other. Keeping the plan *shape* here, with only the intent-to-plan
/// translation (<see cref="Foundgine.Planning.MutationIntent"/>/
/// <see cref="Foundgine.Planning.MutationPlanner"/>) in
/// <see cref="Foundgine.Planning"/>, is what makes that possible without a
/// new <c>ProjectReference</c> in either direction.
/// </summary>
public abstract record MutationOperation;

/// <summary>
/// One entity-level mutation: insert/update/delete/upsert
/// <see cref="Columns"/> on <see cref="Entity"/>.
///
/// <see cref="Filter"/> identifies the target row(s) for
/// <see cref="MutationKind.Update"/> and <see cref="MutationKind.Delete"/> —
/// the mutation counterpart of <see cref="FilterNode"/>/
/// <see cref="Foundgine.Metadata.FilterExpression"/> on the read side. It is
/// unused for <see cref="MutationKind.Create"/>, which always inserts a new
/// row from <see cref="Columns"/> rather than targeting existing ones.
/// </summary>
public sealed record EntityMutation(
    EntityMetadata Entity,
    MutationKind Kind,
    IReadOnlyList<MutationColumn> Columns,
    FilterExpression? Filter = null
) : MutationOperation;

public sealed record GraphMutation(
    GraphMetadata Graph,
    EntityMutation From,
    EntityMutation To
) : MutationOperation;

public sealed record RelationshipMutation(
    EntityMetadata Parent,
    EntityMetadata Child,
    JoinCondition Condition
) : MutationOperation;
