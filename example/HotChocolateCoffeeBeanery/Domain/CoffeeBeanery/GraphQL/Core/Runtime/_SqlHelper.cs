using System.Text;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.GraphQL.Helper;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public static class _SqlHelper
{
    private sealed record PlanNodeContext(
        ExecutionNode Node,
        EntityNodeTree Tree,
        int? ParentNodeId,
        ExecutionEdge? EdgeFromParent);

    public static void GenerateUpsertStatements(
        Dictionary<string, EntityNodeTree> entityTrees,
        Dictionary<string, ModelNodeTree> modelTrees,
        _ExecutionPlan mutationPlan,
        EntityNodeTree rootTree,
        Dictionary<string, string> sqlWhereStatement,
        List<string> statements,
        List<string> selectStatements)
    {
        var nodeContexts = new Dictionary<int, PlanNodeContext>();

        _ExecutionEngine.Traverse(mutationPlan, (node, edge) =>
        {
            EntityNodeTree tree;

            if (modelTrees.TryGetValue(node.Alias, out var modelTree))
            {
                if (!entityTrees.TryGetValue(modelTree.EntityType!.Name, out tree))
                    return;
            }
            else if (!entityTrees.TryGetValue(node.Alias, out tree))
            {
                return;
            }

            nodeContexts[node.Id] = new PlanNodeContext(
                node,
                tree,
                edge?.From,
                edge);
        });

        var columnsByAlias = new Dictionary<string, List<(string Column, string Value)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var ctx in nodeContexts.Values)
        {
            if (ctx.Node.Values.Count == 0) continue;

            if (!columnsByAlias.TryGetValue(ctx.Tree.Alias, out var list))
            {
                list = new List<(string Column, string Value)>();
                columnsByAlias[ctx.Tree.Alias] = list;
            }

            foreach (var v in ctx.Node.Values)
            {
                var idx = list.FindIndex(c => c.Column == v.Column);
                if (idx >= 0) list[idx] = v;
                else list.Add(v);
            }
        }

        var parentLinksByAlias = BuildParentLinksByAlias(nodeContexts);

        var aliasesToProcess = new HashSet<string>(columnsByAlias.Keys, StringComparer.OrdinalIgnoreCase);

        var cteParentAliases = CollectCteParentAliases(columnsByAlias, aliasesToProcess, parentLinksByAlias);

        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ctx in nodeContexts.Values.OrderBy(c => c.Node.Id))
        {
            var current = ctx.Tree;

            if (!aliasesToProcess.Contains(current.Alias) || !processed.Add(current.Alias))
                continue;

            if (cteParentAliases.Contains(current.Alias))
                continue;

            if (!columnsByAlias.TryGetValue(current.Alias, out var currentColumns) || currentColumns.Count == 0)
                continue;

            var fkLinks = CollectFkLinksForTree(current, columnsByAlias, parentLinksByAlias);

            if (fkLinks.Count > 0)
            {
                var cte = BuildCteSql(current, currentColumns, fkLinks);
                if (!string.IsNullOrEmpty(cte) && !statements.Contains(cte))
                    statements.Add(cte);
            }
            else
            {
                GenerateUpsert(
                    current,
                    currentColumns,
                    sqlWhereStatement.GetValueOrDefault(current.Alias) ?? string.Empty,
                    statements);
            }

            AppendGraphMerge(current, columnsByAlias, parentLinksByAlias, selectStatements);
        }

        foreach (var alias in cteParentAliases)
        {
            if (!aliasesToProcess.Contains(alias) || processed.Contains(alias))
                continue;

            if (!columnsByAlias.TryGetValue(alias, out var parentColumns) || parentColumns.Count == 0)
                continue;

            if (!entityTrees.TryGetValue(alias, out var parentTree))
                continue;

            GenerateUpsert(
                parentTree,
                parentColumns,
                sqlWhereStatement.GetValueOrDefault(alias) ?? string.Empty,
                statements);

            processed.Add(alias);
        }
    }

    private static Dictionary<string, List<(EntityNodeTree ParentTree, EntityKey Link)>> BuildParentLinksByAlias(
        Dictionary<int, PlanNodeContext> nodeContexts)
    {
        var result = new Dictionary<string, List<(EntityNodeTree ParentTree, EntityKey Link)>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var ctx in nodeContexts.Values)
        {
            if (ctx.ParentNodeId is not { } parentId) continue;
            if (!nodeContexts.TryGetValue(parentId, out var parentCtx)) continue;
            if (ctx.EdgeFromParent is null) continue;

            var dependentAlias = parentCtx.Tree.Alias;

            var link = new EntityKey
            {
                EntityType = ctx.Tree.EntityType,
                From = dependentAlias,
                FromColumn = ctx.EdgeFromParent.ToColumn ?? "",
                To = ctx.Tree.Alias,
                ToColumn = ctx.EdgeFromParent.FromColumn ?? ""
            };

            if (!result.TryGetValue(dependentAlias, out var list))
            {
                list = new List<(EntityNodeTree ParentTree, EntityKey Link)>();
                result[dependentAlias] = list;
            }

            var alreadyIndexed = list.Any(x =>
                x.ParentTree.Alias.Equals(ctx.Tree.Alias, StringComparison.OrdinalIgnoreCase) &&
                x.Link.ToColumn.Equals(link.ToColumn, StringComparison.OrdinalIgnoreCase));

            if (!alreadyIndexed)
                list.Add((ctx.Tree, link));
        }

        return result;
    }

    private static HashSet<string> CollectCteParentAliases(
        Dictionary<string, List<(string Column, string Value)>> columnsByAlias,
        HashSet<string> aliasesToProcess,
        Dictionary<string, List<(EntityNodeTree ParentTree, EntityKey Link)>> parentLinksByAlias)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var alias in aliasesToProcess)
        {
            if (!parentLinksByAlias.TryGetValue(alias, out var dummy))
                continue;
        }

        foreach (var (dependentAlias, links) in parentLinksByAlias)
        {
            if (!aliasesToProcess.Contains(dependentAlias))
                continue;

            foreach (var (parentTree, link) in links)
            {
                if (columnsByAlias.ContainsKey(parentTree.Alias))
                    result.Add(parentTree.Alias);
            }
        }

        return result;
    }

    private static List<FkLink> CollectFkLinksForTree(
        EntityNodeTree currentTree,
        Dictionary<string, List<(string Column, string Value)>> columnsByAlias,
        Dictionary<string, List<(EntityNodeTree ParentTree, EntityKey Link)>> parentLinksByAlias)
    {
        var result = new List<FkLink>();

        if (!parentLinksByAlias.TryGetValue(currentTree.Alias, out var incomingLinks))
            return result;

        foreach (var (parentTree, link) in incomingLinks)
            TryAddFkLink(link, parentTree, columnsByAlias, currentTree, result);

        return result;
    }

    private sealed record FkLink(
        EntityNodeTree ParentTree,
        string BusinessKeyFkColumn,
        string BusinessKeyPkColumn,
        string SurrogateFkColumn,
        string CteName,
        List<(string Column, string Value)> ParentColumns,
        List<string> OnConflictCols);

    private static void TryAddFkLink(
        EntityKey link,
        EntityNodeTree parentTree,
        Dictionary<string, List<(string Column, string Value)>> columnsByAlias,
        EntityNodeTree currentTree,
        List<FkLink> result)
    {
        if (!columnsByAlias.TryGetValue(parentTree.Alias, out var parentColumns) || parentColumns.Count == 0)
            return;

        var surrogateFkColumn = link.ToColumn.EndsWith("Key", StringComparison.OrdinalIgnoreCase)
            ? link.ToColumn.Substring(0, link.ToColumn.Length - "Key".Length) + "Id"
            : null;

        result.Add(new FkLink(
            ParentTree: parentTree,
            BusinessKeyFkColumn: link.ToColumn,
            BusinessKeyPkColumn: link.FromColumn,
            SurrogateFkColumn: surrogateFkColumn,
            CteName: $"cte_{parentTree.Alias}",
            ParentColumns: parentColumns,
            OnConflictCols: currentTree.UpsertKeys));
    }

    private static string BuildCteSql(
        EntityNodeTree currentTree,
        List<(string Column, string Value)> currentColumns,
        List<FkLink> fkLinks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WITH");

        var cteTerms = new List<string>(fkLinks.Count);

        foreach (var fk in fkLinks)
        {
            var cols = string.Join(", ", fk.ParentColumns.Select(c => $"\"{c.Column}\""));
            var vals = string.Join(", ", fk.ParentColumns.Select(c => $"'{EscapeValue(c.Value)}'"));

            var conflict = string.Join(", ", fk.ParentTree.UpsertKeys.Select(c => $"\"{c}\""));
            var set = string.Join(", ", fk.ParentColumns.Select(c => $"\"{c.Column}\" = EXCLUDED.\"{c.Column}\""));

            var returningCols = fk.SurrogateFkColumn != null
                ? $"\"Id\", \"{fk.BusinessKeyPkColumn}\""
                : $"\"{fk.BusinessKeyPkColumn}\"";

            cteTerms.Add(
                $"    {fk.CteName} AS (\n" +
                $"        INSERT INTO \"{fk.ParentTree.Schema}\".\"{fk.ParentTree.Name}\" ({cols})\n" +
                $"        VALUES ({vals})\n" +
                $"        ON CONFLICT ({conflict}) DO UPDATE SET {set}\n" +
                $"        RETURNING {returningCols}\n" +
                $"    )");
        }

        if (cteTerms.Count == 0)
            return string.Empty;

        sb.Append(string.Join(",\n", cteTerms));
        sb.AppendLine();

        var fkCols = fkLinks
            .SelectMany(f => f.SurrogateFkColumn != null
                ? new[] { f.BusinessKeyFkColumn, f.SurrogateFkColumn }
                : new[] { f.BusinessKeyFkColumn })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var conflictCols = fkLinks[0].OnConflictCols;

        var scalarCols = currentColumns
            .Where(c => !fkCols.Contains(c.Column))
            .ToList();

        var insertCols = new List<string>();
        var selectCols = new List<string>();
        var fromCtes = new List<string>();
        var setCols = new List<string>();

        foreach (var col in scalarCols)
        {
            insertCols.Add($"\"{col.Column}\"");
            selectCols.Add($"'{EscapeValue(col.Value)}' AS \"{col.Column}\"");

            if (!conflictCols.Contains(col.Column))
                setCols.Add($"\"{col.Column}\" = EXCLUDED.\"{col.Column}\"");
        }

        foreach (var fk in fkLinks)
        {
            insertCols.Add($"\"{fk.BusinessKeyFkColumn}\"");
            selectCols.Add($"{fk.CteName}.\"{fk.BusinessKeyPkColumn}\" AS \"{fk.BusinessKeyFkColumn}\"");
            setCols.Add($"\"{fk.BusinessKeyFkColumn}\" = EXCLUDED.\"{fk.BusinessKeyFkColumn}\"");

            if (fk.SurrogateFkColumn != null)
            {
                insertCols.Add($"\"{fk.SurrogateFkColumn}\"");
                selectCols.Add($"{fk.CteName}.\"Id\" AS \"{fk.SurrogateFkColumn}\"");
                setCols.Add($"\"{fk.SurrogateFkColumn}\" = EXCLUDED.\"{fk.SurrogateFkColumn}\"");
            }

            fromCtes.Add(fk.CteName);
        }

        sb.AppendLine($"INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\"");
        sb.AppendLine($"({string.Join(", ", insertCols)})");
        sb.AppendLine($"SELECT {string.Join(", ", selectCols)}");
        sb.AppendLine($"FROM {string.Join(", ", fromCtes)}");
        sb.AppendLine($"ON CONFLICT ({string.Join(", ", conflictCols.Select(c => $"\"{c}\""))})");
        sb.AppendLine($"DO UPDATE SET {string.Join(", ", setCols)};");

        return sb.ToString();
    }

    private static string? ResolveVertexJoinValue(
        EntityNodeTree edgeTree,
        GraphVertex vertex,
        string joinColumn,
        Dictionary<string, List<(string Column, string Value)>> columnsByAlias,
        Dictionary<string, List<(EntityNodeTree ParentTree, EntityKey Link)>> parentLinksByAlias)
    {
        if (!string.IsNullOrWhiteSpace(vertex.AliasTo) &&
            columnsByAlias.TryGetValue(vertex.AliasTo, out var vertexCols))
        {
            var direct = vertexCols.FirstOrDefault(c =>
                string.Equals(c.Column, joinColumn, StringComparison.OrdinalIgnoreCase));

            if (direct.Column != null)
                return direct.Value;
        }

        if (columnsByAlias.TryGetValue(edgeTree.Alias, out var edgeCols))
        {
            if (parentLinksByAlias.TryGetValue(vertex.AliasTo ?? string.Empty, out var links))
            {
                foreach (var (parentTree, link) in links)
                {
                    if (parentTree.Alias.Equals(edgeTree.Alias, StringComparison.OrdinalIgnoreCase))
                    {
                        var fkVal = edgeCols.FirstOrDefault(c =>
                            string.Equals(c.Column, link.ToColumn, StringComparison.OrdinalIgnoreCase));

                        if (fkVal.Column != null)
                            return fkVal.Value;
                    }
                }
            }
        }

        return null;
    }

    private static void AppendGraphMerge(
        EntityNodeTree currentTree,
        Dictionary<string, List<(string Column, string Value)>> columnsByAlias,
        Dictionary<string, List<(EntityNodeTree ParentTree, EntityKey Link)>> parentLinksByAlias,
        List<string> selectStatements)
    {
        if (currentTree.GraphMap == null)
            return;

        var graphMap = currentTree.GraphMap;

        var fromValue = ResolveVertexJoinValue(
            currentTree, graphMap.FromVertex, graphMap.FromJoinColumn, columnsByAlias, parentLinksByAlias);

        var toValue = ResolveVertexJoinValue(
            currentTree, graphMap.ToVertex, graphMap.ToJoinColumn, columnsByAlias, parentLinksByAlias);

        string? edgeValue = null;
        if (columnsByAlias.TryGetValue(currentTree.Alias, out var edgeCols))
        {
            edgeValue = edgeCols.FirstOrDefault(c =>
                string.Equals(c.Column, graphMap.EdgeKeyColumn, StringComparison.OrdinalIgnoreCase)).Value;
        }

        if (fromValue == null || toValue == null || edgeValue == null)
            return;

        var sql = BuildMergeRelationship(
            graphMap.GraphName,
            graphMap.FromVertex.Label,
            graphMap.FromJoinColumn,
            fromValue,
            graphMap.ToVertex.Label,
            graphMap.ToJoinColumn,
            toValue,
            graphMap.EdgeLabel,
            graphMap.EdgeKeyColumn,
            edgeValue);

        if (!selectStatements.Contains(sql))
            selectStatements.Add(sql);
    }

    public static void GenerateUpsert(
        EntityNodeTree currentTree,
        List<(string Column, string Value)> currentColumns,
        string whereClause,
        List<string> statements)
    {
        if (currentColumns.Count == 0)
            return;

        var colNames = string.Join(", ", currentColumns.Select(c => $"\"{c.Column}\""));
        var colVals = string.Join(", ", currentColumns.Select(c => $"'{EscapeValue(c.Value)}'"));

        var sql =
            $"INSERT INTO \"{currentTree.Schema}\".\"{currentTree.Name}\" ({colNames}) " +
            $"VALUES ({colVals}) " +
            $"ON CONFLICT ({string.Join(", ", currentTree.UpsertKeys.Select(k => $"\"{k}\""))}) " +
            $"DO NOTHING {whereClause};";

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
        var setClause = edgeProperties == null
            ? ""
            : "SET " + string.Join(", ", edgeProperties.Select(p => $"r.{p.Key} = '{EscapeValue(p.Value)}'"));

        return $@"
;CREATE TEMP TABLE temp_merge AS SELECT 1
FROM ag_catalog.cypher(
'{graphName}',
$$
MERGE (a:{fromLabel} {{ {fromKeyColumn}: '{EscapeValue(fromValue)}' }})
MERGE (b:{toLabel} {{ {toKeyColumn}: '{EscapeValue(toValue)}' }})
MERGE (a)-[r:{edgeLabel}]->(b)
{setClause}
RETURN r
$$
) AS (r text); DROP TABLE temp_merge;";
    }

    private static string EscapeValue(string value)
        => value?.Replace("'", "''") ?? string.Empty;
}