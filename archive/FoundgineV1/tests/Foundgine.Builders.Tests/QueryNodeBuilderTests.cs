using Foundgine.Metadata;
using Xunit;

namespace Foundgine.Builders.Tests;

public class QueryNodeBuilderTests
{
    private static EntityMetadata Entity(ushort id, string name) =>
        new(new EntityId(id), name, Array.Empty<ColumnMetadata>());

    [Fact]
    public void ScanComposite_SingleEntityModel_ReturnsBareScanNode()
    {
        var entity = Entity(1, "Customer");
        var model = new ModelMetadata(
            new ModelId(1),
            "Customer",
            typeof(object),
            Array.Empty<FieldMetadata>(),
            new[] { new ModelEntityBinding(entity, JoinToParent: null) });

        var node = QueryNodeBuilder.ScanComposite(model);

        var scan = Assert.IsType<ScanNode>(node);
        Assert.Equal(entity, scan.Entity);
    }

    [Fact]
    public void ScanComposite_MultiEntityModel_BuildsLeftJoinChain_InEntityOrder()
    {
        var primary = Entity(1, "Customer");
        var detail = Entity(2, "CustomerDetail");
        var joinCondition = new JoinCondition(
            new ColumnReference(primary, 1),
            new ColumnReference(detail, 1));

        var model = new ModelMetadata(
            new ModelId(1),
            "Customer",
            typeof(object),
            Array.Empty<FieldMetadata>(),
            new[]
            {
                new ModelEntityBinding(primary, JoinToParent: null),
                new ModelEntityBinding(detail, joinCondition),
            });

        var node = QueryNodeBuilder.ScanComposite(model);

        var join = Assert.IsType<JoinNode>(node);
        Assert.Equal(JoinKind.Left, join.Join.Kind);
        Assert.Same(joinCondition, join.Join.Condition);
        var left = Assert.IsType<ScanNode>(join.Left);
        Assert.Equal(primary, left.Entity);
        var right = Assert.IsType<ScanNode>(join.Right);
        Assert.Equal(detail, right.Entity);
    }

    [Fact]
    public void ScanComposite_ThreeEntityModel_ChainsJoinsLeftDeep()
    {
        var a = Entity(1, "A");
        var b = Entity(2, "B");
        var c = Entity(3, "C");
        var joinAB = new JoinCondition(new ColumnReference(a, 1), new ColumnReference(b, 1));
        var joinBC = new JoinCondition(new ColumnReference(b, 2), new ColumnReference(c, 1));

        var model = new ModelMetadata(
            new ModelId(1),
            "ABC",
            typeof(object),
            Array.Empty<FieldMetadata>(),
            new[]
            {
                new ModelEntityBinding(a, JoinToParent: null),
                new ModelEntityBinding(b, joinAB),
                new ModelEntityBinding(c, joinBC),
            });

        var node = QueryNodeBuilder.ScanComposite(model);

        // Root join is (A join B) join C -- the outer join's Right side is the
        // most recently added entity, and its Left side is everything before it.
        var outer = Assert.IsType<JoinNode>(node);
        Assert.Same(joinBC, outer.Join.Condition);
        var outerRight = Assert.IsType<ScanNode>(outer.Right);
        Assert.Equal(c, outerRight.Entity);

        var inner = Assert.IsType<JoinNode>(outer.Left);
        Assert.Same(joinAB, inner.Join.Condition);
        Assert.Equal(a, Assert.IsType<ScanNode>(inner.Left).Entity);
        Assert.Equal(b, Assert.IsType<ScanNode>(inner.Right).Entity);
    }

    [Fact]
    public void ScanComposite_NoEntities_Throws()
    {
        var model = new ModelMetadata(
            new ModelId(1),
            "Empty",
            typeof(object),
            Array.Empty<FieldMetadata>(),
            Array.Empty<ModelEntityBinding>());

        var ex = Assert.Throws<InvalidOperationException>(() => QueryNodeBuilder.ScanComposite(model));
        Assert.Contains("Empty", ex.Message);
    }
}

public class QueryPlanTests
{
    [Fact]
    public void QueryPlan_WrapsRootNode()
    {
        var entity = new EntityMetadata(new EntityId(1), "Customer", Array.Empty<ColumnMetadata>());
        var root = new ScanNode(entity);

        var plan = new QueryPlan(root);

        Assert.Same(root, plan.Root);
    }

    [Fact]
    public void QueryPlan_Equality_IsStructural()
    {
        var entity = new EntityMetadata(new EntityId(1), "Customer", Array.Empty<ColumnMetadata>());

        var planA = new QueryPlan(new ScanNode(entity));
        var planB = new QueryPlan(new ScanNode(entity));

        Assert.Equal(planA, planB);
    }
}
