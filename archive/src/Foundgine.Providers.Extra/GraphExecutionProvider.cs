using Foundgine.Execution.Contracts;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

namespace Foundgine.Providers;

/// <summary>
/// Graph execution provider. Not yet implemented — see <see cref="SqlExecutionProvider"/>
/// for why this deliberately isn't an `async IAsyncEnumerable&lt;T&gt;` iterator that
/// throws before its first (unreachable) yield.
/// </summary>
public sealed class GraphExecutionProvider : IExecutionProvider
{
    public ProviderKind Kind => ProviderKind.Graph;

    public IAsyncEnumerable<ExecutionRow> ExecuteAsync(
        ProviderPlan plan,
        ExecutionContext context,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            $"{nameof(GraphExecutionProvider)} does not yet translate a {nameof(ProviderPlan)} " +
            "into a graph traversal and execute it. This provider is architecturally wired up " +
            "but not implemented yet.");
}
