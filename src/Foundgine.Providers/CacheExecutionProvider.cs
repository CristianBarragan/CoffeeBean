using Foundgine.Execution.Contracts;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

namespace Foundgine.Providers;

/// <summary>
/// Cache execution provider. Not yet implemented — see <see cref="SqlExecutionProvider"/>
/// for why this deliberately isn't an `async IAsyncEnumerable&lt;T&gt;` iterator that
/// throws before its first (unreachable) yield.
/// </summary>
public sealed class CacheExecutionProvider : IExecutionProvider
{
    public ProviderKind Kind => ProviderKind.Cache;

    public IAsyncEnumerable<ExecutionRow> ExecuteAsync(
        ProviderPlan plan,
        ExecutionContext context,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            $"{nameof(CacheExecutionProvider)} does not yet serve a {nameof(ProviderPlan)} " +
            "from cache. This provider is architecturally wired up but not implemented yet.");
}
