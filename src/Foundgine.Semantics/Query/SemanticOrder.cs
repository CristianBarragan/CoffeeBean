using Foundgine.Abstractions;

namespace Foundgine.Semantics.Query;

public sealed record SemanticOrderTerm(
    FieldId Field,
    SemanticSortDirection Direction,
    IReadOnlyList<RelationshipId>? Path = null,
    SemanticOrderAggregate Aggregate = SemanticOrderAggregate.None)
{
    public IReadOnlyList<RelationshipId> EffectivePath => Path ?? [];

    public bool IsRootField => EffectivePath.Count == 0;

    public bool IsAggregate => Aggregate != SemanticOrderAggregate.None;
}

public enum SemanticSortDirection : byte
{
    Asc,
    Desc
}

/// <summary>
/// Aggregate semantics for ordering a parent through a collection relationship.
/// Count uses the target entity cardinality; Min/Max operate on <see cref="SemanticOrderTerm.Field"/>.
/// </summary>
public enum SemanticOrderAggregate : byte
{
    None,
    Count,
    Min,
    Max
}
