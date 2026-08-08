using Graphgine.HotChocolate;
using Graphgine.Execution;
using Graphgine.Sql;
using Graphgine;
using Domain.Model;
using HotChocolate.Data;
using HotChocolate.Resolvers;
using HotChocolate.Types.Pagination;

namespace Api.Banking.Mutation;

[ExtendObjectType("WrapperMutation")]
public class WrapperMutationResolver : IInputType, IOutputType
{
    private readonly ILogger<WrapperMutationResolver> _logger;

    public WrapperMutationResolver(ILogger<WrapperMutationResolver> logger)
    {
        _logger = logger;
    }

    [UsePaging]
    [UseFiltering]
    // [UseSorting]
    public async Task<Connection<Wrapper>> UpsertWrapper(
        [Service] IProcessService<Wrapper> service,
        [Service] AdapterLookup adapterLookup,
        [SchemaService] IResolverContext resolverContext,
        Wrapper wrapper)
    {
        try
        {
            var modelName = wrapper.Model.ToString();

            var rootEntityId =
                service.ResolveRootEntityId(modelName);

            var selectionIr =
                HotChocolateAdapter.AdaptQuery(
                    rootEntityId,
                    modelName,
                    resolverContext.Selection,
                    adapterLookup);

            var mutations =
                HotChocolateAdapter.AdaptMutationRequest(
                    resolverContext.Selection,
                    rootEntityId,
                    modelName,
                    adapterLookup);

            var request = new MutationRequest
            {
                SelectionIr = selectionIr,
                Mutations = mutations
            };

            var set = await service.MutationProcessAsync(
                wrapper.CacheKey, request,
                modelName, CancellationToken.None);

            var entityNodes = set.Models
                .Where(a => a is not null)
                .Select(a => new EntityNode<Wrapper>(a, nameof(Wrapper)));

            var connection = ContextResolverHelper.GenerateConnection<Wrapper>(
                entityNodes.ToList(),
                set.Cursors,
                new Pagination
                {
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
                ex,                                    
                "UpsertWrapper failed: {Message}", ex.Message);
        }

        return default!;
    }

    public TypeKind Kind { get; }
    public Type RuntimeType { get; }
}