using System.Text;
using CoffeeBeanery.GraphQL.Core.GraphQL;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.GraphQL.Helper;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public static class SqlHelper
{
    public static void GenerateUpsertStatements(
        Dictionary<string, NodeTree> modelTrees,
        Dictionary<string, NodeTree> entityTrees,
        Dictionary<string, SqlNode> sqlUpsertStatementNodes,
        NodeTree rootTree,
        List<string> entityNames,
        Dictionary<string, string> sqlWhereStatement,
        List<string> entitiesProcessed,
        List<string> statements,
        List<string> selectStatements)
    {
        var aliasesToProcess = sqlUpsertStatementNodes
            .Select(k => k.Key.Split('~')[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var cteParentAliases = CollectCteParentAliases(
            entityTrees, sqlUpsertStatementNodes, aliasesToProcess);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue   = new Queue<NodeTree>();

        foreach (var link in rootTree.ModelToEntityLinks)
        {
            if (entityTrees.TryGetValue(link.AliasTo, out var seed) &&
                !visited.Contains(seed.Alias))
                queue.Enqueue(seed);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!visited.Add(current.Alias))
                continue;

            if (aliasesToProcess.Contains(current.Alias) &&
                !entitiesProcessed.Contains(current.Alias))
            {
                entitiesProcessed.Add(current.Alias);

                if (!cteParentAliases.Contains(current.Alias))
                {
                    var currentColumns = sqlUpsertStatementNodes
                        .Where(k =>
                            k.Key.Split('~')[0].Matches(current.Alias) &&
                            entityNames.Contains(k.Value.RelationshipKey.Split('~')[1]))
                        .ToList();

                    if (currentColumns.Count > 0)
                    {
                        var fkLinks = CollectFkLinksForTree(
                            current, entityTrees, sqlUpsertStatementNodes);

                        if (fkLinks.Count > 0)
                        {
                            var cte = BuildCteSql(
                                current, currentColumns, fkLinks,
                                sqlUpsertStatementNodes);

                            if (!string.IsNullOrEmpty(cte) && !statements.Contains(cte))
                                statements.Add(cte);
                        }
                        else
                        {
                            GenerateUpsert(
                                current, currentColumns,
                                sqlWhereStatement.GetValueOrDefault(current.Alias) ?? string.Empty,
                                statements);
                        }

                        AppendGraphMerge(current, sqlUpsertStatementNodes, selectStatements);
                    }
                }
            }

            foreach (var link in current.Children.Concat(current.RelatedChildren))
            {
                var next = !string.IsNullOrWhiteSpace(link.AliasTo) ? link.AliasTo : link.To;
                if (entityTrees.TryGetValue(next, out var nextTree) &&
                    !visited.Contains(nextTree.Alias))
                    queue.Enqueue(nextTree);
            }
        }
    }

    private static HashSet<string> CollectCteParentAliases(
        Dictionary<string, NodeTree> entityTrees,
        Dictionary<string, SqlNode>  sqlUpsertStatementNodes,
        HashSet<string>              aliasesToProcess)
    {
        var cteParents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var alias in aliasesToProcess)
        {
            if (!entityTrees.TryGetValue(alias, out var tree))
                continue;

            var fkLinks = CollectFkLinksForTree(tree, entityTrees, sqlUpsertStatementNodes);

            foreach (var fk in fkLinks)
                cteParents.Add(fk.ParentTree.Alias);
        }

        return cteParents;
    }

    private sealed record FkLink(
        NodeTree ParentTree,
        string   FkColumn,
        string   PkColumn,
        string   CteName,
        List<KeyValuePair<string, SqlNode>> ParentColumns,
        List<string>                        OnConflictCols);

    private static List<FkLink> CollectFkLinksForTree(
        NodeTree                     currentTree,
        Dictionary<string, NodeTree> trees,
        Dictionary<string, SqlNode>  sqlUpsertStatementNodes)
    {
        var result = new List<FkLink>();

        foreach (var link in currentTree.Children.Concat(currentTree.RelatedChildren))
        {
            if (!link.ToColumn.Matches("Id"))
                continue;

            if (!link.AliasFrom.Matches(currentTree.Alias))
                continue;

            if (!trees.TryGetValue(link.AliasTo, out var parentTree))
                continue;

            var parentColumns = sqlUpsertStatementNodes
                .Where(k =>
                    k.Key.Split('~')[0].Matches(parentTree.Alias) &&
                    !k.Value.Column.Matches("Id"))
                .Where(k => !string.IsNullOrEmpty(k.Value.Value))
                .DistinctBy(k => k.Value.Column)
                .ToList();

            if (parentColumns.Count == 0)
                continue;

            var parentHasUpsertKey = parentColumns.Any(a =>
                parentTree.UpsertKeys.Any(b => b.Split('~')[1].Matches(a.Value.Column)));

            if (!parentHasUpsertKey)
                continue;

            result.Add(new FkLink(
                ParentTree:    parentTree,
                FkColumn:      link.FromColumn,
                PkColumn:      link.ToColumn,
                CteName:       $"cte_{link.AliasTo}",
                ParentColumns: parentColumns,
                OnConflictCols: currentTree.UpsertKeys
                                    .Select(k => k.Split('~')[1])
                                    .Distinct()
                                    .ToList()));
        }

        return result;
    }

    private static string BuildCteSql(
        NodeTree                             currentTree,
        List<KeyValuePair<string, SqlNode>>  currentColumns,
        List<FkLink>                         fkLinks,
        Dictionary<string, SqlNode>          sqlUpsertStatementNodes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WITH");

        var cteTerms = new List<string>();

        foreach (var fk in fkLinks)
        {
            var colNames  = string.Join(", ", fk.ParentColumns.Select(c => $"\"{c.Value.Column}\"").Distinct());
            var colValues = string.Join(", ", fk.ParentColumns.Select(c => $"'{EscapeValue(c.Value.Value)}'").Distinct());
            var conflict  = string.Join(", ", fk.ParentTree.UpsertKeys.Select(k => $"\"{k.Split('~')[1]}\""));
            var setExprs  = string.Join(", ", fk.ParentColumns
                .Select(c => $"\"{c.Value.Column}\" = EXCLUDED.\"{c.Value.Column}\"").Distinct());

            cteTerms.Add(
                $"    {fk.CteName} AS (\n" +
                $"        INSERT INTO \"{fk.ParentTree.Schema}\".\"{fk.ParentTree.Name}\" ( {colNames} )\n" +
                $"        VALUES ( {colValues} )\n" +
                $"        ON CONFLICT ({conflict}) DO UPDATE SET {setExprs}\n" +
                $"        RETURNING \"{fk.PkColumn}\"\n" +
                $"    )");
        }

        if (cteTerms.Count == 0)
            return string.Empty;

        sb.Append(string.Join(",\n", cteTerms));
        sb.AppendLine();

        var fkColNames = fkLinks.Select(f => f.FkColumn).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var scalarCols = currentColumns
            .Where(c => !string.IsNullOrEmpty(c.Value.Value))
            .Where(c => !fkColNames.Contains(c.Value.Column))
            .DistinctBy(c => c.Value.Column)
            .ToList();

        var insertCols  = new List<string>();
        var selectExprs = new List<string>();
        var fromCtes    = new List<string>();
        var setClauses  = new List<string>();

        var conflictColSet = fkLinks.First().OnConflictCols
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var col in scalarCols)
        {
            insertCols.Add($"\"{col.Value.Column}\"");
            selectExprs.Add($"'{EscapeValue(col.Value.Value)}' AS \"{col.Value.Column}\"");

            if (!conflictColSet.Contains(col.Value.Column))
                setClauses.Add($"\"{col.Value.Column}\" = EXCLUDED.\"{col.Value.Column}\"");
        }

        foreach (var fk in fkLinks)
        {
            insertCols.Add($"\"{fk.FkColumn}\"");
            selectExprs.Add($"{fk.CteName}.\"{fk.PkColumn}\" AS \"{fk.FkColumn}\"");
            fromCtes.Add(fk.CteName);
            setClauses.Add($"\"{fk.FkColumn}\" = EXCLUDED.\"{fk.FkColumn}\"");
        }

        var conflictOn = string.Join(", ", fkLinks.First().OnConflictCols.Select(c => $"\"{c}\""));

        sb.AppendLine($"INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\"");
        sb.AppendLine($"    ( {string.Join(", ", insertCols)} )");
        sb.AppendLine($"SELECT {string.Join(", ", selectExprs)}");
        sb.AppendLine($"FROM   {string.Join(", ", fromCtes)}");
        sb.AppendLine($"ON CONFLICT ({conflictOn})");
        sb.AppendLine(setClauses.Count > 0
            ? $"DO UPDATE SET {string.Join(", ", setClauses)};"
            : "DO NOTHING;");

        return sb.ToString();
    }

    private static void AppendGraphMerge(
        NodeTree currentTree,
        Dictionary<string, SqlNode> sqlUpsertStatementNodes,
        List<string> selectStatements)
    {
        if (currentTree.GraphMap == null)
            return;

        var map = currentTree.GraphMap;
        var sql = BuildMergeRelationship(
            graphName:     map.GraphName,
            fromLabel:     map.FromVertex.Label,
            fromKeyColumn: map.FromVertex.KeyColumn,
            fromValue:     sqlUpsertStatementNodes
                               .FirstOrDefault(a => a.Value.Column.Matches(map.FromVertex.KeyColumn))
                               .Value?.Value ?? string.Empty,
            toLabel:       map.ToVertex.Label,
            toKeyColumn:   map.ToVertex.KeyColumn,
            toValue:       sqlUpsertStatementNodes
                               .FirstOrDefault(a => a.Value.Column.Matches(map.ToVertex.KeyColumn))
                               .Value?.Value ?? string.Empty,
            edgeLabel:     map.EdgeLabel,
            edgeKeyColumn: map.EdgeKeyColumn,
            edgeValue:     sqlUpsertStatementNodes
                               .FirstOrDefault(a => a.Value.Column.Matches(map.EdgeKeyColumn))
                               .Value?.Value ?? string.Empty,
            edgeProperties: new Dictionary<string, string>
            {
                {
                    map.EdgeKeyColumn,
                    sqlUpsertStatementNodes
                        .FirstOrDefault(a => a.Value.Column.Matches(map.EdgeKeyColumn))
                        .Value?.Value ?? string.Empty
                }
            });

        if (!selectStatements.Contains(sql))
            selectStatements.Add(sql);
    }

    public static void GenerateUpsert(
        NodeTree currentTree,
        List<KeyValuePair<string, SqlNode>> currentColumns,
        string whereClause,
        List<string> statements)
    {
        if (currentColumns.Count == 0)
            return;

        var columnsWithValues = currentColumns
            .Where(a => !string.IsNullOrEmpty(a.Value.Value))
            .ToList();

        if (columnsWithValues.Count == 0 ||
            !columnsWithValues.Any(a =>
                currentTree.UpsertKeys.Any(b => b.Split('~')[1].Matches(a.Value.Column))))
            return;

        var columnNames  = string.Join(", ", columnsWithValues.Select(s => $"\"{s.Value.Column}\"").Distinct());
        var columnValues = string.Join(", ", columnsWithValues.Select(s => $"'{s.Value.Value}'").Distinct());

        var sql =
            $"INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" " +
            $"( {columnNames} ) VALUES ( {columnValues} ) " +
            $"ON CONFLICT ({string.Join(", ", currentTree.UpsertKeys.Select(a => $"\"{a.Split('~')[1]}\""))}) ";

        var exclude = columnsWithValues
            .Select(e => $"\"{e.Value.Column}\" = EXCLUDED.\"{e.Value.Column}\"")
            .Distinct().ToList();

        sql += exclude.Count > 0
            ? $"DO UPDATE SET {string.Join(", ", exclude)} {whereClause};"
            : $"DO NOTHING {whereClause};";

        if (!statements.Contains(sql))
            statements.Add(sql);
    }

    public static string BuildMergeRelationship(
        string graphName,
        string fromLabel,
        string fromKeyColumn,
        string fromValue,
        string toLabel,
        string toKeyColumn,
        string toValue,
        string edgeLabel,
        string edgeKeyColumn,
        string edgeValue,
        Dictionary<string, string>? edgeProperties = null)
    {
        var setClause = BuildSetClause(edgeProperties);

        return @$"
            ;CREATE TEMP TABLE temp_merge AS SELECT 1 
            FROM ag_catalog.cypher(
                '{graphName}',
                $$

                MERGE (a:{fromLabel} {{ {fromKeyColumn}: '{EscapeValue(fromValue)}' }})
                MERGE (b:{toLabel} {{ {toKeyColumn}: '{EscapeValue(toValue)}' }})

                MERGE (a)-[r:{edgeLabel}]->(b)

                {setClause}

                RETURN r.{edgeLabel}::text

                $$
            ) AS (r text); DROP TABLE temp_merge;
            ";
    }

    private static string BuildSetClause(Dictionary<string, string>? props)
    {
        if (props == null || props.Count == 0)
            return string.Empty;

        return "SET " + string.Join(", ",
            props.Select(p => $"r.{p.Key} = '{EscapeValue(p.Value)}'"));
    }

    private static string EscapeValue(string value)
        => value?.Replace("'", "''") ?? string.Empty;
}