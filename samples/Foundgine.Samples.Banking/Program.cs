using Foundgine.Builders;
using Foundgine.Execution.Contracts;
using Foundgine.Providers;
using Foundgine.Samples.Banking.Metadata;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

// ---------------------------------------------------------------------
// This is the demo the architecture review asked for: proof that
// Foundgine is a platform Graphgine happens to be built on, not just
// Graphgine wearing a different name. Nothing below references GraphQL,
// HotChocolate, or any Graphgine.* project — check
// Foundgine.Samples.Banking.csproj if you don't believe it.
//
//     Domain -> Metadata -> QueryPlan -> ProviderPlan -> Execution
//
// ---------------------------------------------------------------------

Console.WriteLine("Foundgine Banking sample (no GraphQL, no Graphgine)");
Console.WriteLine("====================================================");
Console.WriteLine();

// 1) Domain -> Metadata
//    BankingMetadata.cs hand-describes Customer/Account as
//    Foundgine.Metadata records. See that file for why this is written
//    by hand rather than generated.
Console.WriteLine($"Entities: {BankingMetadata.Customer.Name}, {BankingMetadata.Account.Name}");
Console.WriteLine($"Join:     {BankingMetadata.Account.Name}.CustomerId -> {BankingMetadata.Customer.Name}.Id ({BankingMetadata.AccountToCustomer.Kind})");
Console.WriteLine();

// 2) Metadata -> logical, provider-agnostic QueryPlan (Foundgine.Builders)
//    "Every account, joined to its customer" — describes WHAT is needed,
//    not which database or storage engine answers it.
var logicalPlan = new QueryPlan(
    new JoinNode(
        Left: new ScanNode(BankingMetadata.Customer),
        Right: new ScanNode(BankingMetadata.Account),
        Join: BankingMetadata.AccountToCustomer));

Console.WriteLine($"Logical plan (Foundgine.Builders.QueryPlan): {Describe(logicalPlan.Root)}");
Console.WriteLine();

// 3) Logical plan -> physical ProviderPlan (Foundgine.Execution.Contracts)
//    There's no optimizer/provider-planner yet to derive this
//    automatically from the QueryPlan above — see item 5 of the
//    architecture review, on Execution.Contracts' relationship to
//    Metadata — so this sample mirrors the logical tree by hand,
//    node-for-node, choosing the SQL provider's node types.
var providerPlan = new ProviderPlan(
    new SqlJoinNode(
        Left: new SqlScanNode(BankingMetadata.Customer),
        Right: new SqlScanNode(BankingMetadata.Account),
        Join: BankingMetadata.AccountToCustomer));

Console.WriteLine($"Physical plan (Foundgine.Execution.Contracts.ProviderPlan): {DescribeProvider(providerPlan.Root)}");
Console.WriteLine();

// 4) Physical plan -> execution
IExecutionProvider provider = new SqlExecutionProvider();
var context = new ExecutionContext(Guid.NewGuid(), new Dictionary<string, object?>());

Console.WriteLine($"Executing via {provider.GetType().Name} (Foundgine.Providers)...");
try
{
    await foreach (var row in provider.ExecuteAsync(providerPlan, context))
    {
        Console.WriteLine($"  row: {row}");
    }
}
catch (NotSupportedException ex)
{
    Console.WriteLine();
    Console.WriteLine("Execution stops here, and that's expected:");
    Console.WriteLine($"  {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("Everything above this point is real, working Foundgine: domain,");
    Console.WriteLine("hand-written metadata, a logical query plan, and a physical");
    Console.WriteLine("provider plan. Only the last mile — a provider that actually runs");
    Console.WriteLine("a ProviderPlan against a database — isn't implemented yet. That's");
    Console.WriteLine("the SQL provider milestone tracked in the root README, and it's the");
    Console.WriteLine("only thing standing between this sample and a real result set.");
}

static string Describe(QueryNode node) => node switch
{
    ScanNode s => $"Scan({s.Entity.Name})",
    JoinNode j => $"Join({Describe(j.Left)}, {Describe(j.Right)}, {j.Join.Kind})",
    GraphEdgeNode g => $"GraphEdge({g.Graph.GraphName})",
    ProjectionNode p => $"Project({Describe(p.Source)})",
    MaterializeNode m => $"Materialize({Describe(m.Source)} -> {m.Model.Name})",
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
