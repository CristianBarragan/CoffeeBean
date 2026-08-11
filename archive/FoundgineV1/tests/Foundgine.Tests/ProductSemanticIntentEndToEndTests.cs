using Foundgine.Builders;
using Foundgine.Execution.Contracts;
using Foundgine.Metadata;
using Foundgine.Planning;
using Foundgine.Providers;
using Foundgine.Semantic;
using Foundgine.Semantic.Intent;
using Foundgine.Semantic.Resolution;
using Microsoft.Data.Sqlite;
using Xunit;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

namespace Foundgine.Tests;

/// <summary>
/// Combines two things that had each only been proven separately:
///
/// <list type="bullet">
/// <item><description><see cref="ProductCompositeEndToEndTests"/> proved the full five-entity <c>Product</c> composite (<c>Customer -&gt; CustomerBankingRelationship -&gt; Contract -&gt; Account -&gt; Transaction</c>) plans, compiles, and executes.</description></item>
/// <item><description><see cref="ReadIntentEndToEndTests"/> proved the <see cref="ReadIntent"/> -&gt; <see cref="ReadPlanner"/>/<see cref="EntityResolver"/> -&gt; <see cref="QueryIntent"/> bridge, but only on a three-entity Banking domain.</description></item>
/// </list>
///
/// This proves the bridge on the real depth of the domain -- "Find Ada's
/// five most recent transactions" walking the full chain, plus the domain
/// tree the roadmap actually cares about:
///
/// <code>
/// Product
/// ├── Customer
/// ├── CustomerBankingRelationship
/// ├── Account
/// ├── Contract
/// └── Transaction
/// </code>
///
/// end to end:
///
/// <code>
/// Semantic intent
///         ↓
/// Resolved Customer
///         ↓
/// Composite logical model
///         ↓
/// Dynamic join graph
///         ↓
/// Filter
///         ↓
/// Order
///         ↓
/// Limit
///         ↓
/// SQL
///         ↓
/// SQLite
///         ↓
/// Result
/// </code>
///
/// <see cref="Customer"/> also carries a self-referencing
/// <c>ReferredByCustomerId</c> column -- the repeated-entity stress test
/// the architecture review called out. Per that review's own advice ("make
/// the test pass using the general metadata/planning machinery; don't
/// build special support prematurely"), <see cref="WhoReferredAda_SelfJoin_ResolvesThroughTheSameRegisteredJoinGraph"/>
/// reuses the exact same <see cref="MetadataRegistry"/>/<see cref="JoinGraph"/>
/// every other test in this file uses -- nothing here special-cases
/// self-joins.
/// </summary>
public sealed class ProductSemanticIntentEndToEndTests : IAsyncLifetime
{
    private readonly string _connectionString =
        $"Data Source=file:{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private SqliteConnection _keeper = null!;

    private static readonly EntityMetadata Customer = new(
        new EntityId(1),
        "Customer",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "Name"),
            new(new ColumnId(3), "ReferredByCustomerId"),
        });

    private static readonly EntityMetadata CustomerBankingRelationship = new(
        new EntityId(2),
        "CustomerBankingRelationship",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "CustomerId"),
            new(new ColumnId(3), "RelationshipType"),
        });

    private static readonly EntityMetadata Contract = new(
        new EntityId(3),
        "Contract",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "CustomerBankingRelationshipId"),
            new(new ColumnId(3), "ContractType"),
            new(new ColumnId(4), "Amount"),
        });

    private static readonly EntityMetadata Account = new(
        new EntityId(4),
        "Account",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "ContractId"),
            new(new ColumnId(3), "AccountNumber"),
            new(new ColumnId(4), "Balance"),
        });

    private static readonly EntityMetadata TransactionEntity = new(
        new EntityId(5),
        "Transaction",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "AccountId"),
            new(new ColumnId(3), "Amount"),
        });

    /// <summary>CustomerBankingRelationship.CustomerId = Customer.Id</summary>
    private static readonly JoinMetadata RelationshipToCustomer = new(
        new JoinCondition(
            new ColumnReference(CustomerBankingRelationship, 2),
            new ColumnReference(Customer, 1)),
        JoinKind.Inner);

    /// <summary>Contract.CustomerBankingRelationshipId = CustomerBankingRelationship.Id</summary>
    private static readonly JoinMetadata ContractToRelationship = new(
        new JoinCondition(
            new ColumnReference(Contract, 2),
            new ColumnReference(CustomerBankingRelationship, 1)),
        JoinKind.Inner);

    /// <summary>Account.ContractId = Contract.Id</summary>
    private static readonly JoinMetadata AccountToContract = new(
        new JoinCondition(
            new ColumnReference(Account, 2),
            new ColumnReference(Contract, 1)),
        JoinKind.Inner);

    /// <summary>Transaction.AccountId = Account.Id</summary>
    private static readonly JoinMetadata TransactionToAccount = new(
        new JoinCondition(
            new ColumnReference(TransactionEntity, 2),
            new ColumnReference(Account, 1)),
        JoinKind.Inner);

    /// <summary>
    /// Customer.ReferredByCustomerId = Customer.Id -- a genuine self-loop,
    /// same shape as <c>RepeatedEntityEndToEndTests</c>' Employee/Manager
    /// edge: Left is the referred customer's own column (the parent
    /// occurrence in the tree), Right is the referrer's Id (the child
    /// occurrence).
    /// </summary>
    private static readonly JoinMetadata CustomerReferredBy = new(
        new JoinCondition(
            new ColumnReference(Customer, 3),
            new ColumnReference(Customer, 1)),
        JoinKind.Inner);

    // Semantic-layer identities, deliberately independent of the
    // Foundgine.Metadata EntityId/ColumnId numbering above (see
    // ReadIntentEndToEndTests for the same convention).
    private static readonly FieldId CustomerNameField = new(2);
    private static readonly FieldId TransactionIdField = new(1);
    private static readonly RelationshipId CustomerBankingRelationships = new(1);
    private static readonly RelationshipId RelationshipContracts = new(2);
    private static readonly RelationshipId ContractAccount = new(3);
    private static readonly RelationshipId AccountTransactions = new(4);
    private static readonly RelationshipId CustomerReferredByRelationship = new(5);

    private static SemanticModel BuildSemanticModel() =>
        new SemanticModelBuilder()
            .Entity(Customer.EntityId, "Customer", c => c
                .Identity(new FieldId(1), "Id")
                .Field(CustomerNameField, "Name", typeof(string))
                .Relationship(
                    CustomerBankingRelationships, "BankingRelationships",
                    CustomerBankingRelationship.EntityId, RelationshipCardinality.Many)
                .Relationship(
                    CustomerReferredByRelationship, "ReferredBy",
                    Customer.EntityId, RelationshipCardinality.One)
                .Search(new SearchCapability([CustomerNameField], SearchStrategy.Fuzzy)))
            .Entity(CustomerBankingRelationship.EntityId, "CustomerBankingRelationship", r => r
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "RelationshipType", typeof(string))
                .Relationship(RelationshipContracts, "Contracts", Contract.EntityId, RelationshipCardinality.Many))
            .Entity(Contract.EntityId, "Contract", c => c
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "ContractType", typeof(string))
                .Field(new FieldId(4), "Amount", typeof(decimal))
                .Relationship(ContractAccount, "Account", Account.EntityId, RelationshipCardinality.Many))
            .Entity(Account.EntityId, "Account", a => a
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "AccountNumber", typeof(string))
                .Field(new FieldId(4), "Balance", typeof(decimal))
                .Relationship(AccountTransactions, "Transactions", TransactionEntity.EntityId, RelationshipCardinality.Many))
            .Entity(TransactionEntity.EntityId, "Transaction", t => t
                .Identity(new FieldId(1), "Id")
                .Field(new FieldId(3), "Amount", typeof(decimal)))
            .Build();

    /// <summary>
    /// The real <see cref="ICandidateSource"/> for this scenario: targeted,
    /// parameterized SQL against the same database the final query runs
    /// against -- one lookup per relationship this scenario's chain
    /// actually walks, following <see cref="ReadIntentEndToEndTests.TestCandidateSource"/>'s
    /// convention exactly.
    /// </summary>
    private sealed class TestCandidateSource(string connectionString) : ICandidateSource
    {
        public IReadOnlyList<IdentityCandidate> FindByIdentity(EntityId entityType, string identityValue) =>
            throw new NotSupportedException(
                $"{nameof(TestCandidateSource)} only supports the lookups this scenario needs " +
                "(search + relationship traversal) -- not identity lookup.");

        public IReadOnlyList<IdentityCandidate> FindByField(
            EntityId entityType, FieldId fieldId, string text, SearchStrategy strategy)
        {
            if (entityType != Customer.EntityId)
            {
                throw new NotSupportedException(
                    $"{nameof(TestCandidateSource)} only searches Customer, not entity " +
                    $"{entityType.Value}.");
            }

            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name FROM Customer WHERE Name LIKE @pattern";
            command.Parameters.AddWithValue("@pattern", "%" + text + "%");

            return Read(command);
        }

        public IReadOnlyList<IdentityCandidate> FindByRelationship(
            RelationshipId relationshipId, string sourceIdentityValue)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();

            if (relationshipId == CustomerBankingRelationships)
                command.CommandText = "SELECT Id, Id FROM CustomerBankingRelationship WHERE CustomerId = @id";
            else if (relationshipId == RelationshipContracts)
                command.CommandText = "SELECT Id, Id FROM Contract WHERE CustomerBankingRelationshipId = @id";
            else if (relationshipId == ContractAccount)
                command.CommandText = "SELECT Id, Id FROM Account WHERE ContractId = @id";
            else if (relationshipId == AccountTransactions)
                command.CommandText = "SELECT Id, Id FROM \"Transaction\" WHERE AccountId = @id";
            else if (relationshipId == CustomerReferredByRelationship)
            {
                command.CommandText =
                    "SELECT ReferredByCustomerId, ReferredByCustomerId FROM Customer " +
                    "WHERE Id = @id AND ReferredByCustomerId IS NOT NULL";
            }
            else
                throw new NotSupportedException($"Unknown relationship {relationshipId.Value}.");

            command.Parameters.AddWithValue("@id", sourceIdentityValue);

            return Read(command);
        }

        private SqliteConnection Open()
        {
            var connection = new SqliteConnection(connectionString);
            connection.Open();
            return connection;
        }

        private static IReadOnlyList<IdentityCandidate> Read(SqliteCommand command)
        {
            var results = new List<IdentityCandidate>();
            using var reader = command.ExecuteReader();

            while (reader.Read())
                results.Add(new IdentityCandidate(reader.GetValue(0).ToString()!, reader.GetValue(1).ToString()!));

            return results;
        }
    }

    public async Task InitializeAsync()
    {
        _keeper = new SqliteConnection(_connectionString);
        await _keeper.OpenAsync();

        var setup = _keeper.CreateCommand();
        setup.CommandText =
            """
            CREATE TABLE Customer (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, ReferredByCustomerId INTEGER NULL);
            CREATE TABLE CustomerBankingRelationship (Id INTEGER PRIMARY KEY, CustomerId INTEGER NOT NULL, RelationshipType TEXT NOT NULL);
            CREATE TABLE Contract (Id INTEGER PRIMARY KEY, CustomerBankingRelationshipId INTEGER NOT NULL, ContractType TEXT NOT NULL, Amount REAL NOT NULL);
            CREATE TABLE Account (Id INTEGER PRIMARY KEY, ContractId INTEGER NOT NULL, AccountNumber TEXT NOT NULL, Balance REAL NOT NULL);
            CREATE TABLE "Transaction" (Id INTEGER PRIMARY KEY, AccountId INTEGER NOT NULL, Amount REAL NOT NULL);

            -- Grace exists first so she can refer Ada.
            INSERT INTO Customer (Id, Name, ReferredByCustomerId) VALUES (2, 'Grace Hopper', NULL);
            INSERT INTO Customer (Id, Name, ReferredByCustomerId) VALUES (1, 'Ada Lovelace', 2);

            -- Ada: one banking relationship, one mortgage contract, one
            -- account, seven transactions -- "last five" exercises LIMIT.
            INSERT INTO CustomerBankingRelationship (Id, CustomerId, RelationshipType) VALUES (10, 1, 'Primary');
            INSERT INTO Contract (Id, CustomerBankingRelationshipId, ContractType, Amount) VALUES (100, 10, 'Mortgage', 250000.0);
            INSERT INTO Account (Id, ContractId, AccountNumber, Balance) VALUES (1000, 100, 'ACC-1000', 500.0);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (10000, 1000, -25.5);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (10001, 1000, 60.0);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (10002, 1000, -10.0);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (10003, 1000, 15.0);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (10004, 1000, -5.0);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (10005, 1000, 200.0);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (10006, 1000, -50.0);

            -- Grace: distractor customer -- her account/transactions must
            -- never appear in Ada's result.
            INSERT INTO CustomerBankingRelationship (Id, CustomerId, RelationshipType) VALUES (20, 2, 'Primary');
            INSERT INTO Contract (Id, CustomerBankingRelationshipId, ContractType, Amount) VALUES (200, 20, 'CreditCard', 5000.0);
            INSERT INTO Account (Id, ContractId, AccountNumber, Balance) VALUES (2000, 200, 'ACC-2000', 1000.0);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (20000, 2000, 10.0);
            INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (20001, 2000, 20.0);
            """;

        await setup.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() =>
        await _keeper.DisposeAsync();

    /// <summary>
    /// The single registry/join-graph every test in this file plans
    /// against -- all five Product entities plus the Customer self-loop,
    /// built once exactly as a real application would at startup. No test
    /// below builds a narrower graph for its own scenario.
    /// </summary>
    private static (MetadataRegistry Registry, JoinGraph Joins) BuildDomain()
    {
        var registry = new MetadataRegistry();
        registry.Register(Customer);
        registry.Register(CustomerBankingRelationship);
        registry.Register(Contract);
        registry.Register(Account);
        registry.Register(TransactionEntity);

        var joinGraph = new JoinGraph();
        joinGraph.AddEdge(Customer.EntityId, CustomerBankingRelationship.EntityId, RelationshipToCustomer);
        joinGraph.AddEdge(CustomerBankingRelationship.EntityId, Contract.EntityId, ContractToRelationship);
        joinGraph.AddEdge(Contract.EntityId, Account.EntityId, AccountToContract);
        joinGraph.AddEdge(Account.EntityId, TransactionEntity.EntityId, TransactionToAccount);
        joinGraph.AddEdge(Customer.EntityId, Customer.EntityId, CustomerReferredBy);

        return (registry, joinGraph);
    }

    [Fact]
    public async Task FindAdasFiveMostRecentTransactions_ThroughTheFullProductChain_ToRealDatabase()
    {
        // 1) Semantic intent -- what an LLM or any other intent extractor
        //    would hand Foundgine for "Find Ada's five most recent
        //    transactions." Nothing here parses that sentence.
        var readIntent = new ReadIntent(
            AnchorEntity: Customer.EntityId,
            AnchorPhrase: "Ada Lovelace",
            ThroughRelationships: ["BankingRelationships", "Contracts", "Account"],
            TargetRelationship: "Transactions",
            OrderBy: TransactionIdField,
            Descending: true,
            Limit: 5);

        // 2) Semantic intent -> Resolved Customer -- EntityResolver walks
        //    Customer -> BankingRelationship -> Contract -> Account, each
        //    hop narrowing to a single instance, against the same real
        //    database step 5 executes against.
        var semanticModel = BuildSemanticModel();
        var resolver = new EntityResolver(semanticModel, new TestCandidateSource(_connectionString));
        var readPlanResult = new ReadPlanner(semanticModel, resolver).Plan(readIntent);

        Assert.True(readPlanResult.IsResolved, readPlanResult.UnresolvedReason);
        var readPlan = readPlanResult.Plan!;

        Assert.Equal(Customer.EntityId, readPlan.AnchorChain[0].EntityType);
        Assert.Equal("1", readPlan.AnchorChain[0].IdentityValue); // Customer#1 = Ada
        Assert.Equal(CustomerBankingRelationship.EntityId, readPlan.AnchorChain[1].EntityType);
        Assert.Equal("10", readPlan.AnchorChain[1].IdentityValue);
        Assert.Equal(Contract.EntityId, readPlan.AnchorChain[2].EntityType);
        Assert.Equal("100", readPlan.AnchorChain[2].IdentityValue);
        Assert.Equal(Account.EntityId, readPlan.AnchorChain[3].EntityType);
        Assert.Equal("1000", readPlan.AnchorChain[3].IdentityValue);
        Assert.Equal(TransactionEntity.EntityId, readPlan.TargetEntity);

        // 3) Resolved Customer -> Composite logical model -> Dynamic join
        //    graph: the bridge itself. The resolved Account's literal
        //    identity becomes a WHERE-clause filter; the intent's
        //    ordering/limit pass straight through to Sort/Page.
        var anchor = readPlan.AnchorChain[^1];

        var queryIntent = QueryIntent.Linear(
            root: anchor.EntityType,
            path: [readPlan.TargetEntity],
            filter: new ComparisonFilter(
                new ColumnReference(Account, 1),
                ComparisonOperator.Equal,
                anchor.IdentityValue),
            sort: [new SortTerm(
                new ColumnReference(TransactionEntity, 1),
                readPlan.Descending ? SortDirection.Descending : SortDirection.Ascending)],
            page: new PageSpec(Limit: readPlan.Limit));

        // 4) Filter / Order / Limit -> SQL, via the exact same
        //    QueryPlanner/SqlPlanCompiler every other test in this file
        //    plans against -- the domain's full registry and join graph,
        //    not a scenario-scoped subset.
        var (registry, joinGraph) = BuildDomain();
        var providerPlan = SqlPlanCompiler.Compile(new QueryPlanner(registry, joinGraph).Plan(queryIntent));

        // 5) SQL -> SQLite -> Result.
        var provider = new SqlExecutionProvider();
        var context = new ExecutionContext(
            Guid.NewGuid(),
            new Dictionary<string, object?> { ["ConnectionString"] = _connectionString });

        var rows = new List<ExecutionRow>();
        await foreach (var row in provider.ExecuteAsync(providerPlan, context))
            rows.Add(row);

        // Exactly Ada's five most recent transactions, newest first --
        // proving the filter excluded Grace's account entirely and the
        // sort + limit both actually ran in SQL, not in this test.
        Assert.Equal(5, rows.Count);

        var transactionIds = rows
            .Select(r => (long)r.Single(TransactionEntity.EntityId)[0]!)
            .ToArray();

        Assert.Equal(new long[] { 10006, 10005, 10004, 10003, 10002 }, transactionIds);

        Assert.All(rows, row =>
            Assert.Equal(1000L, (long)row.Single(Account.EntityId)[0]!));
    }

    [Fact]
    public async Task FindAdasFiveMostRecentTransactions_ProducesEvidence_AcrossTheFullChain()
    {
        // Same pipeline as the test above, capturing evidence at each
        // stage instead of only asserting on the final rows.
        var readIntent = new ReadIntent(
            Customer.EntityId,
            "Ada Lovelace",
            ["BankingRelationships", "Contracts", "Account"],
            "Transactions",
            OrderBy: TransactionIdField,
            Descending: true,
            Limit: 5);

        var semanticModel = BuildSemanticModel();
        var resolver = new EntityResolver(semanticModel, new TestCandidateSource(_connectionString));
        var readPlanResult = new ReadPlanner(semanticModel, resolver).Plan(readIntent);

        Assert.True(readPlanResult.IsResolved, readPlanResult.UnresolvedReason);
        var readPlan = readPlanResult.Plan!;
        var anchor = readPlan.AnchorChain[^1];

        var queryIntent = QueryIntent.Linear(
            root: anchor.EntityType,
            path: [readPlan.TargetEntity],
            filter: new ComparisonFilter(new ColumnReference(Account, 1), ComparisonOperator.Equal, anchor.IdentityValue),
            sort: [new SortTerm(new ColumnReference(TransactionEntity, 1), SortDirection.Descending)],
            page: new PageSpec(Limit: readPlan.Limit));

        var (registry, joinGraph) = BuildDomain();
        var providerPlan = SqlPlanCompiler.Compile(new QueryPlanner(registry, joinGraph).Plan(queryIntent));
        var translation = SqlTextTranslator.Translate(providerPlan);

        var provider = new SqlExecutionProvider();
        var context = new ExecutionContext(
            Guid.NewGuid(),
            new Dictionary<string, object?> { ["ConnectionString"] = _connectionString });

        var rows = new List<ExecutionRow>();
        await foreach (var row in provider.ExecuteAsync(providerPlan, context))
            rows.Add(row);

        var evidence = ReadEvidence.Build(readPlan, translation, provider.Kind, rows, TransactionEntity.EntityId);

        // Why did Foundgine produce this query -- who did we resolve, and
        // through which relationships, all four hops of it.
        Assert.Contains(evidence.Resolution, e => e.Contains("Ada Lovelace"));
        Assert.Contains(evidence.Resolution, e => e.Contains("BankingRelationships"));
        Assert.Contains(evidence.Resolution, e => e.Contains("Contracts"));
        Assert.Contains(evidence.Resolution, e => e.Contains("Account"));

        Assert.Contains("SELECT", evidence.Plan);
        Assert.Contains("WHERE", evidence.Plan);
        Assert.Contains("ORDER BY", evidence.Plan);
        Assert.Contains("LIMIT", evidence.Plan);
        Assert.Contains("1000", evidence.Plan); // the resolved account id, bound as a parameter

        Assert.Contains("Sql", evidence.Execution);
        Assert.Equal(5, rows.Count);
        Assert.Equal(5, evidence.Result.Count);
    }

    [Fact]
    public async Task WhoReferredAda_SelfJoin_ResolvesThroughTheSameRegisteredJoinGraph()
    {
        // The repeated-entity stress test the architecture review called
        // out -- proven with no special-casing: this is the exact same
        // (registry, joinGraph) BuildDomain() hands every other test in
        // this file, which already has four unrelated entities and joins
        // registered in it. The general QueryPlanner/SqlPlanCompiler
        // machinery either handles a Customer -> Customer self-loop
        // sitting alongside all of that, or it doesn't -- nothing here
        // gives it help.
        var (registry, joinGraph) = BuildDomain();

        var intent = QueryIntent.Linear(
            root: Customer.EntityId,
            path: [Customer.EntityId]);

        var queryPlan = new QueryPlanner(registry, joinGraph).Plan(intent);
        Assert.IsType<CompositeNode>(queryPlan.Root);

        var providerPlan = SqlPlanCompiler.Compile(queryPlan);
        Assert.IsType<SqlJoinNode>(providerPlan.Root);

        var provider = new SqlExecutionProvider();
        var context = new ExecutionContext(
            Guid.NewGuid(),
            new Dictionary<string, object?> { ["ConnectionString"] = _connectionString });

        var rows = new List<ExecutionRow>();
        await foreach (var row in provider.ExecuteAsync(providerPlan, context))
            rows.Add(row);

        // Only Ada has a referrer, so exactly one row comes back: Ada
        // (occurrence 0) referred by Grace (occurrence 1).
        var rowAssert = Assert.Single(rows);

        var referred = Assert.Single(rowAssert.Occurrences.Where(o => o.OccurrenceIndex == 0));
        var referrer = Assert.Single(rowAssert.Occurrences.Where(o => o.OccurrenceIndex == 1));

        Assert.Equal("Ada Lovelace", referred.Values[1]);
        Assert.Equal("Grace Hopper", referrer.Values[1]);
    }
}