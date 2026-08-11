using Foundgine.Abstractions;
using Foundgine.Semantics.Query;

namespace Foundgine.Semantics.Intent;

/// <summary>
/// External, provider-neutral read intent. This is deliberately simpler than
/// GraphQL or an ORM expression tree so an API, agent, or other producer can
/// create it without knowing physical storage.
/// </summary>
public sealed record ReadIntent(
    string RootEntity,
    IReadOnlyList<ReadSelection> Selections,
    ReadFilter? Filter = null,
    IReadOnlyList<ReadOrder>? Order = null,
    int? Limit = null,
    int? Offset = null,
    string? After = null);

public sealed record ReadSelection(
    string? Field = null,
    string? Relationship = null,
    IReadOnlyList<ReadSelection>? Children = null)
{
    public IReadOnlyList<ReadSelection> EffectiveChildren => Children ?? [];
}

public abstract record ReadFilter;

public sealed record ReadFieldFilter(
    string Field,
    SemanticFilterOperator Operator,
    object? Value) : ReadFilter;

public sealed record ReadRelationshipFilter(
    string Relationship,
    SemanticRelationshipQuantifier Quantifier,
    ReadFilter Predicate) : ReadFilter;

public sealed record ReadAndFilter(IReadOnlyList<ReadFilter> Expressions) : ReadFilter;

public sealed record ReadOrFilter(IReadOnlyList<ReadFilter> Expressions) : ReadFilter;

public sealed record ReadOrder(
    string Field,
    SemanticSortDirection Direction,
    IReadOnlyList<string>? RelationshipPath = null,
    SemanticOrderAggregate Aggregate = SemanticOrderAggregate.None)
{
    public IReadOnlyList<string> EffectivePath => RelationshipPath ?? [];
}
