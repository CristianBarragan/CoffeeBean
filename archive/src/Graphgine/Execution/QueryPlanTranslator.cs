using System.Collections.Generic;
using FoundationQuery = Foundgine.Builders;

namespace Graphgine.Execution;

/// <summary>
/// Lowers a Foundation logical QueryNode tree onto the existing, working
/// QueryPlanBuilder/PhysicalQueryPlan physical representation. This is the seam
/// between Foundation's provider-agnostic model and this project's
/// existing SQL/graph execution engine (SqlQueryCompiler etc.), which is
/// left completely untouched — it still only ever sees a PhysicalQueryPlan.
///
/// FIRST PASS / DIRECTIONAL:
/// - Alias assignment (EntityName, EntityName1, EntityName2, ...) is a
///   reasonable default, not necessarily what HotChocolateAdapter's real
///   alias resolution produces today — reconcile before relying on this
///   for anything user-facing.
/// - Foundation's EntityMetadata carries a single EntityId; this project
///   distinguishes EntityId (model-scoped) from StorageEntityId
///   (table-scoped). Both are populated with the same value below, which
///   is very likely wrong for composite models — flagging rather than
///   guessing further.
/// - GraphEdgeNode -> AddGraphJoin/AddGraphResultJoin mirrors the
///   convention already established in the old SelectionIR-driven
///   pipeline's PlannerEmitter.EmitGraphVertexResultJoins (graph alias =
///   "{EdgeEntityName}_{EdgeLabel}_graph", vertex alias-or-label doubles
///   as both the AddGraphJoin alias and the AddGraphResultJoin column-alias
///   prefix). Not independently verified against a running query yet.
/// </summary>
public static class QueryPlanTranslator
{
    public static PhysicalQueryPlan FromQueryNode(FoundationQuery.QueryNode root)
    {
        var builder = new QueryPlanBuilder();
        var aliasCounts = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        Walk(root, ref builder, aliasCounts);

        return builder.Build();
    }

    private static string NextAlias(Dictionary<string, int> counts, string entityName)
    {
        var count = counts.TryGetValue(entityName, out var c) ? c : 0;
        counts[entityName] = count + 1;
        return count == 0 ? entityName : $"{entityName}{count}";
    }

    /// <summary>Walks a node, emitting into the builder, and returns the SQL alias it was assigned.</summary>
    private static string Walk(
        FoundationQuery.QueryNode node,
        ref QueryPlanBuilder builder,
        Dictionary<string, int> aliasCounts)
    {
        switch (node)
        {
            case FoundationQuery.ScanNode scan:
            {
                var alias = NextAlias(aliasCounts, scan.Entity.Name);
                var storageId = scan.Entity.EntityId.Value;

                // NOTE: same value used for both ids -- see class remarks.
                builder.SetRoot(storageId, storageId, alias);

                return alias;
            }

            case FoundationQuery.JoinNode join:
            {
                var leftAlias = Walk(join.Left, ref builder, aliasCounts);
                var rightAlias = Walk(join.Right, ref builder, aliasCounts);

                var left = join.Join.Condition.Left;
                var right = join.Join.Condition.Right;

                builder.AddJoin(
                    leftAlias,
                    rightAlias,
                    left.Entity.EntityId.Value,
                    left.Entity.EntityId.Value,
                    left.ColumnId,
                    right.Entity.EntityId.Value,
                    right.Entity.EntityId.Value,
                    right.ColumnId,
                    join.Join.Kind == Foundgine.Metadata.JoinKind.Left
                        ? JoinKind.Left
                        : JoinKind.Inner);

                return rightAlias;
            }

            case FoundationQuery.GraphEdgeNode graphEdge:
                return TranslateGraphEdge(graphEdge, ref builder, aliasCounts);

            case FoundationQuery.ProjectionNode projection:
            {
                var alias = Walk(projection.Source, ref builder, aliasCounts);

                foreach (var field in projection.Fields)
                {
                    var columnName = "";

                    foreach (var column in field.Source.Entity.Columns)
                    {
                        if (column.Id.Value == field.Source.ColumnId)
                        {
                            columnName = column.Name;
                            break;
                        }
                    }

                    builder.AddColumn(
                        field.Source.Entity.EntityId.Value,
                        field.Source.Entity.EntityId.Value,
                        field.Source.ColumnId,
                        alias,
                        columnName);
                }

                return alias;
            }

            case FoundationQuery.MaterializeNode materialize:
                return Walk(materialize.Source, ref builder, aliasCounts);

            default:
                throw new System.NotSupportedException(
                    $"QueryPlanTranslator: unsupported QueryNode '{node.GetType().Name}'.");
        }
    }

    /// <summary>
    /// Mirrors the (already correct, working) convention from the old
    /// SelectionIR-driven pipeline's EmitGraphVertexResultJoins: the graph
    /// join's own alias is "{EdgeEntityName}_{EdgeLabel}_graph"; each
    /// vertex's alias-or-label is both what AddGraphJoin's from/to alias
    /// params want AND the column alias prefix AddGraphResultJoin needs
    /// ("{alias}{joinColumn}"). Either vertex side is only wired up with
    /// AddGraphResultJoin if that side was actually supplied (i.e. actually
    /// selected as a child in this query) -- unlike a JoinNode, a graph
    /// edge's two sides are independently optional.
    /// </summary>
    private static string TranslateGraphEdge(
        FoundationQuery.GraphEdgeNode graphEdge,
        ref QueryPlanBuilder builder,
        Dictionary<string, int> aliasCounts)
    {
        var sourceAlias = Walk(graphEdge.Source, ref builder, aliasCounts);

        var graph = graphEdge.Graph;
        var edgeStorageId = graph.EdgeEntity.EntityId.Value;
        var graphAlias = $"{graph.EdgeEntity.Name}_{graph.EdgeLabel}_graph";

        builder.AddGraphJoin(
            edgeStorageId,
            edgeStorageId,
            graph.GraphName,
            graph.EdgeLabel,
            graph.EdgeKeyColumn,
            graph.From.Label,
            graph.From.GraphProperty,
            graph.From.Alias,
            graph.From.JoinColumn,
            graph.To.Label,
            graph.To.GraphProperty,
            graph.To.Alias,
            graph.To.JoinColumn,
            graphAlias);

        if (graphEdge.From is not null)
        {
            Walk(graphEdge.From, ref builder, aliasCounts);
            AddGraphResultJoin(ref builder, graphAlias, graph.From);
        }

        if (graphEdge.To is not null)
        {
            Walk(graphEdge.To, ref builder, aliasCounts);
            AddGraphResultJoin(ref builder, graphAlias, graph.To);
        }

        return sourceAlias;
    }

    private static void AddGraphResultJoin(
        ref QueryPlanBuilder builder,
        string graphAlias,
        Foundgine.Metadata.VertexMetadata vertex)
    {
        var columnAlias = vertex.Alias + vertex.JoinColumn;

        var columnId = 0;

        foreach (var column in vertex.ConnectedEntity.Columns)
        {
            if (string.Equals(column.Name, vertex.JoinColumn, System.StringComparison.Ordinal))
            {
                columnId = column.Id.Value;
                break;
            }
        }

        builder.AddGraphResultJoin(
            graphAlias,
            columnAlias,
            vertex.ConnectedEntity.EntityId.Value,
            vertex.ConnectedEntity.EntityId.Value,
            (ushort)columnId,
            JoinKind.Left,
            vertex.Alias);
    }
}