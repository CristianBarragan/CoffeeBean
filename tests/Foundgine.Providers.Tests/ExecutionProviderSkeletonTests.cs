using Foundgine.Providers;
using Foundgine.Execution.Contracts;
using Foundgine.Metadata;
using Xunit;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

namespace Foundgine.Providers.Tests;

public class ExecutionProviderSkeletonTests
{
    private static ProviderPlan EmptyPlan() =>
        new(new SqlScanNode(new EntityMetadata(new EntityId(1), "X", Array.Empty<ColumnMetadata>())));

    private static ExecutionContext EmptyContext() =>
        new(Guid.NewGuid(), new Dictionary<string, object?>());

    [Theory]
    [InlineData(typeof(SqlExecutionProvider), ProviderKind.Sql)]
    [InlineData(typeof(CacheExecutionProvider), ProviderKind.Cache)]
    [InlineData(typeof(GraphExecutionProvider), ProviderKind.Graph)]
    public void EachProvider_ReportsItsOwnKind(Type providerType, ProviderKind expectedKind)
    {
        var provider = (IExecutionProvider)Activator.CreateInstance(providerType)!;

        Assert.Equal(expectedKind, provider.Kind);
    }

    // These providers are currently unimplemented skeletons (see the file
    // comments in src/Foundgine.Providers/) -- this test documents that
    // contract so it fails loudly, right here, the moment someone forgets to
    // update it while wiring up a real implementation.
    [Fact]
    public async Task SqlExecutionProvider_ExecuteAsync_IsNotYetImplemented()
    {
        var provider = new SqlExecutionProvider();

        await Assert.ThrowsAsync<NotImplementedException>(async () =>
        {
            await foreach (var _ in provider.ExecuteAsync(EmptyPlan(), EmptyContext()))
            {
            }
        });
    }

    [Fact]
    public async Task CacheExecutionProvider_ExecuteAsync_IsNotYetImplemented()
    {
        var provider = new CacheExecutionProvider();

        await Assert.ThrowsAsync<NotImplementedException>(async () =>
        {
            await foreach (var _ in provider.ExecuteAsync(EmptyPlan(), EmptyContext()))
            {
            }
        });
    }

    [Fact]
    public async Task GraphExecutionProvider_ExecuteAsync_IsNotYetImplemented()
    {
        var provider = new GraphExecutionProvider();

        await Assert.ThrowsAsync<NotImplementedException>(async () =>
        {
            await foreach (var _ in provider.ExecuteAsync(EmptyPlan(), EmptyContext()))
            {
            }
        });
    }
}
