using CoffeeBeanery.CQRS;
using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.GraphQL.Helper;
using FASTER.core;
using HotChocolate.Execution.Processing;
using MoreLinq.Extensions;

namespace CoffeeBeanery.Service
{
    public interface IProcessService<M> where M : class
    {
        Task<QueryResult<M>> QueryProcessAsync(
            string cacheKey, ISelection selection,
            string modelName,
            CancellationToken cancellationToken);

        Task<QueryResult<M>> MutationProcessAsync(
            string cacheKey, ISelection selection,
            string modelName,
            CancellationToken cancellationToken);
    }

    public class ProcessService<M> : IProcessService<M>
        where M : class
    {
        private readonly IQueryDispatcher _queryDispatcher;
        private IFasterKV<string, string> _cache;

        public ProcessService(
            IQueryDispatcher queryDispatcher,
            IFasterKV<string, string> cache)
        {
            _queryDispatcher = queryDispatcher;
            _cache = cache;
        }

        public async Task<QueryResult<M>> QueryProcessAsync(
            string cacheKey, ISelection selection, string modelName, CancellationToken cancellationToken)
        {
            var rootTree = SqlNodeRegistry.ModelTrees.First(a => 
                a.Value.ModelName.Matches(modelName)).Value;
            var context = new SqlCompilationContext();
            
            SqlQueryCompiler.Compile(
                context,
                selection,
                rootTree,
                _cache,
                cacheKey
            );
            
            context.ModelTrees = SqlNodeRegistry.ModelTrees;
            context.EntityTrees = SqlNodeRegistry.EntityTrees;
            context.RelativeTree = rootTree;
            context.SqlNodesApplied = SqlNodeRegistry.EntityNodes;

            var parameters = new ProcessQueryParameters
            {
                Context = context,
                Model = modelName
            };

            var (models, startCursor, endCursor, totalCount, totalPageRecords) =
                await _queryDispatcher
                    .DispatchAsync<ProcessQueryParameters, (List<M>, int?, int?, int?, int?)>(
                        parameters, cancellationToken);

            return new QueryResult<M> 
            {
                Models = models,
                StartCursor = startCursor,
                EndCursor = endCursor,
                TotalCount = totalCount ?? 0,
                TotalPageRecords = totalPageRecords ?? 0
            };
        }

        public async Task<QueryResult<M>> MutationProcessAsync(
            string cacheKey, ISelection selection, string modelName, CancellationToken cancellationToken)
        {
            var rootTree = SqlNodeRegistry.ModelTrees.First(a => 
                a.Value.ModelName.Matches(modelName)).Value;
            
            var sqlWhereStatement = new Dictionary<string, string>();
            var context = new SqlCompilationContext();

            SqlMutationCompiler.Compile(context, selection, rootTree, sqlWhereStatement, SqlNodeRegistry.ModelTrees, SqlNodeRegistry.EntityTrees, SqlNodeRegistry.ModelNodes, 
                SqlNodeRegistry.EntityNodes, SqlNodeRegistry.ModelNames, SqlNodeRegistry.EntityNames);

            SqlQueryCompiler.Compile(
                context,
                selection,
                rootTree,
                _cache,
                cacheKey
            );
            
            context.ModelTrees = SqlNodeRegistry.ModelTrees;
            context.EntityTrees = SqlNodeRegistry.EntityTrees;
            context.RelativeTree = rootTree;
            context.SqlNodesApplied = SqlNodeRegistry.EntityNodes; 

            var parameters = new ProcessQueryParameters
            {
                Context = context,
                Model = modelName
            };

            var (models, startCursor, endCursor, totalCount, totalPageRecords) =
                await _queryDispatcher
                    .DispatchAsync<ProcessQueryParameters, (List<M>, int?, int?, int?, int?)>(
                        parameters, cancellationToken);

            return new QueryResult<M> 
            {
                Models = models,
                StartCursor = startCursor,
                EndCursor = endCursor,
                TotalCount = totalCount ?? 0,
                TotalPageRecords = totalPageRecords ?? 0
            };
        }

    }
}
