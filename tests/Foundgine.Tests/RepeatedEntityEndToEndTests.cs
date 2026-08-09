using Foundgine.Builders;
using Foundgine.Execution.Contracts;
using Foundgine.Metadata;
using Foundgine.Planning;
using Foundgine.Providers;
using Microsoft.Data.Sqlite;
using Xunit;
using ExecutionContext = Foundgine.Execution.Contracts.ExecutionContext;

namespace Foundgine.Tests;

/// <summary>
/// The smallest possible repeated-entity/self-join case: the same
/// Employee table joined to itself twice,
///
///   Employee (Alice)
///     -> Manager (Bob, same table)
///          -> Manager's Manager (Carol, same table again)
///
/// via one self-referencing JoinGraph edge
/// (Employee.ManagerId = Employee.Id) applied twice.
///
/// This is a genuine self-join, not just "the same entity type twice in
/// one plan": every level scans the same EntityMetadata instance, so
/// nothing upstream of SQL generation (QueryIntent, QueryPlanner,
/// QueryPlan) can tell the three occurrences apart by entity identity
/// alone — only SqlPlanCompiler's per-occurrence SqlScanNode instances
/// and SqlTextTranslator's reference-identity alias tracking can.
///
/// Two separate things are checked here:
///
/// 1. Does the SQL layer generate a correct, unambiguous self-join —
///    i.e. does exactly the row we expect (Alice/Bob/Carol) come back,
///    and not some other spurious combination? Yes.
///
/// 2. Once that row comes back, can the caller actually read Alice's,
///    Bob's, and Carol's columns as three distinct values? Yes:
///    ExecutionRow now carries EntityOccurrence values, each with an
///    EntityId and an OccurrenceIndex. The same EntityId can therefore
///    legitimately occur three times in one ExecutionRow.
///
/// This test validates both halves of the repeated-entity contract:
/// SQL alias resolution distinguishes the occurrences during execution,
/// and ExecutionRow preserves those occurrences after execution.
/// </summary>
public sealed class RepeatedEntityEndToEndTests : IAsyncLifetime
{
    private readonly string _connectionString =
        $"Data Source=file:{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    private SqliteConnection _keeper = null!;

    private static readonly EntityMetadata Employee = new(
        new EntityId(1),
        "Employee",
        new ColumnMetadata[]
        {
            new(new ColumnId(1), "Id"),
            new(new ColumnId(2), "Name"),
            new(new ColumnId(3), "ManagerId")
        });

    /// <summary>
    /// Employee.ManagerId = Employee.Id — registered once as a self-loop
    /// edge in <see cref="JoinGraph"/> (from Employee to Employee) and
    /// reused for both levels of the chain below.
    ///
    /// Left/Right follow the convention <see cref="SqlTextTranslator"/>
    /// needs for the ambiguous (both-occurrences-match) case:
    /// Left is the column on the parent occurrence in the composite tree
    /// (the employee), Right is the column on the child occurrence
    /// (their manager).
    /// </summary>
    private static readonly JoinMetadata EmployeeToManager = new(
        new JoinCondition(
            Left: new ColumnReference(Employee, ColumnId: 3),
            Right: new ColumnReference(Employee, ColumnId: 1)),
        JoinKind.Inner);

    public async Task InitializeAsync()
    {
        _keeper = new SqliteConnection(_connectionString);
        await _keeper.OpenAsync();

        var setup = _keeper.CreateCommand();
        setup.CommandText =
            """
            CREATE TABLE Employee (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                ManagerId INTEGER NULL
            );

            -- Carol is the top of the chain: no manager.
            INSERT INTO Employee (Id, Name, ManagerId)
            VALUES (3, 'Carol', NULL);

            -- Bob reports to Carol.
            INSERT INTO Employee (Id, Name, ManagerId)
            VALUES (2, 'Bob', 3);

            -- Alice reports to Bob.
            INSERT INTO Employee (Id, Name, ManagerId)
            VALUES (1, 'Alice', 2);
            """;

        await setup.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() =>
        await _keeper.DisposeAsync();

    private static EntityOccurrence Occurrence(
        ExecutionRow row,
        EntityId entityId,
        int occurrenceIndex)
    {
        return Assert.Single(
            row.Occurrences.Where(x =>
                x.EntityId == entityId &&
                x.OccurrenceIndex == occurrenceIndex));
    }

[Fact]
public async Task Employee_ManagerOfManager_SelfJoin_ProducesTheOneCorrectChain()
{
    // 1) Domain -> Metadata
    var registry = new MetadataRegistry();
    registry.Register(Employee);

    var joinGraph = new JoinGraph();

    joinGraph.AddEdge(
        Employee.EntityId,
        Employee.EntityId,
        EmployeeToManager);

    // 2) Metadata + intent -> logical plan
    var planner = new QueryPlanner(registry, joinGraph);

    var intent = QueryIntent.Linear(
        root: Employee.EntityId,
        path: new[]
        {
            Employee.EntityId,
            Employee.EntityId
        });

    var queryPlan = planner.Plan(intent);

    Assert.IsType<CompositeNode>(queryPlan.Root);

    // 3) Logical -> SQL provider plan
    var providerPlan = SqlPlanCompiler.Compile(queryPlan);

    Assert.IsType<SqlJoinNode>(providerPlan.Root);

    // 4) Translate BEFORE executing.
    //
    // This is important: if the result is empty, the problem is SQL
    // generation/planning, not ExecutionRow.
    var translation = SqlTextTranslator.Translate(providerPlan);

    Assert.False(
        string.IsNullOrWhiteSpace(translation.CommandText),
        "The SQL translator produced an empty SQL statement.");

    // The self-join MUST have three independent Employee occurrences.
    Assert.Contains(
        "\"Employee\" AS t0",
        translation.CommandText);

    Assert.Contains(
        "\"Employee\" AS t1",
        translation.CommandText);

    Assert.Contains(
        "\"Employee\" AS t2",
        translation.CommandText);

    // The relationship is:
    //
    // Employee.ManagerId -> Employee.Id
    //
    // Therefore the generated SQL MUST be:
    //
    // t0.ManagerId = t1.Id
    // t1.ManagerId = t2.Id
    Assert.Contains(
        "t0.\"ManagerId\" = t1.\"Id\"",
        translation.CommandText);

    Assert.Contains(
        "t1.\"ManagerId\" = t2.\"Id\"",
        translation.CommandText);

    // Three Employee occurrences x three columns.
    Assert.Equal(
        9,
        translation.Columns.Count);

    // 5) Execute against the real database.
    var provider = new SqlExecutionProvider();

    var context = new ExecutionContext(
        Guid.NewGuid(),
        new Dictionary<string, object?>
        {
            ["ConnectionString"] = _connectionString
        });

    var rows = new List<ExecutionRow>();

    await foreach (
        var row in provider.ExecuteAsync(
            providerPlan,
            context))
    {
        rows.Add(row);
    }

    // Exactly one chain exists:
    //
    // Alice -> Bob -> Carol
    //
    // Alice.ManagerId = 2
    // Bob.Id          = 2
    //
    // Bob.ManagerId   = 3
    // Carol.Id        = 3
    var rowAssert = Assert.Single(
        rows);

    var alice = Occurrence(
        rowAssert,
        Employee.EntityId,
        occurrenceIndex: 0);

    var bob = Occurrence(
        rowAssert,
        Employee.EntityId,
        occurrenceIndex: 1);

    var carol = Occurrence(
        rowAssert,
        Employee.EntityId,
        occurrenceIndex: 2);

    Assert.Equal(
        "Alice",
        alice.Values[1]);

    Assert.Equal(
        2L,
        alice.Values[2]);

    Assert.Equal(
        "Bob",
        bob.Values[1]);

    Assert.Equal(
        3L,
        bob.Values[2]);

    Assert.Equal(
        "Carol",
        carol.Values[1]);

    Assert.Null(
        carol.Values[2]);
}
}