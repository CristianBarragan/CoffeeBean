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

public sealed record SqlJoinNode(
    ProviderNode Left,
    ProviderNode Right,
    JoinMetadata Join
) : ProviderNode;

public sealed record SqlProjectionNode(
    ProviderNode Source,
    IReadOnlyList<FieldBinding> Fields
) : ProviderNode;

/// <summary>Apache AGE (or other graph backend) traversal of a graph edge.</summary>
public sealed record GraphTraversalNode(
    GraphMetadata Graph,
    ProviderNode From,
    ProviderNode To
) : ProviderNode;

/// <summary>Cache-backed lookup by key, used when a provider planner decides a subtree is servable from cache.</summary>
public sealed record CacheLookupNode(
    EntityMetadata Entity,
    IReadOnlyList<ColumnReference> KeyColumns
) : ProviderNode;
