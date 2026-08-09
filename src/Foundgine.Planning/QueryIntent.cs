using Foundgine.Builders;
using Foundgine.Metadata;

namespace Foundgine.Planning;

/// <summary>
/// What a caller wants resolved, expressed purely in terms of
/// <see cref="Foundgine.Metadata"/> identities — never in terms of tables,
/// SQL, or any other physical concept. <see cref="QueryPlanner"/> is the
/// only thing that turns this into a <see cref="QueryPlan"/>.
///
/// <see cref="Path"/> is deliberately a flat, ordered chain rather than a
/// tree: it says "start at <see cref="Root"/>, then reach <c>Path[0]</c>,
/// then <c>Path[1]</c>, ..." following whatever edge <see cref="JoinGraph"/>
/// has registered between each consecutive pair. This is enough to express
/// the Customer -> Account -> Transaction case the first E2E targets. A
/// branching intent (e.g. Customer -> Accounts AND Customer -> Addresses)
/// is future work and not required to prove the thesis.
/// </summary>
public sealed record QueryIntent(
    EntityId Root,
    IReadOnlyList<EntityId> Path,
    IReadOnlyList<FieldBinding>? Fields = null
);
