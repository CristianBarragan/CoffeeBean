using CoffeeBeanery.GraphQL.Core.Foundation.Metadata;

namespace CoffeeBeanery.GraphQL.Core.Foundation.QueryPlan;

/// <summary>
/// The logical, provider-agnostic query tree. Describes WHAT data is
/// needed, never HOW a specific backend (SQL, AGE, cache, ...) fetches it.
/// </summary>
public abstract record QueryNode;

public sealed record ScanNode(
    EntityMetadata Entity
) : QueryNode;

public sealed record JoinNode(
    QueryNode Left,
    QueryNode Right,
    JoinMetadata Join
) : QueryNode;

public sealed record GraphEdgeNode(
    QueryNode Source,
    GraphMetadata Graph,
    QueryNode? From,
    QueryNode? To
) : QueryNode;

public sealed record ProjectionNode(
    QueryNode Source,
    IReadOnlyList<FieldBinding> Fields
) : QueryNode;

public sealed record MaterializeNode(
    QueryNode Source,
    ModelMetadata Model
) : QueryNode;