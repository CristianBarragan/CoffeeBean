using Foundgine.Execution.Contracts;

namespace Foundgine.Core.Provider;

public sealed class SqlExecutionProvider : IExecutionProvider
{
    public ProviderKind Kind => ProviderKind.Sql;

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
