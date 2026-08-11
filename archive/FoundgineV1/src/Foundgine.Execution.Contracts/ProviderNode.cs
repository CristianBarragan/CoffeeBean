using Foundgine.Metadata;

namespace Foundgine.Execution.Contracts;

/// <summary>
/// Physical, provider-specific plan tree. A provider planner (e.g.
/// SqlProviderPlanner, AgeProviderPlanner) turns a QueryNode into one of
/// these, choosing concrete strategies for a specific backend.
/// </summary>
public abstract record ProviderNode;

public sealed record SqlScanNode(
    EntityMetadata Entity
) : ProviderNode;

/// <summary>
/// A SQL join between two subtrees.
///
/// <see cref="LeftOccurrence"/>/<see cref="RightOccurrence"/> are optional
/// pointers to the exact <see cref="SqlScanNode"/> instance each side of
/// this join binds to — occurrence identity, not just entity type. A
/// compiler that scans the same <see cref="EntityMetadata"/> more than
/// once in one plan (e.g. <c>Employee -> Manager -> Manager</c>) should
/// populate these so <see cref="Foundgine.Providers.SqlTextTranslator"/>
/// can resolve each join's aliases against the correct occurrence instead
/// of falling back to "the alias last registered for this entity type",
/// which silently collapses repeated entities onto one alias.
///
/// Left as optional (rather than required) so a hand-built plan that never
/// repeats an entity — the common case, and every existing call site as of
/// this change — doesn't need to thread them through; <see cref="Foundgine.Providers.SqlTextTranslator"/>
/// falls back to entity-type lookup when they're absent.
/// </summary>
public sealed record SqlJoinNode(
    ProviderNode Left,
    ProviderNode Right,
    JoinMetadata Join,
    SqlScanNode? LeftOccurrence = null,
    SqlScanNode? RightOccurrence = null
) : ProviderNode;

public sealed record SqlProjectionNode(
    ProviderNode Source,
    IReadOnlyList<FieldBinding> Fields
) : ProviderNode;

/// <summary>Apache AGE (or other graph backend) traversal of a graph edge.</summary>
public sealed record GraphTraversalNode(
    ProviderNode Source,
    GraphMetadata Graph,
    ProviderNode? From,
    ProviderNode? To
) : ProviderNode;

/// <summary>Cache-backed lookup by key, used when a provider planner decides a subtree is servable from cache.</summary>
public sealed record CacheLookupNode(
    EntityMetadata Entity,
    IReadOnlyList<ColumnReference> KeyColumns
) : ProviderNode;

/// <summary>
/// Physical counterpart of <see cref="Foundgine.Builders.FilterNode"/>:
/// carries the same provider-agnostic <see cref="FilterExpression"/>
/// through to <see cref="Foundgine.Providers.SqlTextTranslator"/>, which is
/// the only thing that turns it into a WHERE clause plus parameters.
/// </summary>
public sealed record SqlFilterNode(
    ProviderNode Source,
    FilterExpression Filter
) : ProviderNode;

/// <summary>Physical counterpart of <see cref="Foundgine.Builders.SortNode"/>.</summary>
public sealed record SqlSortNode(
    ProviderNode Source,
    IReadOnlyList<SortTerm> Terms
) : ProviderNode;

/// <summary>Physical counterpart of <see cref="Foundgine.Builders.PageNode"/>.</summary>
public sealed record SqlPageNode(
    ProviderNode Source,
    PageSpec Page
) : ProviderNode;