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
    public void Plan_WithEmptyPath_ProducesACompositeNode_WithNoChildren()
    {
        var (metadata, joins, customer, _, _, _) = BuildBankingGraph();
        var planner = new QueryPlanner(metadata, joins);

        var plan = planner.Plan(QueryIntent.Linear(customer.EntityId, Array.Empty<EntityId>()));

        var composite = Assert.IsType<CompositeNode>(plan.Root);
        Assert.Same(customer, composite.Entity);
        Assert.Empty(composite.Children);
    }

    [Fact]
    public void Plan_CustomerToAccount_ProducesACompositeNode_WithOneChildEdge_UsingTheRegisteredJoin()
    {
        var (metadata, joins, customer, account, _, _) = BuildBankingGraph();
        var planner = new QueryPlanner(metadata, joins);

        var plan = planner.Plan(QueryIntent.Linear(customer.EntityId, new[] { account.EntityId }));

        var composite = Assert.IsType<CompositeNode>(plan.Root);
        Assert.Same(customer, composite.Entity);
        var edge = Assert.Single(composite.Children);
        Assert.Same(account, edge.Child.Entity);
        Assert.Empty(edge.Child.Children);
        Assert.True(joins.TryGetJoin(customer.EntityId, account.EntityId, out var expected));
        Assert.Equal(expected, edge.Join);
    }

    [Fact]
    public void Plan_CustomerToAccountToTransaction_ProducesANestedCompositeChain()
    {
        // This is the exact "🔴 NOW" checklist scenario: Customer -> Account
        // -> Transaction, planned dynamically rather than hand-assembled.
        //
        // TECH-DEBT-001: the plan now preserves this as a nested
        // CompositeNode tree rather than flattening it into a JoinNode
        // chain at planning time — flattening is SqlPlanCompiler's job.
        var (metadata, joins, customer, account, transaction, _) = BuildBankingGraph();
        var planner = new QueryPlanner(metadata, joins);

        var plan = planner.Plan(QueryIntent.Linear(customer.EntityId, new[] { account.EntityId, transaction.EntityId }));

        var customerNode = Assert.IsType<CompositeNode>(plan.Root);
        Assert.Same(customer, customerNode.Entity);

        var toAccount = Assert.Single(customerNode.Children);
        Assert.Same(account, toAccount.Child.Entity);
        Assert.True(joins.TryGetJoin(customer.EntityId, account.EntityId, out var expectedCustomerToAccount));
        Assert.Equal(expectedCustomerToAccount, toAccount.Join);

        var toTransaction = Assert.Single(toAccount.Child.Children);
        Assert.Same(transaction, toTransaction.Child.Entity);
        Assert.Empty(toTransaction.Child.Children);
        Assert.True(joins.TryGetJoin(account.EntityId, transaction.EntityId, out var expectedAccountToTransaction));
        Assert.Equal(expectedAccountToTransaction, toTransaction.Join);
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
        Assert.IsType<CompositeNode>(projection.Source);
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
    // rather than only a single linear path. Since TECH-DEBT-001,
    // QueryPlan.Root preserves that exact tree shape as a CompositeNode
    // instead of flattening it — so these tests assert on the tree
    // directly, rather than walking a flattened join chain.
    // ------------------------------------------------------------------

    [Fact]
    public void Plan_CustomerToAccountsAndContactPoints_ProducesTwoSiblingChildEdges()
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

        var composite = Assert.IsType<CompositeNode>(plan.Root);
        Assert.Same(customer, composite.Entity);
        Assert.Equal(2, composite.Children.Count);

        var toAccount = composite.Children[0];
        Assert.Same(account, toAccount.Child.Entity);
        Assert.Empty(toAccount.Child.Children);
        Assert.True(joins.TryGetJoin(customer.EntityId, account.EntityId, out var expectedAccountJoin));
        Assert.Equal(expectedAccountJoin, toAccount.Join);

        var toContactPoint = composite.Children[1];
        Assert.Same(contactPoint, toContactPoint.Child.Entity);
        Assert.Empty(toContactPoint.Child.Children);
        Assert.True(joins.TryGetJoin(customer.EntityId, contactPoint.EntityId, out var expectedContactJoin));
        Assert.Equal(expectedContactJoin, toContactPoint.Join);
    }

    [Fact]
    public void Plan_CustomerToAccountsToTransactionsAndContactPoints_PreservesTheFullTreeShape()
    {
        // The exact FOUND-002 example, and the exact TECH-DEBT-001 concern:
        //
        //   Customer
        //   ├── Accounts
        //   │    └── Transactions
        //   └── ContactPoints
        //
        // must come back out of the planner still shaped like that tree,
        // not as (((Customer JOIN Account) JOIN Transaction) JOIN ContactPoint).
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

        var composite = Assert.IsType<CompositeNode>(plan.Root);
        Assert.Same(customer, composite.Entity);
        Assert.Equal(2, composite.Children.Count);

        // Accounts branch: Customer -> Account -> Transaction, nested two
        // levels deep, exactly matching the requested shape.
        var accountEdge = composite.Children[0];
        Assert.Same(account, accountEdge.Child.Entity);
        var transactionEdge = Assert.Single(accountEdge.Child.Children);
        Assert.Same(transaction, transactionEdge.Child.Entity);
        Assert.Empty(transactionEdge.Child.Children);

        // ContactPoints branch: a direct sibling of Accounts under
        // Customer, not nested underneath it.
        var contactPointEdge = composite.Children[1];
        Assert.Same(contactPoint, contactPointEdge.Child.Entity);
        Assert.Empty(contactPointEdge.Child.Children);
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
}