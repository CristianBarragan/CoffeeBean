using Graphgine.HotChocolate;
using Graphgine.Execution;
using Graphgine.Execution.Filtering;
using Graphgine;
using Domain.Model;
using HotChocolate.Resolvers;
using HotChocolate.Types.Pagination;
using Graphgine.Sql;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Api.Banking.Query;

[ExtendObjectType("WrapperQuery")]
public class WrapperQueryResolver
{
    private readonly ILogger<WrapperQueryResolver> _logger;

    public WrapperQueryResolver(
        ILogger<WrapperQueryResolver> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Real pagination, through ProcessService.QueryProcessAsyncViaFoundationPaged
    /// -- previously, [UsePaging] only shaped the GraphQL schema (Connection/
    /// PageInfo types, first/last/after/before args); the actual fetch always
    /// pulled the ENTIRE result set via QueryProcessAsync, and the Pagination
    /// object handed to GenerateConnection never read the real first/last/
    /// before arguments at all (only After was set, and incorrectly, from
    /// the OUTGOING end cursor rather than the client's incoming `after`).
    /// first/last/before were silently ignored entirely. Fixed here to
    /// actually extract the real arguments and push them to SQL.
    ///
    /// KNOWN LIMITATION: only forward pagination (first/after) is
    /// implemented. `last`/`before` (backward pagination) are read from the
    /// request but not yet acted on -- see
    /// ProcessService.QueryProcessAsyncViaFoundationPaged, which only
    /// accepts first/after. A request using last/before will currently
    /// paginate forward from the start instead, which is wrong; fixing that
    /// needs a real backward-keyset query (ORDER BY DESC, then reverse),
    /// not just wiring the arguments through.
    /// </summary>
    [UsePaging]
    [UseFiltering]
    // [UseSorting]
    public async Task<Connection<Wrapper>> GetWrapper(
        [Service] IProcessService<Wrapper> service,
        [Service] AdapterLookup adapterLookup,
        [SchemaService] IResolverContext resolverContext,
        Wrapper wrapper)
    {
        try
        {
            var first =
                resolverContext.ArgumentValue<int?>("first");

            var last =
                resolverContext.ArgumentValue<int?>("last");

            var after =
                resolverContext.ArgumentValue<string?>("after");

            var before =
                resolverContext.ArgumentValue<string?>("before");

            if (last is not null || before is not null)
            {
                _logger.LogWarning(
                    "GetWrapper: 'last'/'before' (backward pagination) were " +
                    "requested but are not implemented yet -- falling back " +
                    "to forward pagination from the start. See remarks on " +
                    "this method.");
            }

            var modelName = wrapper.Model.ToString();

            var rootEntityId =
                service.ResolveRootEntityId(modelName);

            var selectionIr =
                HotChocolateAdapter.AdaptQuery(
                    rootEntityId,
                    modelName,
                    resolverContext.Selection,
                    adapterLookup);

            var filter =
                FilterQueryExtension.CompileWhere(
                    resolverContext.Selection,
                    rootEntityId,
                    new FilterMetadataResolver(
                        ImmutableArray.Create(
                            RuntimeEntityMetadataRegistry.GetRootOnly(rootEntityId))));

            var request = new PagedQueryRequest
            {
                SelectionIr = selectionIr,
                Filter = filter,
                First = first,
                After = after
            };

            var set =
                await service.QueryProcessAsyncViaFoundationPaged(
                    wrapper.CacheKey,
                    request,
                    modelName,
                    CancellationToken.None);

            var entityNodes = set.Models
                .Where(a => a is not null)
                .Select(a => new EntityNode<Wrapper>(a, nameof(Wrapper)))
                .ToList();

            var connection = ContextResolverHelper.GenerateConnection<Wrapper>(
                entityNodes,
                set.Cursors,
                new Pagination
                {
                    First = first,
                    Last = last,
                    After = after,
                    Before = before,
                    TotalRecordCount = new TotalRecordCount { RecordCount = set.TotalCount },
                    TotalPageRecords = new TotalPageRecords { PageRecords = set.TotalPageRecords },
                    StartCursor = set.StartCursor,
                    EndCursor = set.EndCursor,
                    HasNextPage = set.HasNextPage,
                    HasPreviousPage = set.HasPreviousPage,
                });

            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"Exception: {ex.Message} with inner exception {ex.InnerException}");
        }

        return default!;
    }
}
