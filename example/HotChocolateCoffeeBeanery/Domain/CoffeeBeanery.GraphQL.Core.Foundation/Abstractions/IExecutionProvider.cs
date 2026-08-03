using CoffeeBeanery.GraphQL.Core.Foundation.Runtime;
using ExecutionContext = CoffeeBeanery.GraphQL.Core.Foundation.Runtime.ExecutionContext;

namespace CoffeeBeanery.GraphQL.Core.Foundation.Abstractions;

/// <summary>Executes planned graph operations.</summary>
public interface IExecutionProvider
{
    ValueTask<ExecutionResult> ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default);
}
