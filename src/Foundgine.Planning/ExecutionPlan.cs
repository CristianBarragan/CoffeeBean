using Foundgine.Abstractions;

namespace Foundgine.Planning;

/// <summary>
/// Provider-independent execution plan produced from an authorized semantic
/// graph. The plan describes execution operations and preserves request
/// topology without introducing SQL, storage names, aliases, or provider
/// concepts.
/// </summary>
public sealed record ExecutionPlan(ExecutionPlanNode Root);

/// <summary>
/// One provider-independent execution operation.
/// </summary>
public sealed record ExecutionPlanNode(
    int Id,
    ExecutionOperation Operation,
    EntityId EntityId,
    IReadOnlyList<FieldId> Fields,
    RelationshipId? ViaRelationship,
    IReadOnlyList<ExecutionPlanNode> Children,
    Foundgine.Semantics.Query.SemanticQueryOptions? QueryOptions = null);

/// <summary>
/// Minimal logical operations understood by the provider-independent planner.
/// Providers decide later how these operations are physically executed.
/// </summary>
public enum ExecutionOperation : byte
{
    Scan,
    Traverse
}
