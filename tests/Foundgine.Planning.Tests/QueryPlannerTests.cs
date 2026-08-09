using Foundgine.Builders;
using Foundgine.Metadata;
using Xunit;

namespace Foundgine.Planning.Tests;

public class QueryPlannerTests
{
    // Customer -> Account -> Transaction, the exact chain the first E2E
    // targets, kept local to this test file rather than reused from the
    // sample so Foundgine.Planning.Tests doesn't need a ProjectReference on
    // samples/.
    private static (MetadataRegistry Metadata, JoinGraph Joins, EntityMetadata Customer, EntityMetadata Account, EntityMetadata Transaction, EntityMetadata ContactPoint) BuildBankingGraph()
    {
        var customer = new EntityMetadata(
            new EntityId(1), "Customer",
            new ColumnMetadata[] { new(new ColumnId(1), "Id"), new(new ColumnId(2), "Name") });

        var account = new EntityMetadata(
            new EntityId(2), "Account",
            new ColumnMetadata[] { new(new ColumnId(1), "Id"), new(new ColumnId(2), "CustomerId"), new(new ColumnId(3), "Balance") });

        var transaction = new EntityMetadata(
            new EntityId(3), "Transaction",
            new ColumnMetadata[] { new(new ColumnId(1), "Id"), new(new ColumnId(2), "AccountId"), new(new ColumnId(3), "Amount") });

        // A second child of Customer, unrelated to Account/Transaction, so
        // branching tests have real fan-out to plan: Customer -> Accounts
        // -> Transactions *and* Customer -> ContactPoints, per the roadmap's
        // FOUND-002 example.
        var contactPoint = new EntityMetadata(
            new EntityId(4), "ContactPoint",
            new ColumnMetadata[] { new(new ColumnId(1), "Id"), new(new ColumnId(2), "CustomerId"), new(new ColumnId(3), "Kind") });

        var metadata = new MetadataRegistry();
        metadata.Register(customer);
        metadata.Register(account);
        metadata.Register(transaction);
        metadata.Register(contactPoint);

        var joins = new JoinGraph();
        joins.AddEdge(
            customer.EntityId, account.EntityId,
            new JoinMetadata(
                new JoinCondition(
                    Left: new ColumnReference(account, ColumnId: 2),   // Account.CustomerId
                    Right: new ColumnReference(customer, ColumnId: 1)), // Customer.Id
                JoinKind.Inner));
        joins.AddEdge(
            account.EntityId, transaction.EntityId,
            new JoinMetadata(
                new JoinCondition(
                    Left: new ColumnReference(transaction, ColumnId: 2), // Transaction.AccountId
                    Right: new ColumnReference(account, ColumnId: 1)),   // Account.Id
                JoinKind.Inner));
        joins.AddEdge(
            customer.EntityId, contactPoint.EntityId,
            new JoinMetadata(
                new JoinCondition(
                    Left: new ColumnReference(contactPoint, ColumnId: 2),  // ContactPoint.CustomerId
                    Right: new ColumnReference(customer, ColumnId: 1)),    // Customer.Id
                JoinKind.Inner));

        return (metadata, joins, customer, account, transaction, contactPoint);
    }

    [Fact]
    public void Plan_WithEmptyPath_ProducesASingleScan()
    {
        var (metadata, joins, customer, _, _, _) = BuildBankingGraph();
        var planner = new QueryPlanner(metadata, joins);

        var plan = planner.Plan(QueryIntent.Linear(customer.EntityId, Array.Empty<EntityId>()));

        var scan = Assert.IsType<ScanNode>(plan.Root);
        Assert.Same(customer, scan.Entity);
    }

    [Fact]
    public void Plan_CustomerToAccount_ProducesAJoinNode_UsingTheRegisteredJoin()
    {
        var (metadata, joins, customer, account, _, _) = BuildBankingGraph();
        var planner = new QueryPlanner(metadata, joins);

        var plan = planner.Plan(QueryIntent.Linear(customer.EntityId, new[] { account.EntityId }));

        var join = Assert.IsType<JoinNode>(plan.Root);
        var left = Assert.IsType<ScanNode>(join.Left);
        var right = Assert.IsType<ScanNode>(join.Right);
        Assert.Same(customer, left.Entity);
        Assert.Same(account, right.Entity);
        Assert.True(joins.TryGetJoin(customer.EntityId, account.EntityId, out var expected));
        Assert.Equal(expected, join.Join);
    }

    [Fact]
    public void Plan_CustomerToAccountToTransaction_ProducesANestedJoinChain()
    {
        // This is the exact "🔴 NOW" checklist scenario: Customer -> Account
        // -> Transaction, planned dynamically rather than hand-assembled.
        var (metadata, joins, customer, account, transaction, _) = BuildBankingGraph();
        var planner = new QueryPlanner(metadata, joins);

        var plan = planner.Plan(QueryIntent.Linear(customer.EntityId, new[] { account.EntityId, transaction.EntityId }));

        var outerJoin = Assert.IsType<JoinNode>(plan.Root);
        Assert.Same(transaction, Assert.IsType<ScanNode>(outerJoin.Right).Entity);

        var innerJoin = Assert.IsType<JoinNode>(outerJoin.Left);
        Assert.Same(customer, Assert.IsType<ScanNode>(innerJoin.Left).Entity);
        Assert.Same(account, Assert.IsType<ScanNode>(innerJoin.Right).Entity);
    }

    [Fact]
    public void Plan_WithFields_WrapsTheTreeInAProjectionNode()
    {
        var (metadata, joins, customer, account, _, _) = BuildBankingGraph();
        var planner = new QueryPlanner(metadata, joins);
        var fields = new[]
        {
            new FieldBinding(new ColumnReference(customer, 2), TargetFieldId: 1),
            new FieldBinding(new ColumnReference(account, 3), TargetFieldId: 2),
        };

        var plan = planner.Plan(QueryIntent.Linear(customer.EntityId, new[] { account.EntityId }, fields));

        var projection = Assert.IsType<ProjectionNode>(plan.Root);
        Assert.Same(fields, projection.Fields);
        Assert.IsType<JoinNode>(projection.Source);
    }

    [Fact]
    public void Plan_WithNoRegisteredRelationship_Throws()
    {
        var (metadata, joins, customer, _, transaction, _) = BuildBankingGraph();
        var planner = new QueryPlanner(metadata, joins);

        // Customer -> Transaction directly (skipping Account) has no
        // registered edge — the planner must not invent one.
        var ex = Assert.Throws<InvalidOperationException>(
            () => planner.Plan(QueryIntent.Linear(customer.EntityId, new[] { transaction.EntityId })));

        Assert.Contains("Customer", ex.Message);
        Assert.Contains("Transaction", ex.Message);
    }

    [Fact]
    public void Plan_WithUnregisteredRootEntity_Throws()
    {
        var (metadata, joins, _, _, _, _) = BuildBankingGraph();
        var planner = new QueryPlanner(metadata, joins);

        Assert.Throws<InvalidOperationException>(
            () => planner.Plan(QueryIntent.Linear(new EntityId(999), Array.Empty<EntityId>())));
    }

    // ------------------------------------------------------------------
    // Branching: FOUND-002. QueryIntent.Branches is a tree, so a Customer
    // can fan out to Accounts *and* ContactPoints in the same intent,
    // rather than only a single linear path.
    // ------------------------------------------------------------------

    [Fact]
    public void Plan_CustomerToAccountsAndContactPoints_JoinsBothSiblingBranches()
    {
        var (metadata, joins, customer, account, _, contactPoint) = BuildBankingGraph();
        var planner = new QueryPlanner(metadata, joins);

        var intent = new QueryIntent(
            customer.EntityId,
            new[]
            {
                new QueryIntentBranch(account.EntityId),
                new QueryIntentBranch(contactPoint.EntityId),
            });

        var plan = planner.Plan(intent);

        // Two siblings under Customer means two JoinNodes, both ultimately
        // scanning Customer once. Walk the (left-associated) chain and
        // collect every entity it scans, since the exact tree shape is an
        // implementation detail — what matters is that every entity in the
        // intent ends up joined together correctly.
        var scannedEntities = CollectScannedEntities(plan.Root);
        Assert.Equal(
            new[] { customer, account, contactPoint },
            scannedEntities);

        var outerJoin = Assert.IsType<JoinNode>(plan.Root);
        Assert.Same(contactPoint, Assert.IsType<ScanNode>(outerJoin.Right).Entity);
        Assert.True(joins.TryGetJoin(customer.EntityId, contactPoint.EntityId, out var expectedContactJoin));
        Assert.Equal(expectedContactJoin, outerJoin.Join);
    }

    [Fact]
    public void Plan_CustomerToAccountsToTransactionsAndContactPoints_JoinsTheWholeTree()
    {
        // The exact FOUND-002 example:
        //
        //   Customer
        //   ├── Accounts
        //   │    └── Transactions
        //   └── ContactPoints
        var (metadata, joins, customer, account, transaction, contactPoint) = BuildBankingGraph();
        var planner = new QueryPlanner(metadata, joins);

        var intent = new QueryIntent(
            customer.EntityId,
            new[]
            {
                new QueryIntentBranch(
                    account.EntityId,
                    new[] { new QueryIntentBranch(transaction.EntityId) }),
                new QueryIntentBranch(contactPoint.EntityId),
            });

        var plan = planner.Plan(intent);

        Assert.Equal(
            new[] { customer, account, transaction, contactPoint },
            CollectScannedEntities(plan.Root));
    }

    [Fact]
    public void Plan_BranchWithNoRegisteredRelationship_Throws()
    {
        var (metadata, joins, customer, account, _, contactPoint) = BuildBankingGraph();
        var planner = new QueryPlanner(metadata, joins);

        // Account -> ContactPoint has no registered edge (only Customer ->
        // ContactPoint does) — nesting ContactPoint under Account must be
        // rejected rather than silently resolved against a different edge.
        var intent = new QueryIntent(
            customer.EntityId,
            new[]
            {
                new QueryIntentBranch(
                    account.EntityId,
                    new[] { new QueryIntentBranch(contactPoint.EntityId) }),
            });

        var ex = Assert.Throws<InvalidOperationException>(() => planner.Plan(intent));

        Assert.Contains("Account", ex.Message);
        Assert.Contains("ContactPoint", ex.Message);
    }

    private static IReadOnlyList<EntityMetadata> CollectScannedEntities(QueryNode node)
    {
        var found = new List<EntityMetadata>();
        Visit(node);
        return found;

        void Visit(QueryNode current)
        {
            switch (current)
            {
                case ScanNode scan:
                    found.Add(scan.Entity);
                    break;
                case JoinNode join:
                    Visit(join.Left);
                    Visit(join.Right);
                    break;
                case ProjectionNode projection:
                    Visit(projection.Source);
                    break;
            }
        }
    }
}
