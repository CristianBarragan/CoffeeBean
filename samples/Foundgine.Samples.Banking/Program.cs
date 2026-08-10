using Foundgine.Execution.Contracts;
using Foundgine.Metadata;
using Foundgine.Planning;
using Foundgine.Providers;
using Foundgine.Samples.Banking.Metadata;
using Foundgine.Samples.Banking.Resolution;
using Foundgine.Samples.Banking.Semantic;
using Foundgine.Semantic;
using Foundgine.Semantic.Intent;
using Foundgine.Semantic.Resolution;
using Microsoft.Data.Sqlite;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

// ---------------------------------------------------------------------
// Milestone 4: "Find Ada Lovelace's last five transactions", driven
// end-to-end through a single ReadIntent, against a real database.
//
//   ReadIntent -> ReadPlanner+EntityResolver -> ResolvedReadPlan
//              -> (bridge) -> QueryIntent (Filter/Sort/Page)
//              -> QueryPlanner -> QueryPlan -> SqlPlanCompiler
//              -> ProviderPlan -> SQL -> real SQLite -> ExecutionRow
//              -> ExecutionEvidence
//
// Nothing below references GraphQL, HotChocolate, or any Graphgine
// project — check Foundgine.Samples.Banking.csproj if you don't believe it.
// ---------------------------------------------------------------------

Console.WriteLine("Foundgine Banking sample — Milestone 4");
Console.WriteLine("Find Ada Lovelace's last five transactions");
Console.WriteLine("==============================================================");
Console.WriteLine();

// 1) Domain -> Metadata
var registry = BankingMetadata.Registry;
var joins = BankingMetadata.Joins;

Console.WriteLine(
    $"Entities: {BankingMetadata.Customer.Name}, " +
    $"{BankingMetadata.Account.Name}, " +
    $"{BankingMetadata.Transaction.Name}");

Console.WriteLine(
    $"Joins:    {BankingMetadata.Account.Name}.CustomerId -> " +
    $"{BankingMetadata.Customer.Name}.Id");

Console.WriteLine(
    $"          {BankingMetadata.Transaction.Name}.AccountId -> " +
    $"{BankingMetadata.Account.Name}.Id");

Console.WriteLine();

// 2) Metadata -> Semantic model
var semanticModel = BankingSemanticModel.Build();

Console.WriteLine("Semantic model (Foundgine.Semantic):");
Console.WriteLine();

foreach (var entity in semanticModel.Entities)
{
    foreach (var line in SemanticModelPrinter.Describe(entity).Split(Environment.NewLine))
    {
        Console.WriteLine($"  {line}");
    }

    Console.WriteLine();
}

// 3) Set up a real in-memory SQLite database, seeded with a real
//    TransactionDate on every row -- without it, "last five" has no
//    honest meaning, it would just be insertion/Id order.
var connectionString =
    $"Data Source=file:{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

await using var keeper = new SqliteConnection(connectionString);

await keeper.OpenAsync();

var setup = keeper.CreateCommand();

setup.CommandText =
    """
    CREATE TABLE Customer
    (
        Id INTEGER PRIMARY KEY,
        Name TEXT NOT NULL
    );

    CREATE TABLE Account
    (
        Id INTEGER PRIMARY KEY,
        CustomerId INTEGER NOT NULL,
        Balance REAL NOT NULL
    );

    CREATE TABLE "Transaction"
    (
        Id INTEGER PRIMARY KEY,
        AccountId INTEGER NOT NULL,
        Amount REAL NOT NULL,
        TransactionDate TEXT NOT NULL
    );

    INSERT INTO Customer (Id, Name) VALUES (1, 'Ada Lovelace');
    INSERT INTO Customer (Id, Name) VALUES (2, 'Grace Hopper');

    INSERT INTO Account (Id, CustomerId, Balance) VALUES (10, 1, 500.0);
    INSERT INTO Account (Id, CustomerId, Balance) VALUES (20, 2, 1000.0);

    -- Ada has 7 transactions spread across different dates, so "last
    -- five" actually exercises ORDER BY + LIMIT rather than trivially
    -- returning everything she has.
    INSERT INTO "Transaction" (Id, AccountId, Amount, TransactionDate) VALUES (100, 10, -25.50, '2026-08-01');
    INSERT INTO "Transaction" (Id, AccountId, Amount, TransactionDate) VALUES (101, 10,  60.00, '2026-08-02');
    INSERT INTO "Transaction" (Id, AccountId, Amount, TransactionDate) VALUES (102, 10, -10.00, '2026-08-03');
    INSERT INTO "Transaction" (Id, AccountId, Amount, TransactionDate) VALUES (103, 10,  15.00, '2026-08-04');
    INSERT INTO "Transaction" (Id, AccountId, Amount, TransactionDate) VALUES (104, 10,  -5.00, '2026-08-05');
    INSERT INTO "Transaction" (Id, AccountId, Amount, TransactionDate) VALUES (105, 10, 200.00, '2026-08-06');
    INSERT INTO "Transaction" (Id, AccountId, Amount, TransactionDate) VALUES (106, 10, -50.00, '2026-08-07');

    -- Grace's transactions exist only to prove the resolved filter
    -- excludes her entirely, not just that she ranks below the limit.
    INSERT INTO "Transaction" (Id, AccountId, Amount, TransactionDate) VALUES (200, 20, 10.00, '2026-08-06');
    INSERT INTO "Transaction" (Id, AccountId, Amount, TransactionDate) VALUES (201, 20, 20.00, '2026-08-07');
    """;

await setup.ExecuteNonQueryAsync();

// 4) Structured intent -- what an LLM or any other intent extractor would
//    hand Foundgine for "Find Ada Lovelace's last five transactions."
//    Nothing in this program parses that sentence; that boundary is
//    exactly the point (Foundgine owns everything from here down).
var readIntent = new ReadIntent(
    AnchorEntity: BankingMetadata.Customer.EntityId,
    AnchorPhrase: "Ada Lovelace",
    ThroughRelationships: ["Accounts"],
    TargetRelationship: "Transactions",
    OrderBy: new FieldId(4), // Transaction.TransactionDate
    Descending: true,
    Limit: 5);

Console.WriteLine("ReadIntent (Foundgine.Semantic.Intent):");
Console.WriteLine(
    $"  anchor=\"{readIntent.AnchorPhrase}\" through=[{string.Join(", ", readIntent.ThroughRelationships)}] " +
    $"target={readIntent.TargetRelationship} orderBy=TransactionDate desc={readIntent.Descending} limit={readIntent.Limit}");
Console.WriteLine();

// 5) ReadIntent -> ResolvedReadPlan, via EntityResolver walking the
//    anchor phrase and each relationship hop against the *same* SQLite
//    database step 8 executes against -- no fakes.
var candidates = new SqlCandidateSource(connectionString, semanticModel, registry, joins);
var resolver = new EntityResolver(semanticModel, candidates);
var readPlanner = new ReadPlanner(semanticModel, resolver);

var readPlanResult = readPlanner.Plan(readIntent);

if (!readPlanResult.IsResolved)
{
    Console.WriteLine($"ReadIntent did not resolve: {readPlanResult.UnresolvedReason}");
    foreach (var evidence in readPlanResult.Evidence)
        Console.WriteLine($"  evidence: {evidence.Description}");

    return;
}

var readPlan = readPlanResult.Plan!;
var anchor = readPlan.AnchorChain[^1]; // Ada's resolved Account (after the "Accounts" hop)

Console.WriteLine("ResolvedReadPlan (Foundgine.Semantic.Intent):");
foreach (var step in readPlan.AnchorChain)
    Console.WriteLine($"  resolved {semanticModel.Get(step.EntityType).Name}#{step.IdentityValue} ({step.Reason})");
Console.WriteLine($"  target: {semanticModel.Get(readPlan.TargetEntity).Name} via relationship {readPlan.TargetRelationship}");
Console.WriteLine();

// 6) ResolvedReadPlan -> QueryIntent: the bridge itself. The resolved
//    anchor's literal identity becomes a WHERE-clause filter; the
//    intent's ordering/limit pass straight through to Sort/Page. Nothing
//    here hardcodes "Account.CustomerId" or "Transaction.Id" reasoning
//    beyond what both sides already expressed in Foundgine.Metadata terms.
var queryIntent = QueryIntent.Linear(
    root: anchor.EntityType,
    path: [readPlan.TargetEntity],
    filter: new ComparisonFilter(
        new ColumnReference(BankingMetadata.Account, 1),
        ComparisonOperator.Equal,
        anchor.IdentityValue),
    sort:
    [
        new SortTerm(
            new ColumnReference(BankingMetadata.Transaction, 4), // TransactionDate
            readPlan.Descending ? SortDirection.Descending : SortDirection.Ascending)
    ],
    page: new PageSpec(Limit: readPlan.Limit));

// 7) QueryIntent -> QueryPlan -> ProviderPlan -> SQL.
var planner = new QueryPlanner(registry, joins);
var queryPlan = planner.Plan(queryIntent);
var providerPlan = SqlPlanCompiler.Compile(queryPlan);
var translation = SqlTextTranslator.Translate(providerPlan);

Console.WriteLine("Generated SQL (Foundgine.Providers.SqlTextTranslator):");
Console.WriteLine($"  {translation.CommandText}");
if (translation.Parameters.Count > 0)
{
    Console.WriteLine(
        "  params: " +
        string.Join(", ", translation.Parameters.Select(p => $"{p.Name}={p.Value}")));
}
Console.WriteLine();

// 8) ProviderPlan -> execution against the real database.
IExecutionProvider provider = new SqlExecutionProvider();

var context = new ExecutionContext(
    Guid.NewGuid(),
    new Dictionary<string, object?> { ["ConnectionString"] = connectionString });

Console.WriteLine($"Executing via {provider.GetType().Name} (Foundgine.Providers)...");
Console.WriteLine();

var rows = new List<ExecutionRow>();

await foreach (var row in provider.ExecuteAsync(providerPlan, context))
{
    rows.Add(row);

    var transactionRow = row.Single(BankingMetadata.Transaction.EntityId);

    Console.WriteLine(
        $"  transaction #{transactionRow[0]}: {transactionRow[2]:C} on {transactionRow[3]}");
}

Console.WriteLine();

// 9) Capture & print ExecutionEvidence: resolution log + resolved intent
//    + generated SQL plan + actual database row output, combined into a
//    single audit object.
var evidenceExecution = ExecutionEvidence.Build(readPlan, translation, provider.Kind, rows, BankingMetadata.Transaction.EntityId);

Console.WriteLine("ExecutionEvidence:");
Console.WriteLine("  Resolution:");
foreach (var line in evidenceExecution.Resolution)
    Console.WriteLine($"    - {line}");
Console.WriteLine($"  Plan: {evidenceExecution.Plan}");
Console.WriteLine($"  Execution: {evidenceExecution.Execution}");
Console.WriteLine("  Result:");
foreach (var line in evidenceExecution.Result)
    Console.WriteLine($"    - {line}");

Console.WriteLine();
Console.WriteLine(
    rows.Count == 5
        ? "Milestone 4: PASSED — 5 records returned from SQLite via a single resolved ReadIntent."
        : $"Milestone 4: FAILED — expected 5 records, got {rows.Count}.");

// ---------------------------------------------------------------------
// ExecutionEvidence: one printed audit object answering four questions —
// who did we resolve and why, what plan did we generate, what provider
// executed it, what came back. Deliberately small; see ReadEvidence in
// Foundgine.Tests for the same shape proven under test.
// ---------------------------------------------------------------------
internal sealed record ExecutionEvidence(
    IReadOnlyList<string> Resolution,
    string Plan,
    string Execution,
    IReadOnlyList<string> Result)
{
    public static ExecutionEvidence Build(
        ResolvedReadPlan readPlan,
        SqlTranslation translation,
        ProviderKind providerKind,
        IReadOnlyList<ExecutionRow> rows,
        EntityId resultEntity)
    {
        var resolution = readPlan.Evidence.Select(e => e.Description).ToArray();

        var plan = translation.Parameters.Count == 0
            ? translation.CommandText
            : translation.CommandText + " -- params: " +
              string.Join(", ", translation.Parameters.Select(p => $"{p.Name}={p.Value}"));

        var execution = $"{providerKind} provider, {rows.Count} row(s) returned";

        var result = rows
            .Select(row => string.Join(", ", row.Single(resultEntity)))
            .ToArray();

        return new ExecutionEvidence(resolution, plan, execution, result);
    }
}
