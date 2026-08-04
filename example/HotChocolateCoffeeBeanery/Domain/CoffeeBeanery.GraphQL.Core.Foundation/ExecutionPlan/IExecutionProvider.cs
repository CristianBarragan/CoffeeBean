using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoffeeBeanery.GraphQL.Core.Foundation.ExecutionPlan;

namespace CoffeeBeanery.GraphQL.Core.Foundation.ExecutionPlan;

public interface IExecutionProvider
{
    IAsyncEnumerable<ExecutionRow> ExecuteAsync(
        ExecutionPlanNode node,
        ExecutionContext context,
        CancellationToken cancellationToken = default);
}

public sealed record ExecutionRow(
    IReadOnlyDictionary<ushort, object?[]> Entities
);




