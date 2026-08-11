using Foundgine.Execution.Contracts;
using Foundgine.Metadata;
using Xunit;

namespace Foundgine.Providers.Tests;

public class SqlTextTranslatorTests
{
    private static EntityMetadata Entity(ushort id, string name, params string[] columns) =>
        new(new EntityId(id), name,
            columns.Select((c, i) => new ColumnMetadata(new ColumnId((ushort)(i + 1)), c)).ToArray());

    [Fact]
    public void Translate_SingleScan_SelectsEveryColumn_FromOneAliasedTable()
    {
        var customer = Entity(1, "Customer", "Id", "Name");
        var plan = new ProviderPlan(new SqlScanNode(customer));

        var translation = SqlTextTranslator.Translate(plan);

        Assert.Equal(
            "SELECT t0.\"Id\" AS \"t0_Id\", t0.\"Name\" AS \"t0_Name\" FROM \"Customer\" AS t0",
            translation.CommandText);
        Assert.Equal(2, translation.Columns.Count);
        Assert.All(translation.Columns, c => Assert.Same(customer, c.Entity));
    }

    [Fact]
    public void Translate_Join_ProducesInnerJoin_OnTheConditionColumns()
    {
        var customer = Entity(1, "Customer", "Id", "Name");
        var account = Entity(2, "Account", "Id", "CustomerId");
        var join = new JoinMetadata(
            new JoinCondition(new ColumnReference(account, 2), new ColumnReference(customer, 1)),
            JoinKind.Inner);
        var plan = new ProviderPlan(new SqlJoinNode(new SqlScanNode(customer), new SqlScanNode(account), join));

        var translation = SqlTextTranslator.Translate(plan);

        Assert.Contains(
            "\"Customer\" AS t0 INNER JOIN \"Account\" AS t1 ON t1.\"CustomerId\" = t0.\"Id\"",
            translation.CommandText);
        Assert.Equal(4, translation.Columns.Count); // Customer.Id, Customer.Name, Account.Id, Account.CustomerId
    }

    [Fact]
    public void Translate_LeftJoin_UsesLeftJoinKeyword()
    {
        var customer = Entity(1, "Customer", "Id");
        var account = Entity(2, "Account", "Id", "CustomerId");
        var join = new JoinMetadata(
            new JoinCondition(new ColumnReference(account, 2), new ColumnReference(customer, 1)),
            JoinKind.Left);
        var plan = new ProviderPlan(new SqlJoinNode(new SqlScanNode(customer), new SqlScanNode(account), join));

        var translation = SqlTextTranslator.Translate(plan);

        Assert.Contains("LEFT JOIN", translation.CommandText);
    }

    [Fact]
    public void Translate_WithProjection_SelectsOnlyTheProjectedColumns()
    {
        var customer = Entity(1, "Customer", "Id", "Name");
        var fields = new[] { new FieldBinding(new ColumnReference(customer, 2), 1) };
        var plan = new ProviderPlan(new SqlProjectionNode(new SqlScanNode(customer), fields));

        var translation = SqlTextTranslator.Translate(plan);

        var column = Assert.Single(translation.Columns);
        Assert.Same(customer, column.Entity);
        Assert.Equal((ushort)2, column.ColumnId);
        Assert.Contains("t0_Name", translation.CommandText);
        Assert.DoesNotContain("t0_Id", translation.CommandText);
    }

    [Fact]
    public void Translate_SelfJoinChain_GivesEachOccurrenceItsOwnAlias()
    {
        // The review's own example: Employee -> Manager -> Manager, where
        // every occurrence is the *same* EntityMetadata. Before this fix,
        // aliasByEntity was keyed by EntityMetadata, so the second and
        // third scans would silently overwrite the first occurrence's
        // alias entry and the ON clauses would resolve to the wrong table.
        var employee = Entity(1, "Employee", "Id", "Name", "ManagerId");
        var managesUp = new JoinMetadata(
            new JoinCondition(new ColumnReference(employee, 3), new ColumnReference(employee, 1)),
            JoinKind.Left);

        // Built the same way SqlPlanCompiler.CompileComposite would: each
        // occurrence gets its own SqlScanNode instance, and each join
        // carries explicit LeftOccurrence/RightOccurrence references to
        // exactly which occurrence it binds — not just "Employee".
        var mia = new SqlScanNode(employee);
        var vic = new SqlScanNode(employee);
        var carol = new SqlScanNode(employee);
        var miaToVic = new SqlJoinNode(mia, vic, managesUp, mia, vic);
        var plan = new ProviderPlan(new SqlJoinNode(miaToVic, carol, managesUp, vic, carol));

        var translation = SqlTextTranslator.Translate(plan);

        // Three distinct occurrences of the same entity must get three
        // distinct aliases, in scan order.
        Assert.Contains("\"Employee\" AS t0", translation.CommandText);
        Assert.Contains("\"Employee\" AS t1", translation.CommandText);
        Assert.Contains("\"Employee\" AS t2", translation.CommandText);

        // The two ON clauses must bind to the correct, distinct pair of
        // occurrences each — t0/t1 for Mia->Vic, t1/t2 for Vic->Carol —
        // never both resolving against the same alias.
        Assert.Contains("ON t0.\"ManagerId\" = t1.\"Id\"", translation.CommandText);
        Assert.Contains("ON t1.\"ManagerId\" = t2.\"Id\"", translation.CommandText);

        // 3 occurrences x 3 columns each = 9 selected columns, none merged
        // together the way an entity-keyed dictionary would have.
        Assert.Equal(9, translation.Columns.Count);
    }

    [Fact]
    public void Translate_HandBuiltJoin_WithoutOccurrenceReferences_StillFallsBackToEntityTypeLookup()
    {
        // A JoinNode built by hand (bypassing CompositeNode/SqlPlanCompiler
        // entirely, as this whole test file already does elsewhere) has no
        // LeftOccurrence/RightOccurrence set. As long as it doesn't itself
        // scan the same entity twice, the entity-type fallback still
        // resolves it correctly — this fix doesn't regress that path.
        var customer = Entity(1, "Customer", "Id");
        var account = Entity(2, "Account", "Id", "CustomerId");
        var join = new JoinMetadata(
            new JoinCondition(new ColumnReference(account, 2), new ColumnReference(customer, 1)), JoinKind.Inner);
        var plan = new ProviderPlan(new SqlJoinNode(new SqlScanNode(customer), new SqlScanNode(account), join));

        var translation = SqlTextTranslator.Translate(plan);

        Assert.Contains("ON t1.\"CustomerId\" = t0.\"Id\"", translation.CommandText);
    }

    [Fact]
    public void Translate_ThreeWayJoin_RegistersAllThreeEntities()
    {
        var customer = Entity(1, "Customer", "Id");
        var account = Entity(2, "Account", "Id", "CustomerId");
        var transaction = Entity(3, "Transaction", "Id", "AccountId", "Amount");
        var customerToAccount = new JoinMetadata(
            new JoinCondition(new ColumnReference(account, 2), new ColumnReference(customer, 1)), JoinKind.Inner);
        var accountToTransaction = new JoinMetadata(
            new JoinCondition(new ColumnReference(transaction, 2), new ColumnReference(account, 1)), JoinKind.Inner);

        var plan = new ProviderPlan(
            new SqlJoinNode(
                new SqlJoinNode(new SqlScanNode(customer), new SqlScanNode(account), customerToAccount),
                new SqlScanNode(transaction),
                accountToTransaction));

        var translation = SqlTextTranslator.Translate(plan);

        Assert.Equal(1 + 2 + 3, translation.Columns.Count);
        Assert.Contains("t0", translation.CommandText);
        Assert.Contains("t1", translation.CommandText);
        Assert.Contains("t2", translation.CommandText);
    }

    [Fact]
    public void Translate_WithStorageNames_EmitsThePhysicalTableAndColumnNames_NotTheDomainOnes()
    {
        // The architecture review's "ugly physical schema" checkpoint, at
        // the translator unit level: EntityMetadata/ColumnMetadata.Name
        // stay the domain-facing identity, StorageName is what actually
        // ends up in the generated SQL text.
        var customer = new EntityMetadata(
            new EntityId(1), "Customer",
            new ColumnMetadata[]
            {
                new(new ColumnId(1), "Id", StorageName: "customer_pk"),
                new(new ColumnId(2), "Name", StorageName: "full_name"),
            },
            StorageName: "crm_customer");

        var plan = new ProviderPlan(new SqlScanNode(customer));

        var translation = SqlTextTranslator.Translate(plan);

        Assert.Equal(
            "SELECT t0.\"customer_pk\" AS \"t0_Id\", t0.\"full_name\" AS \"t0_Name\" FROM \"crm_customer\" AS t0",
            translation.CommandText);

        // No domain name leaked into the physical identifiers.
        Assert.DoesNotContain("\"Customer\"", translation.CommandText);
        Assert.DoesNotContain("\"Id\"", translation.CommandText);
        Assert.DoesNotContain("\"Name\"", translation.CommandText);

        // But the domain name is still what SqlColumnMap reports back —
        // downstream code (SqlExecutionProvider) never needs to know the
        // physical names either.
        Assert.All(translation.Columns, c => Assert.Same(customer, c.Entity));
        Assert.Contains(translation.Columns, c => c.ResultAlias == "t0_Id");
        Assert.Contains(translation.Columns, c => c.ResultAlias == "t0_Name");
    }
}