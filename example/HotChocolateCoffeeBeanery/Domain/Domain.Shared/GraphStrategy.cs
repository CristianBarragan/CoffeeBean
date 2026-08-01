using System.Collections.Immutable;
using System.Text;
using CoffeeBeanery.GraphQL.Core.Runtime;

namespace Domain.Shared;

/// <summary>
/// Strategy for graph traversal (query-side join) and graph mutation
/// (edge merge/upsert). Two independent concerns bundled together because
/// today's only implementation (Apache AGE) handles both via Cypher — a
/// future strategy backed by a relational edge table might implement both
/// very differently (a JOIN vs a CTE), or split into two interfaces if
/// that turns out cleaner once a second implementation actually exists.
/// </summary>
public interface IGraphStrategy
{
    /// <summary>
    /// Appends a join that resolves graph edges into tabular from/to key
    /// columns aliased as join.JoinAlias, joined back to primaryOutputAlias
    /// on join.EdgeKeyColumn.
    /// </summary>
    void AppendGraphJoin(StringBuilder sb, in GraphJoinSpec join, string primaryOutputAlias);

    /// <summary>
    /// Appends a join that resolves a column living on a graph subquery's
    /// output (rather than a stored ColumnId) — mirrors a normal join but
    /// for edge-derived columns.
    /// </summary>
    void AppendGraphResultJoin(StringBuilder sb, in GraphResultJoinSpec join);

    /// <summary>
    /// Produces the statement(s) that merge (upsert) a graph edge. Returns
    /// a single string; multi-statement strategies join their parts
    /// internally.
    /// </summary>
    string BuildGraphMerge(in GraphMergeSpec spec);
}

public sealed class ApacheAgeGraphStrategy : IGraphStrategy
{
    private readonly IEntityMetaProvider _meta;

    public ApacheAgeGraphStrategy(IEntityMetaProvider meta)
    {
        _meta = meta;
    }

    public void AppendGraphJoin(StringBuilder sb, in GraphJoinSpec join, string primaryOutputAlias)
    {
        var fromColAlias = join.FromAlias + join.FromJoinColumn;
        var toColAlias   = join.ToAlias   + join.ToJoinColumn;

        var edgeReturn = string.IsNullOrEmpty(join.EdgeKeyColumn) ? "" : $"r.{join.EdgeKeyColumn}";
        var edgeColumnDef = string.IsNullOrEmpty(join.EdgeKeyColumn) ? "" : $"{join.EdgeKeyColumn} agtype";
        var edgeSelect = string.IsNullOrEmpty(join.EdgeKeyColumn) ? "" : $"({join.EdgeKeyColumn})::text::uuid AS \"{join.EdgeKeyColumn}\"";

        sb.Append("LEFT JOIN (\n");
        sb.Append("    WITH graph_edges AS (\n");
        sb.Append("        SELECT DISTINCT * FROM cypher(\n");
        sb.Append($"            '{join.GraphName}',\n");
        sb.Append("            $$\n");
        sb.Append($"            MATCH (a:{join.FromLabel})-[r:{join.EdgeLabel}]->(b:{join.ToLabel})\n");
        sb.Append("            RETURN\n");
        sb.Append($"                a.{join.FromGraphProperty} AS from_key,\n");
        sb.Append($"                b.{join.ToGraphProperty} AS to_key");
        if (!string.IsNullOrEmpty(edgeReturn)) sb.Append($",\n                {edgeReturn}");
        sb.Append("\n            $$\n");
        sb.Append("        ) AS (\n");
        sb.Append("            from_key agtype,\n");
        sb.Append("            to_key agtype");
        if (!string.IsNullOrEmpty(edgeColumnDef)) sb.Append($",\n            {edgeColumnDef}");
        sb.Append("\n        )\n");
        sb.Append("    )\n");
        sb.Append("    SELECT\n");
        sb.Append($"        from_key::text::uuid AS \"{fromColAlias}\",\n");
        sb.Append($"        to_key::text::uuid AS \"{toColAlias}\"");
        if (!string.IsNullOrEmpty(edgeSelect)) sb.Append($",\n        {edgeSelect}");
        sb.Append("\n    FROM graph_edges\n");
        sb.Append(") ");
        PostgresSqlWriter.AppendQuotedIdentifierStatic(sb, join.JoinAlias);
        sb.Append(" ON ");
        PostgresSqlWriter.AppendQuotedIdentifierStatic(sb, join.JoinAlias);
        sb.Append(".\"").Append(join.EdgeKeyColumn).Append("\" = ");
        PostgresSqlWriter.AppendQuotedIdentifierStatic(sb, primaryOutputAlias);
        sb.Append(".\"").Append(join.EdgeKeyColumn).Append('"');
    }

    public void AppendGraphResultJoin(
        StringBuilder sb,
        in GraphResultJoinSpec join)
    {
        var keyword =
            join.Kind == JoinKind.Left
                ? "LEFT JOIN"
                : "JOIN";

        sb.Append(keyword)
            .Append(' ');

        sb.Append('"')
            .Append(_meta.EntitySchema[join.ToStorageEntityId])
            .Append("\".\"")
            .Append(_meta.EntityTable[join.ToStorageEntityId])
            .Append('"');

        sb.Append(' ');

        PostgresSqlWriter.AppendQuotedIdentifierStatic(
            sb,
            join.ToOutputAlias);

        sb.Append("\n    ON ");

        var toColumn =
            _meta.EntityColumnName
                    [join.ToStorageEntityId]
                [join.ToColumnId];

        PostgresSqlWriter.AppendQuotedIdentifierStatic(
            sb,
            join.ToOutputAlias);

        sb.Append(".\"")
            .Append(toColumn)
            .Append("\" = ");

        PostgresSqlWriter.AppendQuotedIdentifierStatic(
            sb,
            join.FromAlias);

        sb.Append(".\"")
            .Append(join.FromColumnName)
            .Append('"');
    }

    // public string BuildGraphMerge(in GraphMergeSpec spec)
    // {
    //     var setClause = BuildGraphSetClause(spec.EdgeKeyColumn, spec.EdgeKeyValue, spec.EdgeProperties);
    //     return $@"
    //             ;CREATE TEMP TABLE temp_merge AS SELECT 1 
    //             FROM ag_catalog.cypher(
    //                 '{spec.GraphName}',
    //                 $$
    //                 MERGE (a:{spec.FromLabel} {{ {spec.FromKeyColumn}: '{EscapeCypherValue(spec.FromKeyValue)}' }})
    //                 MERGE (b:{spec.ToLabel} {{ {spec.ToKeyColumn}: '{EscapeCypherValue(spec.ToKeyValue)}' }})
    //                 MERGE (a)-[r:{spec.EdgeLabel}]->(b)
    //                 {setClause}
    //                 RETURN r.{spec.EdgeLabel}::text
    //                 $$
    //             ) AS (r text); DROP TABLE temp_merge;
    //             ";
    // }
    
    public string BuildGraphMerge(in GraphMergeSpec spec)
    {
        var setClause =
            BuildGraphSetClause(
                spec.EdgeKeyColumn,
                spec.EdgeKeyValue,
                spec.EdgeProperties);

        return $@"
SELECT 1
FROM ag_catalog.cypher(
    '{spec.GraphName}',
    $$
    MERGE (a:{spec.FromLabel} {{ {spec.FromKeyColumn}: '{EscapeCypherValue(spec.FromKeyValue)}' }})
    MERGE (b:{spec.ToLabel} {{ {spec.ToKeyColumn}: '{EscapeCypherValue(spec.ToKeyValue)}' }})
    MERGE (a)-[r:{spec.EdgeLabel}]->(b)
    {setClause}
    RETURN r.{spec.EdgeLabel}::text
    $$
) AS (r text)";
    }

    private static string BuildGraphSetClause(
        string edgeKeyColumn,
        string? edgeKeyValue,
        ImmutableDictionary<string,string> edgeProperties)
    {
        var parts =
            new List<string>();


        if (!string.IsNullOrWhiteSpace(edgeKeyValue))
        {
            parts.Add(
                $"r.{edgeKeyColumn} = '{EscapeCypherValue(edgeKeyValue)}'");
        }


        foreach (var kvp in edgeProperties)
        {
            parts.Add(
                $"r.{kvp.Key} = '{EscapeCypherValue(kvp.Value)}'");
        }


        return parts.Count == 0
            ? string.Empty
            : "SET " + string.Join(", ", parts);
    }

    private static string EscapeCypherValue(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'");
}

/// <summary>
/// Placeholder for a relational-edge-table graph strategy. NOT a working
/// implementation — every method throws. Wiring depends on confirming
/// whether an edge table already exists (name/columns) and whether
/// "recursive CTE" here means true variable-depth traversal (WITH RECURSIVE
/// over an edges table) or just a non-recursive CTE replacing a single-hop
/// Cypher MATCH. Both open questions block writing this for real.
/// </summary>
public sealed class RecursiveCteGraphStrategy : IGraphStrategy
{
    public void AppendGraphJoin(StringBuilder sb, in GraphJoinSpec join, string primaryOutputAlias)
        => throw new NotImplementedException(
            "RecursiveCteGraphStrategy.AppendGraphJoin: needs the relational edge table's schema " +
            "(table/column names) and confirmation of whether traversal depth is fixed at one hop " +
            "or variable, before this can be written.");

    public void AppendGraphResultJoin(StringBuilder sb, in GraphResultJoinSpec join)
        => throw new NotImplementedException(
            "RecursiveCteGraphStrategy.AppendGraphResultJoin: same blocker as AppendGraphJoin.");

    public string BuildGraphMerge(in GraphMergeSpec spec)
        => throw new NotImplementedException(
            "RecursiveCteGraphStrategy.BuildGraphMerge: needs the edge table's schema to build an " +
            "INSERT ... ON CONFLICT against it instead of a Cypher MERGE.");
}
