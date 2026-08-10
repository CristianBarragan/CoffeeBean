using Foundgine.Builders;
using Foundgine.Execution.Contracts;
using Foundgine.Metadata;
using Xunit;

namespace Foundgine.Providers.Tests;

public class SqlPlanCompilerTests
{
    private static EntityMetadata Entity(ushort id, string name, params string[] columns) =>
        new(new EntityId(id), name,
            columns.Select((c, i) => new ColumnMetadata(new ColumnId((ushort)(i + 1)), c)).ToArray());

    [Fact]
    public void Compile_ScanNode_ProducesSqlScanNode_ForTheSameEntity()
    {
        var customer = Entity(1, "Customer", "Id", "Name");
        var plan = new QueryPlan(new ScanNode(customer));

        var compiled = SqlPlanCompiler.Compile(plan);

        var scan = Assert.IsType<SqlScanNode>(compiled.Root);
        Assert.Same(customer, scan.Entity);
    }

    [Fact]
    public void Compile_JoinNode_ProducesSqlJoinNode_PreservingJoinMetadata()
    {
        var customer = Entity(1, "Customer", "Id");
        var account = Entity(2, "Account", "Id", "CustomerId");
        var join = new JoinMetadata(
            new JoinCondition(new ColumnReference(account, 2), new ColumnReference(customer, 1)),
            JoinKind.Inner);
        var plan = new QueryPlan(new JoinNode(new ScanNode(customer), new ScanNode(account), join));

        var compiled = SqlPlanCompiler.Compile(plan);

        var sqlJoin = Assert.IsType<SqlJoinNode>(compiled.Root);
        Assert.Same(join, sqlJoin.Join);
        Assert.Same(customer, Assert.IsType<SqlScanNode>(sqlJoin.Left).Entity);
        Assert.Same(account, Assert.IsType<SqlScanNode>(sqlJoin.Right).Entity);
    }

    [Fact]
    public void Compile_CompositeNode_WithNoChildren_ProducesASqlScanNode()
    {
        var customer = Entity(1, "Customer", "Id");
        var plan = new QueryPlan(new CompositeNode(customer, Array.Empty<CompositeEdge>()));

        var compiled = SqlPlanCompiler.Compile(plan);

        var scan = Assert.IsType<SqlScanNode>(compiled.Root);
        Assert.Same(customer, scan.Entity);
    }

    [Fact]
    public void Compile_CompositeNode_LinearChain_FlattensIntoNestedSqlJoinNodes()
    {
        // Customer -> Account -> Transaction as a CompositeNode tree should
        // compile to the same nested SqlJoinNode chain a hand-built
        // QueryPlan of JoinNodes would have.
        var customer = Entity(1, "Customer", "Id");
        var account = Entity(2, "Account", "Id", "CustomerId");
        var transaction = Entity(3, "Transaction", "Id", "AccountId");
        var customerToAccount = new JoinMetadata(
            new JoinCondition(new ColumnReference(account, 2), new ColumnReference(customer, 1)), JoinKind.Inner);
        var accountToTransaction = new JoinMetadata(
            new JoinCondition(new ColumnReference(transaction, 2), new ColumnReference(account, 1)), JoinKind.Inner);

        var composite = new CompositeNode(
            customer,
            new[]
            {
                new CompositeEdge(
                    customerToAccount,
                    new CompositeNode(
                        account,
                        new[] { new CompositeEdge(accountToTransaction, new CompositeNode(transaction, Array.Empty<CompositeEdge>())) })),
            });

        var compiled = SqlPlanCompiler.Compile(new QueryPlan(composite));

        var outerJoin = Assert.IsType<SqlJoinNode>(compiled.Root);
        Assert.Same(accountToTransaction, outerJoin.Join);
        Assert.Same(transaction, Assert.IsType<SqlScanNode>(outerJoin.Right).Entity);

        var innerJoin = Assert.IsType<SqlJoinNode>(outerJoin.Left);
        Assert.Same(customerToAccount, innerJoin.Join);
        Assert.Same(customer, Assert.IsType<SqlScanNode>(innerJoin.Left).Entity);
        Assert.Same(account, Assert.IsType<SqlScanNode>(innerJoin.Right).Entity);
    }

    [Fact]
    public void Compile_CompositeNode_WithSiblingBranches_JoinsBothSiblingsOntoTheSameRoot()
    {
        // Customer ├── Account └── ContactPoint: two children of the same
        // parent should both end up joined into a single connected chain,
        // not lose one branch or nest one under the other.
        var customer = Entity(1, "Customer", "Id");
        var account = Entity(2, "Account", "Id", "CustomerId");
        var contactPoint = Entity(3, "ContactPoint", "Id", "CustomerId");
        var customerToAccount = new JoinMetadata(
            new JoinCondition(new ColumnReference(account, 2), new ColumnReference(customer, 1)), JoinKind.Inner);
        var customerToContactPoint = new JoinMetadata(
            new JoinCondition(new ColumnReference(contactPoint, 2), new ColumnReference(customer, 1)), JoinKind.Inner);

        var composite = new CompositeNode(
            customer,
            new[]
            {
                new CompositeEdge(customerToAccount, new CompositeNode(account, Array.Empty<CompositeEdge>())),
                new CompositeEdge(customerToContactPoint, new CompositeNode(contactPoint, Array.Empty<CompositeEdge>())),
            });

        var compiled = SqlPlanCompiler.Compile(new QueryPlan(composite));

        var outerJoin = Assert.IsType<SqlJoinNode>(compiled.Root);
        Assert.Same(customerToContactPoint, outerJoin.Join);
        Assert.Same(contactPoint, Assert.IsType<SqlScanNode>(outerJoin.Right).Entity);

        var innerJoin = Assert.IsType<SqlJoinNode>(outerJoin.Left);
        Assert.Same(customerToAccount, innerJoin.Join);
        Assert.Same(customer, Assert.IsType<SqlScanNode>(innerJoin.Left).Entity);
        Assert.Same(account, Assert.IsType<SqlScanNode>(innerJoin.Right).Entity);
    }

    [Fact]
    public void Compile_ProjectionNode_ProducesSqlProjectionNode_PreservingFields()
    {
        var customer = Entity(1, "Customer", "Id", "Name");
        var fields = new[] { new FieldBinding(new ColumnReference(customer, 2), 1) };
        var plan = new QueryPlan(new ProjectionNode(new ScanNode(customer), fields));

        var compiled = SqlPlanCompiler.Compile(plan);

        var projection = Assert.IsType<SqlProjectionNode>(compiled.Root);
        Assert.Same(fields, projection.Fields);
        Assert.IsType<SqlScanNode>(projection.Source);
    }

    [Fact]
    public void Compile_GraphEdgeNode_Throws()
    {
        var customer = Entity(1, "Customer", "Id");
        var graph = new GraphMetadata(
            new GraphId(1), "owns", "OWNS", "edge_id",
            Entity(2, "OwnsEdge", "edge_id"),
            new VertexMetadata("Customer", "id", "customer_id", "c", customer),
            new VertexMetadata("Account", "id", "account_id", "a", Entity(3, "Account", "id")));
        var plan = new QueryPlan(new GraphEdgeNode(new ScanNode(customer), graph, From: null, To: null));

        Assert.Throws<NotSupportedException>(() => SqlPlanCompiler.Compile(plan));
    }

    [Fact]
    public void Compile_MaterializeNode_Throws()
    {
        var customer = Entity(1, "Customer", "Id");
        var model = new ModelMetadata(
            new ModelId(1), "CustomerModel", typeof(object),
            Array.Empty<FieldMetadata>(),
            new[] { new ModelEntityBinding(customer, null) });
        var plan = new QueryPlan(new MaterializeNode(new ScanNode(customer), model));

        Assert.Throws<NotSupportedException>(() => SqlPlanCompiler.Compile(plan));
    }
}