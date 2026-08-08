using CoffeeBeanery.GraphQL.Core.Helper;
using CoffeeBeanery.Service;
using Domain.Model;
using HotChocolate.Resolvers;
using HotChocolate.Types.Pagination;
using CoffeeBeanery.GraphQL.Core.Sql;

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

    [UsePaging]
    [UseFiltering]
    // [UseSorting]
    public async Task<Connection<Wrapper>> GetWrapper(
        [Service] IProcessService<Wrapper> service,
        [SchemaService] IResolverContext resolverContext,
        Wrapper wrapper)
    {
        try
        {
            var cacheKey = string.Empty;
            
            var set = await service.QueryProcessAsync(
                wrapper.CacheKey, resolverContext.Selection,
                wrapper.Model.ToString(), CancellationToken.None);

            var entityNodes = set.Models
                .Where(a => a is not null)
                .Select(a => new EntityNode<Wrapper>(a, nameof(Wrapper)));

            var connection = ContextResolverHelper.GenerateConnection<Wrapper>(
                entityNodes,
                new Pagination
                {
                    TotalRecordCount = new TotalRecordCount { RecordCount = set.TotalCount },
                    TotalPageRecords = new TotalPageRecords { PageRecords = set.TotalPageRecords },
                    StartCursor = set.StartCursor,
                    After = set.EndCursor?.ToString(),
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
