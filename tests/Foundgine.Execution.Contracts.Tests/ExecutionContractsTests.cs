using Foundgine.Metadata;
using Xunit;

namespace Foundgine.Execution.Contracts.Tests;

public class ExecutionContextTests
{
    [Fact]
    public void TwoContexts_WithSameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        var vars = new Dictionary<string, object?> { ["x"] = 1 };

        var a = new ExecutionContext(id, vars);
        var b = new ExecutionContext(id, vars);

        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentExecutionIds_AreNotEqual()
    {
        var vars = new Dictionary<string, object?>();

        var a = new ExecutionContext(Guid.NewGuid(), vars);
        var b = new ExecutionContext(Guid.NewGuid(), vars);

        Assert.NotEqual(a, b);
    }
}

public class ExecutionOptionsTests
{
    [Fact]
    public void Defaults_AreDiagnosticsOff_AndMaxDepth64()
    {
        var options = new ExecutionOptions();

        Assert.False(options.EnableDiagnostics);
        Assert.Equal(64, options.MaxDepth);
    }

    [Fact]
    public void CanOverrideEachDefault()
    {
        var options = new ExecutionOptions(EnableDiagnostics: true, MaxDepth: 8);

        Assert.True(options.EnableDiagnostics);
        Assert.Equal(8, options.MaxDepth);
    }
}

public class ExecutionResultTests
{
    [Fact]
    public void Success_CarriesData_AndNoErrors()
    {
        var result = new ExecutionResult(true, new { Name = "x" }, Array.Empty<string>());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failure_CarriesErrors()
    {
        var result = new ExecutionResult(false, null, new[] { "not found" });

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Single(result.Errors);
    }
}

public class ExecutionRowTests
{
    [Fact]
    public void Entities_AreKeyedByEntityId()
    {
        var row = new ExecutionRow(
        [
            new EntityOccurrence(
                new EntityId(1),
                0,
                new object?[] { "Bob", 42 })
        ]);

        var entity = Assert.Single(row.Occurrences);

        Assert.Equal(new EntityId(1), entity.EntityId);
        Assert.Equal(0, entity.OccurrenceIndex);
        Assert.Equal(new object?[] { "Bob", 42 }, entity.Values);
    }
}

public class ProviderPlanTests
{
    private static EntityMetadata Entity() =>
        new(new EntityId(1), "Customer", Array.Empty<ColumnMetadata>());

    [Fact]
    public void ProviderPlan_WrapsRootNode()
    {
        var root = new SqlScanNode(Entity());

        var plan = new ProviderPlan(root);

        Assert.Same(root, plan.Root);
    }

    [Fact]
    public void SqlJoinNode_HoldsLeftAndRightSubtrees_AndJoinMetadata()
    {
        var entity = Entity();
        var left = new SqlScanNode(entity);
        var right = new SqlScanNode(entity);
        var join = new JoinMetadata(
            new JoinCondition(new ColumnReference(entity, 1), new ColumnReference(entity, 2)),
            JoinKind.Inner);

        var node = new SqlJoinNode(left, right, join);

        Assert.Same(left, node.Left);
        Assert.Same(right, node.Right);
        Assert.Same(join, node.Join);
    }

    [Fact]
    public void ProviderNode_Subtypes_AreDistinctFromEachOther()
    {
        ProviderNode scan = new SqlScanNode(Entity());
        ProviderNode cache = new CacheLookupNode(Entity(), Array.Empty<ColumnReference>());

        Assert.IsType<SqlScanNode>(scan);
        Assert.IsType<CacheLookupNode>(cache);
        Assert.IsNotType<SqlScanNode>(cache);
    }
}

public class IExecutionProviderTests
{
    private sealed class StubProvider : IExecutionProvider
    {
        public ProviderKind Kind => ProviderKind.Sql;

        public async IAsyncEnumerable<ExecutionRow> ExecuteAsync(
            ProviderPlan plan,
            ExecutionContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            yield return new ExecutionRow(
                Array.Empty<EntityOccurrence>());
        }
    }

    [Fact]
    public async Task ExecuteAsync_CanBeEnumerated_ByAnyImplementation()
    {
        var provider = new StubProvider();
        var plan = new ProviderPlan(new SqlScanNode(new EntityMetadata(new EntityId(1), "X", Array.Empty<ColumnMetadata>())));
        var context = new ExecutionContext(Guid.NewGuid(), new Dictionary<string, object?>());

        var rows = new List<ExecutionRow>();
        await foreach (var row in provider.ExecuteAsync(plan, context))
            rows.Add(row);

        Assert.Single(rows);
        Assert.Equal(ProviderKind.Sql, provider.Kind);
    }
}
