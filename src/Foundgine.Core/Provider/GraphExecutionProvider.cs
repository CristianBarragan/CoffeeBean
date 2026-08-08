using Foundgine.Execution.Contracts;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

namespace Foundgine.Core.Provider;

public sealed class GraphExecutionProvider : IExecutionProvider
{
    public ProviderKind Kind => ProviderKind.Graph;

    public async IAsyncEnumerable<ExecutionRow> ExecuteAsync(
        ProviderPlan plan,
        ExecutionContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
