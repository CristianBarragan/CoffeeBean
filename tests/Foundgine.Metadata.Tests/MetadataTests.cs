using Xunit;

namespace Foundgine.Metadata.Tests;

public class StrongIdTests
{
    [Fact]
    public void EntityId_Equality_IsByValue()
    {
        Assert.Equal(new EntityId(7), new EntityId(7));
        Assert.NotEqual(new EntityId(7), new EntityId(8));
    }

    [Fact]
    public void EntityId_CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<EntityId, string>
        {
            [new EntityId(1)] = "customer",
        };

        Assert.Equal("customer", dict[new EntityId(1)]);
    }

    [Fact]
    public void DistinctIdTypes_WithSameUnderlyingValue_AreNotInterchangeable()
    {
        // EntityId and FieldId both wrap a ushort, but the compiler must
        // still treat them as distinct types -- this is the whole point of
        // strongly-typed ids over a bare ushort.
        var entityId = new EntityId(1);
        var fieldId = new FieldId(1);

        Assert.Equal(entityId.Value, fieldId.Value);
        Assert.False(entityId.Equals((object)fieldId));
    }
}

public class MetadataRegistryTests
{
    private static EntityMetadata MakeEntity(ushort id, string name) =>
        new(new EntityId(id), name, Array.Empty<ColumnMetadata>());

    [Fact]
    public void Register_ThenTryGet_ReturnsTrue_AndTheEntity()
    {
        var registry = new MetadataRegistry();
        var entity = MakeEntity(1, "Customer");

        registry.Register(entity);
        var found = registry.TryGet(new EntityId(1), out var result);

        Assert.True(found);
        Assert.Same(entity, result);
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForUnregisteredEntity()
    {
        var registry = new MetadataRegistry();

        var found = registry.TryGet(new EntityId(99), out var result);

        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void Get_ReturnsRegisteredEntity()
    {
        var registry = new MetadataRegistry();
        var entity = MakeEntity(2, "Account");
        registry.Register(entity);

        var result = registry.Get(new EntityId(2));

        Assert.Same(entity, result);
    }

    [Fact]
    public void Get_Throws_ForUnregisteredEntity()
    {
        var registry = new MetadataRegistry();

        var ex = Assert.Throws<KeyNotFoundException>(() => registry.Get(new EntityId(99)));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public void Register_SameId_OverwritesPreviousEntity()
    {
        var registry = new MetadataRegistry();
        registry.Register(MakeEntity(1, "First"));
        registry.Register(MakeEntity(1, "Second"));

        var result = registry.Get(new EntityId(1));

        Assert.Equal("Second", result.Name);
    }
}

public class JoinGraphTests
{
    [Fact]
    public void AddEdge_MakesForwardDirectionLookupSucceed()
    {
        var graph = new JoinGraph();
        var customer = new EntityId(1);
        var account = new EntityId(2);
        var entity = new EntityMetadata(customer, "Customer", Array.Empty<ColumnMetadata>());
        var join = new JoinMetadata(
            new JoinCondition(
                new ColumnReference(entity, 1),
                new ColumnReference(entity, 2)),
            JoinKind.Inner);

        graph.AddEdge(customer, account, join);

        Assert.True(graph.TryGetJoin(customer, account, out var found));
        Assert.Equal(join, found);
    }

    [Fact]
    public void AddEdge_AlsoIndexesReverseDirection_WithSwappedColumns()
    {
        var graph = new JoinGraph();
        var customer = new EntityId(1);
        var account = new EntityId(2);
        var entity = new EntityMetadata(customer, "Customer", Array.Empty<ColumnMetadata>());
        var left = new ColumnReference(entity, 1);
        var right = new ColumnReference(entity, 2);
        var join = new JoinMetadata(new JoinCondition(left, right), JoinKind.Left);

        graph.AddEdge(customer, account, join);

        Assert.True(graph.TryGetJoin(account, customer, out var reversed));
        Assert.Equal(right, reversed.Condition.Left);
        Assert.Equal(left, reversed.Condition.Right);
        // "Customer LEFT JOIN Account", read from Account's side, is
        // "Account RIGHT JOIN Customer" — not "Account LEFT JOIN Customer".
        Assert.Equal(JoinKind.Right, reversed.Kind);
    }

    [Theory]
    [InlineData(JoinKind.Inner, JoinKind.Inner)]
    [InlineData(JoinKind.Left, JoinKind.Right)]
    [InlineData(JoinKind.Right, JoinKind.Left)]
    [InlineData(JoinKind.Full, JoinKind.Full)]
    public void AddEdge_ReversesJoinKind_ToPreserveMeaning_ForEveryKind(JoinKind forward, JoinKind expectedReversed)
    {
        var graph = new JoinGraph();
        var a = new EntityId(1);
        var b = new EntityId(2);
        var entity = new EntityMetadata(a, "A", Array.Empty<ColumnMetadata>());
        var join = new JoinMetadata(
            new JoinCondition(new ColumnReference(entity, 1), new ColumnReference(entity, 2)),
            forward);

        graph.AddEdge(a, b, join);

        Assert.True(graph.TryGetJoin(b, a, out var reversed));
        Assert.Equal(expectedReversed, reversed.Kind);
    }

    [Fact]
    public void AddEdge_DoesNotOverwriteExistingExplicitReverseEdge()
    {
        var graph = new JoinGraph();
        var a = new EntityId(1);
        var b = new EntityId(2);
        var entity = new EntityMetadata(a, "A", Array.Empty<ColumnMetadata>());
        var forwardJoin = new JoinMetadata(
            new JoinCondition(new ColumnReference(entity, 1), new ColumnReference(entity, 2)),
            JoinKind.Inner);
        var explicitReverse = new JoinMetadata(
            new JoinCondition(new ColumnReference(entity, 9), new ColumnReference(entity, 9)),
            JoinKind.Right);

        graph.AddEdge(b, a, explicitReverse);
        graph.AddEdge(a, b, forwardJoin);

        graph.TryGetJoin(b, a, out var result);
        Assert.Equal(JoinKind.Right, result.Kind);
    }

    [Fact]
    public void TryGetJoin_ReturnsFalse_WhenNoEdgeExists()
    {
        var graph = new JoinGraph();

        Assert.False(graph.TryGetJoin(new EntityId(1), new EntityId(2), out _));
    }

    [Fact]
    public void EdgesFrom_ReturnsOnlyEdgesOriginatingAtTheGivenEntity()
    {
        var graph = new JoinGraph();
        var a = new EntityId(1);
        var b = new EntityId(2);
        var c = new EntityId(3);
        var entity = new EntityMetadata(a, "A", Array.Empty<ColumnMetadata>());
        var join = new JoinMetadata(
            new JoinCondition(new ColumnReference(entity, 1), new ColumnReference(entity, 2)),
            JoinKind.Inner);

        graph.AddEdge(a, b, join);
        graph.AddEdge(a, c, join);

        var edges = graph.EdgesFrom(a).ToList();

        Assert.Equal(2, edges.Count);
        Assert.Contains(edges, e => e.To == b);
        Assert.Contains(edges, e => e.To == c);
    }
}