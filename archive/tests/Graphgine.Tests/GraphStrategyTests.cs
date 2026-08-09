using System.Collections.Immutable;
using System.Text;
using Graphgine.Execution;
using Graphgine.Sql;
using Xunit;

namespace Graphgine.Tests;

public class GraphStrategyTests
{
    // AppendGraphJoin/BuildGraphMerge don't touch IEntityMetaProvider, so a
    // stub that throws if actually used is enough to construct the strategy.
    private sealed class UnusedEntityMetaProvider : IEntityMetaProvider
    {
        public int Count => throw new NotImplementedException();
        public string[][] ModelName => throw new NotImplementedException();
        public ushort[][] FieldToColumn => throw new NotImplementedException();
        public FieldMapSpec[][] FieldMappings => throw new NotImplementedException();
        public string[][] Table => throw new NotImplementedException();
        public string[][] Schema => throw new NotImplementedException();
        public string[][] ColumnName => throw new NotImplementedException();
        public string[][] FieldName => throw new NotImplementedException();
        public ConflictColumn[][] EntityConflictColumns => throw new NotImplementedException();
        public CteResolutionSpec[][] CteResolutions => throw new NotImplementedException();
        public int StorageEntityCount => throw new NotImplementedException();
        public string[] EntitySchema => throw new NotImplementedException();
        public string[] EntityTable => throw new NotImplementedException();
        public string[][] EntityColumnName => throw new NotImplementedException();
        public bool TryGetEntityId(string modelName, out ushort entityId) => throw new NotImplementedException();
    }

    [Fact]
    public void ApacheAge_AppendGraphJoin_EmitsCypherMatchingFromToLabelsAndEdge()
    {
        var strategy = new ApacheAgeGraphStrategy(new UnusedEntityMetaProvider());
        var spec = new GraphJoinSpec(
            entityId: 1,
            storageEntityId: 1,
            graphName: "banking",
            edgeLabel: "OWNS",
            edgeKeyColumn: "edge_id",
            fromLabel: "Customer",
            fromGraphProperty: "id",
            fromAlias: "c",
            fromJoinColumn: "id",
            toLabel: "Account",
            toGraphProperty: "id",
            toAlias: "a",
            toJoinColumn: "id",
            joinAlias: "j");
        var sb = new StringBuilder();

        strategy.AppendGraphJoin(sb, spec, primaryOutputAlias: "root");

        var sql = sb.ToString();
        Assert.Contains("cypher(", sql);
        Assert.Contains("'banking'", sql);
        Assert.Contains("MATCH (a:Customer)-[r:OWNS]->(b:Account)", sql);
        Assert.Contains("\"j\"", sql);
    }

    [Fact]
    public void ApacheAge_AppendGraphJoin_OmitsEdgeColumns_WhenNoEdgeKeyColumn()
    {
        var strategy = new ApacheAgeGraphStrategy(new UnusedEntityMetaProvider());
        var spec = new GraphJoinSpec(
            entityId: 1, storageEntityId: 1,
            graphName: "g", edgeLabel: "REL", edgeKeyColumn: "",
            fromLabel: "A", fromGraphProperty: "id", fromAlias: "a", fromJoinColumn: "id",
            toLabel: "B", toGraphProperty: "id", toAlias: "b", toJoinColumn: "id",
            joinAlias: "j");
        var sb = new StringBuilder();

        strategy.AppendGraphJoin(sb, spec, primaryOutputAlias: "root");

        Assert.DoesNotContain("agtype\n", sb.ToString());
    }

    [Fact]
    public void ApacheAge_BuildGraphMerge_EmitsMergeForBothVerticesAndEdge()
    {
        var strategy = new ApacheAgeGraphStrategy(new UnusedEntityMetaProvider());
        var spec = new GraphMergeSpec(
            graphName: "banking",
            edgeLabel: "OWNS",
            fromLabel: "Customer",
            fromKeyColumn: "id",
            fromKeyValue: "cust-1",
            toLabel: "Account",
            toKeyColumn: "id",
            toKeyValue: "acct-1",
            edgeKeyColumn: "edge_id",
            edgeKeyValue: null,
            edgeProperties: ImmutableDictionary<string, string>.Empty);

        var cte = strategy.BuildGraphMerge(0, spec);

        Assert.Contains("merge_0 AS", cte);
        Assert.Contains("MERGE (a:Customer { id: 'cust-1' })", cte);
        Assert.Contains("MERGE (b:Account { id: 'acct-1' })", cte);
        Assert.Contains("MERGE (a)-[r:OWNS]->(b)", cte);
    }

    [Fact]
    public void ApacheAge_BuildGraphMerge_EscapesSingleQuotesInKeyValues()
    {
        var strategy = new ApacheAgeGraphStrategy(new UnusedEntityMetaProvider());
        var spec = new GraphMergeSpec(
            graphName: "g", edgeLabel: "REL",
            fromLabel: "A", fromKeyColumn: "id", fromKeyValue: "o'brien",
            toLabel: "B", toKeyColumn: "id", toKeyValue: "acct-1",
            edgeKeyColumn: "edge_id", edgeKeyValue: null,
            edgeProperties: ImmutableDictionary<string, string>.Empty);

        var cte = strategy.BuildGraphMerge(1, spec);

        Assert.Contains(@"o\'brien", cte);
        Assert.DoesNotContain("id: 'o'brien'", cte);
    }

    [Fact]
    public void ApacheAge_BuildGraphMerge_AddsSetClause_WhenEdgePropertiesPresent()
    {
        var strategy = new ApacheAgeGraphStrategy(new UnusedEntityMetaProvider());
        var props = ImmutableDictionary<string, string>.Empty.Add("weight", "5");
        var spec = new GraphMergeSpec(
            graphName: "g", edgeLabel: "REL",
            fromLabel: "A", fromKeyColumn: "id", fromKeyValue: "a1",
            toLabel: "B", toKeyColumn: "id", toKeyValue: "b1",
            edgeKeyColumn: "edge_id", edgeKeyValue: "e1",
            edgeProperties: props);

        var cte = strategy.BuildGraphMerge(0, spec);

        Assert.Contains("SET ", cte);
        Assert.Contains("r.edge_id = 'e1'", cte);
        Assert.Contains("r.weight = '5'", cte);
    }

    [Theory]
    [InlineData(nameof(IGraphStrategy.AppendGraphJoin))]
    [InlineData(nameof(IGraphStrategy.AppendGraphResultJoin))]
    [InlineData(nameof(IGraphStrategy.BuildGraphMerge))]
    public void RecursiveCte_AllMembers_ThrowNotImplemented(string memberName)
    {
        // Documents the current state: RecursiveCteGraphStrategy is a stub
        // pending the relational edge-table schema (see its own comments).
        IGraphStrategy strategy = new RecursiveCteGraphStrategy();
        var sb = new StringBuilder();

        Action act = memberName switch
        {
            nameof(IGraphStrategy.AppendGraphJoin) => () =>
                strategy.AppendGraphJoin(sb, default, "root"),
            nameof(IGraphStrategy.AppendGraphResultJoin) => () =>
                strategy.AppendGraphResultJoin(sb, default),
            nameof(IGraphStrategy.BuildGraphMerge) => () =>
                strategy.BuildGraphMerge(0, default),
            _ => throw new ArgumentOutOfRangeException(nameof(memberName)),
        };

        Assert.Throws<NotImplementedException>(act);
    }
}
