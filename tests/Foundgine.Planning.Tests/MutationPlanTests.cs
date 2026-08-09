using Foundgine.Builders;
using Foundgine.Metadata;
using Xunit;

namespace Foundgine.Planning.Tests;

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

        var plan = new MutationPlan(new MutationOperation[] { op1, op2 });

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
