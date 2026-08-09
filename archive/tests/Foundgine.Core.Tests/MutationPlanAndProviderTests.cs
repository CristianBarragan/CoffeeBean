using Foundgine.Core.MutationPlan;
using Foundgine.Core.Provider;
using Foundgine.Execution.Contracts;
using Foundgine.Metadata;
using Xunit;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

namespace Foundgine.Core.Tests;

public class MutationPlanTests
{
    private static EntityMetadata Entity(ushort id = 1, string name = "Customer") =>
        new(new EntityId(id), name, Array.Empty<ColumnMetadata>());

    [Fact]
    public void MutationPlan_HoldsOperationsInOrder()
    {
        var entity = Entity();
        var op1 = new EntityMutation(entity, MutationKind.Create, Array.Empty<MutationColumn>());
        var op2 = new EntityMutation(entity, MutationKind.Update, Array.Empty<MutationColumn>());

        var plan = new global::Foundgine.Core.MutationPlan.MutationPlan(new MutationOperation[] { op1, op2 });

        Assert.Equal(new MutationOperation[] { op1, op2 }, plan.Operations);
    }

    [Fact]
    public void EntityMutation_IsAMutationOperation_CarryingKindAndColumns()
    {
        var entity = Entity();
        var column = new MutationColumn(
            new ColumnReference(entity, 1),
            SourceFieldId: 1,
            MutationValueKind.Input,
            IsPrimaryKey: true);

        MutationOperation op = new EntityMutation(entity, MutationKind.Upsert, new[] { column });

        var entityMutation = Assert.IsType<EntityMutation>(op);
        Assert.Equal(MutationKind.Upsert, entityMutation.Kind);
        Assert.Single(entityMutation.Columns);
        Assert.True(entityMutation.Columns[0].IsPrimaryKey);
    }

    [Fact]
    public void GraphMutation_CarriesFromAndToEntityMutations()
    {
        var customer = Entity(1, "Customer");
        var account = Entity(2, "Account");
        var graph = new GraphMetadata(
            new GraphId(1),
            "owns",
            "OWNS",
            "edge_id",
            Entity(3, "OwnsEdge"),
            new VertexMetadata("Customer", "id", "customer_id", "c", customer),
            new VertexMetadata("Account", "id", "account_id", "a", account));
        var from = new EntityMutation(customer, MutationKind.Create, Array.Empty<MutationColumn>());
        var to = new EntityMutation(account, MutationKind.Create, Array.Empty<MutationColumn>());

        var mutation = new GraphMutation(graph, from, to);

        Assert.Same(from, mutation.From);
        Assert.Same(to, mutation.To);
        Assert.Same(graph, mutation.Graph);
    }

    [Fact]
    public void RelationshipMutation_CarriesParentChildAndJoinCondition()
    {
        var parent = Entity(1, "Customer");
        var child = Entity(2, "Address");
        var condition = new JoinCondition(new ColumnReference(parent, 1), new ColumnReference(child, 1));

        var mutation = new RelationshipMutation(parent, child, condition);

        Assert.Same(parent, mutation.Parent);
        Assert.Same(child, mutation.Child);
        Assert.Same(condition, mutation.Condition);
    }
}

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
    // comments in src/Foundgine.Core/Provider/) -- this test documents that
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
