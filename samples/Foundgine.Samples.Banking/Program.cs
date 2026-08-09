using Foundgine.Execution.Contracts;
using Foundgine.Planning;
using Foundgine.Providers;
using Foundgine.Samples.Banking.Metadata;
using Microsoft.Data.Sqlite;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

// ---------------------------------------------------------------------
// The first Banking E2E: Customer -> Account -> Transaction, driven
// end-to-end through the real pipeline, against a real database.
//
//   Domain -> Metadata -> Dynamic Planner -> QueryPlan
//          -> ProviderPlan -> SQL -> real database -> Result
//
// Nothing below references GraphQL, HotChocolate, or any Graphgine.*
// project — check Foundgine.Samples.Banking.csproj if you don't believe it.
// ---------------------------------------------------------------------

Console.WriteLine("Foundgine Banking sample: Customer -> Account -> Transaction");
Console.WriteLine("==============================================================");
Console.WriteLine();

// 1) Domain -> Metadata
//    BankingMetadata.cs hand-describes Customer/Account/Transaction as
//    Foundgine.Metadata records, plus the joins between them.
var registry = BankingMetadata.Registry;
var joins = BankingMetadata.Joins;

Console.WriteLine($"Entities: {BankingMetadata.Customer.Name}, {BankingMetadata.Account.Name}, {BankingMetadata.Transaction.Name}");
Console.WriteLine($"Joins:    {BankingMetadata.Account.Name}.CustomerId -> {BankingMetadata.Customer.Name}.Id");
Console.WriteLine($"          {BankingMetadata.Transaction.Name}.AccountId -> {BankingMetadata.Account.Name}.Id");
Console.WriteLine();

// 2) Metadata + Intent -> logical QueryPlan (Foundgine.Planning.QueryPlanner)
//    "Customer, joined to Account, joined to Transaction" — the planner
//    discovers both joins from the JoinGraph. It contains no
//    Banking-specific code at all.
var intent = QueryIntent.Linear(
    root: BankingMetadata.Customer.EntityId,
    path: new[] { BankingMetadata.Account.EntityId, BankingMetadata.Transaction.EntityId });

var planner = new QueryPlanner(registry, joins);
var logicalPlan = planner.Plan(intent);

Console.WriteLine($"Logical plan (Foundgine.Builders.QueryPlan): {Describe(logicalPlan.Root)}");
Console.WriteLine();

// 3) Logical plan -> physical ProviderPlan (Foundgine.Providers.SqlPlanCompiler)
var providerPlan = SqlPlanCompiler.Compile(logicalPlan);
Console.WriteLine($"Physical plan (Foundgine.Execution.Contracts.ProviderPlan): {DescribeProvider(providerPlan.Root)}");
Console.WriteLine();

// 4) Set up a real (in-memory, shared-cache) SQLite database and seed it.
//    This is the "Real Database" step of the pipeline — no mocks.
var connectionString = $"Data Source=file:{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
await using var keeper = new SqliteConnection(connectionString);
await keeper.OpenAsync();

var setup = keeper.CreateCommand();
setup.CommandText =
    """
    CREATE TABLE Customer (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);
    CREATE TABLE Account (Id INTEGER PRIMARY KEY, CustomerId INTEGER NOT NULL, Balance REAL NOT NULL);
    CREATE TABLE "Transaction" (Id INTEGER PRIMARY KEY, AccountId INTEGER NOT NULL, Amount REAL NOT NULL);

    INSERT INTO Customer (Id, Name) VALUES (1, 'Ada Lovelace');
    INSERT INTO Account (Id, CustomerId, Balance) VALUES (10, 1, 500.0);
    INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (100, 10, -25.50);
    INSERT INTO "Transaction" (Id, AccountId, Amount) VALUES (101, 10, 60.00);
    """;
await setup.ExecuteNonQueryAsync();

// 5) Physical plan -> execution, against that real database.
IExecutionProvider provider = new SqlExecutionProvider();
var context = new ExecutionContext(
    Guid.NewGuid(),
    new Dictionary<string, object?> { ["ConnectionString"] = connectionString });

Console.WriteLine($"Executing via {provider.GetType().Name} (Foundgine.Providers)...");
Console.WriteLine();

await foreach (var row in provider.ExecuteAsync(providerPlan, context))
{
    var customerRow = row.Entities[BankingMetadata.Customer.EntityId.Value];
    var accountRow = row.Entities[BankingMetadata.Account.EntityId.Value];
    var transactionRow = row.Entities[BankingMetadata.Transaction.EntityId.Value];

    Console.WriteLine(
        $"  {customerRow[1]} | account #{accountRow[0]} (balance {accountRow[2]:C}) " +
        $"| transaction #{transactionRow[0]}: {transactionRow[2]:C}");
}

Console.WriteLine();
Console.WriteLine("Done: Domain -> Metadata -> Dynamic Planner -> QueryPlan -> ProviderPlan -> SQL -> real database -> Result.");

static string Describe(Foundgine.Builders.QueryNode node) => node switch
{
    Foundgine.Builders.ScanNode s => $"Scan({s.Entity.Name})",
    Foundgine.Builders.JoinNode j => $"Join({Describe(j.Left)}, {Describe(j.Right)}, {j.Join.Kind})",
    Foundgine.Builders.CompositeNode c => c.Children.Count == 0
        ? $"Scan({c.Entity.Name})"
        : $"Composite({c.Entity.Name} -> [{string.Join(", ", c.Children.Select(e => $"{e.Join.Kind}:{Describe(e.Child)}"))}])",
    Foundgine.Builders.GraphEdgeNode g => $"GraphEdge({g.Graph.GraphName})",
    Foundgine.Builders.ProjectionNode p => $"Project({Describe(p.Source)})",
    Foundgine.Builders.MaterializeNode m => $"Materialize({Describe(m.Source)} -> {m.Model.Name})",
    _ => node.GetType().Name,
};

static string DescribeProvider(ProviderNode node) => node switch
{
    SqlScanNode s => $"SqlScan({s.Entity.Name})",
    SqlJoinNode j => $"SqlJoin({DescribeProvider(j.Left)}, {DescribeProvider(j.Right)}, {j.Join.Kind})",
    SqlProjectionNode p => $"SqlProjection({DescribeProvider(p.Source)})",
    GraphTraversalNode g => $"GraphTraversal({g.Graph.GraphName})",
    CacheLookupNode c => $"CacheLookup({c.Entity.Name})",
    _ => node.GetType().Name,
};