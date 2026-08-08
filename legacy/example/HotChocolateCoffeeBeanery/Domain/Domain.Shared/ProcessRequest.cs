using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Runtime.Filtering;

namespace Domain.Shared;

/// <summary>
/// Everything ProcessService.QueryProcessAsync needs, already adapted away
/// from HotChocolate's ISelection/AST types. Build this in the GraphQL
/// layer (see HotChocolateAdapter.AdaptQuery / FilterQueryExtension in
/// CoffeeBeanery.GraphQL) -- ProcessService itself has no HotChocolate
/// reference and never will, by construction (Domain.Shared can't
/// reference CoffeeBeanery.GraphQL without creating a circular project
/// reference, since CoffeeBeanery.GraphQL already references Domain.Shared).
/// </summary>
public sealed class QueryRequest
{
    public required SelectionIR SelectionIr { get; init; }

    /// <summary>
    /// Optional root-entity filter (the `where` argument), already
    /// compiled from GraphQL input into EntityFilterMetadata. Null means
    /// "no filter" -- SQL generation skips the WHERE clause entirely.
    /// </summary>
    public EntityFilterMetadata? Filter { get; init; }
}

/// <summary>
/// Everything ProcessService.MutationProcessAsync needs, already adapted
/// away from HotChocolate's ISelection/AST types -- see QueryRequest remarks.
/// </summary>
public sealed class MutationRequest
{
    public required SelectionIR SelectionIr { get; init; }

    /// <summary>
    /// Already-adapted mutation rows (see HotChocolateAdapter.AdaptMutationRequest).
    /// Empty when the mutation field carried no recognizable entity-input
    /// argument -- ProcessService treats that as "query-only", matching
    /// the previous ISelection-based behavior exactly.
    /// </summary>
    public required IReadOnlyList<MutationIR> Mutations { get; init; }
}

/// <summary>
/// Everything ProcessService.QueryProcessAsyncViaFoundationPaged needs.
/// Same SelectionIr/Filter shape as QueryRequest, plus the real
/// forward-pagination arguments (see PagingSqlWriter remarks: keyset,
/// forward-only -- `last`/`before` are accepted here for forward
/// compatibility but not yet acted on by ProcessService).
/// </summary>
public sealed class PagedQueryRequest
{
    public required SelectionIR SelectionIr { get; init; }

    public EntityFilterMetadata? Filter { get; init; }

    public int? First { get; init; }

    public string? After { get; init; }
}
