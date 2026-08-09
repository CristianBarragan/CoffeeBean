using Foundgine.Metadata;

namespace Foundgine.Builders;

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

/// <summary>
/// A scan of <see cref="Entity"/> plus its child branches, each reached via
/// a resolved <see cref="JoinMetadata"/> — the logical-layer counterpart of
/// <see cref="Foundgine.Planning.QueryIntentBranch"/>.
///
/// This is what keeps <see cref="QueryPlan"/> honest about
/// <see cref="Foundgine.Planning.QueryIntent"/>'s requested shape:
///
/// <code>
/// Customer
/// ├── Accounts
/// │    └── Transactions
/// └── ContactPoints
/// </code>
///
/// stays a tree all the way through planning — <see cref="Foundgine.Planning.QueryPlanner"/>
/// no longer collapses it into <c>(((Customer JOIN Account) JOIN
/// Transaction) JOIN ContactPoint)</c> itself. That flattening is a
/// SQL-specific compilation decision now, made by
/// <see cref="Foundgine.Providers.SqlPlanCompiler"/> when it turns a
/// <see cref="CompositeNode"/> into a provider-level join chain — a
/// different provider (graph, cache, a smarter SQL compiler that reorders
/// joins) is free to make a different decision from the same
/// <see cref="CompositeNode"/>, because the semantic shape wasn't thrown
/// away to get here. (TECH-DEBT-001.)
/// </summary>
public sealed record CompositeNode(
    EntityMetadata Entity,
    IReadOnlyList<CompositeEdge> Children
) : QueryNode;

/// <summary>
/// One edge from a <see cref="CompositeNode"/> to a child
/// <see cref="CompositeNode"/>, carrying the <see cref="JoinMetadata"/>
/// <see cref="Foundgine.Metadata.JoinGraph"/> resolved between the parent
/// and child entity.
/// </summary>
public sealed record CompositeEdge(
    JoinMetadata Join,
    CompositeNode Child
);

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