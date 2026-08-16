using Foundgine.Abstractions;

namespace Foundgine.Planning;

/// <summary>
/// Canonical semantic planning artifact produced from an authorized Semantic IR.
/// It describes the provider-neutral execution strategy without representing
/// physical provider work.
/// </summary>
public sealed record SemanticPlan(
    SemanticPlanNode Root,
    IReadOnlyList<string>? RequiredSecurityInvariants = null)
{
    public IReadOnlyList<string> EffectiveSecurityInvariants =>
        RequiredSecurityInvariants ?? [];
}

/// <summary>
/// One node in the canonical semantic planning artifact.
/// </summary>
public sealed record SemanticPlanNode(
    int Id,
    ExecutionOperation Operation,
    EntityId EntityId,
    IReadOnlyList<FieldId> Fields,
    RelationshipId? ViaRelationship,
    ConnectionId? ViaConnection,
    IReadOnlyList<SemanticPlanNode> Children,
    Foundgine.Semantics.Query.SemanticQueryOptions? QueryOptions = null,
    AuthorizationPredicate? Authorization = null,
    Foundgine.Semantics.RelationshipCardinality? RelationshipCardinality = null,
    RelationshipTraversalMode TraversalMode = RelationshipTraversalMode.Default,
    int TraversalOrder = -1,
    AggregateExecutionStrategy AggregateExecutionStrategy = AggregateExecutionStrategy.Default)
{
    public SemanticPlanNode(
        int id,
        ExecutionOperation operation,
        EntityId entityId,
        IReadOnlyList<FieldId> fields,
        RelationshipId? viaRelationship,
        IReadOnlyList<SemanticPlanNode> children)
        : this(id, operation, entityId, fields, viaRelationship, null, children, null)
    {
    }
}
