using CoffeeBeanery.GraphQL.Core.Foundation.Abstractions;
using CoffeeBeanery.GraphQL.Core.Foundation.ProviderPlan;
using ExecutionContext = CoffeeBeanery.GraphQL.Core.Foundation.Runtime.ExecutionContext;

namespace CoffeeBeanery.GraphQL.Core.Foundation.Provider;

public sealed class SqlExecutionProvider : IExecutionProvider
{
    public ProviderKind Kind => ProviderKind.Sql;

    public async IAsyncEnumerable<ExecutionRow> ExecuteAsync(
        ProviderPlan.ProviderPlan plan,
        ExecutionContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
