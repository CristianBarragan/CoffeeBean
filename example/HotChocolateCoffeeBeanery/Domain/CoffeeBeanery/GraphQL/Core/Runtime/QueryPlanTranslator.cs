using System.Collections.Generic;
using FoundationQuery = CoffeeBeanery.GraphQL.Core.Foundation.QueryPlan;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

/// <summary>
/// Lowers a Foundation logical QueryNode tree onto the existing, working
/// QueryPlanBuilder/QueryPlan physical representation. This is the seam
/// between Foundation's provider-agnostic model and this project's
/// existing SQL/graph execution engine (SqlQueryCompiler etc.), which is
/// left completely untouched — it still only ever sees a QueryPlan.
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
/// - GraphEdgeNode is not translated to AddGraphJoin/AddGraphResultJoin
///   yet: GraphMetadata doesn't currently carry the graph/edge label
///   strings (GraphName, EdgeLabel, EdgeKeyColumn, vertex Alias) that
///   AddGraphJoin needs. Left as a stub — see TranslateGraphEdge below.
/// </summary>
public static class QueryPlanTranslator
{
    public static QueryPlan FromQueryNode(FoundationQuery.QueryNode root)
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
                    join.Join.Kind == CoffeeBeanery.GraphQL.Core.Foundation.Metadata.JoinKind.Left
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
    /// STUB: GraphMetadata needs GraphName/EdgeLabel/EdgeKeyColumn and each
    /// VertexMetadata needs a graph-property name + SQL alias before this
    /// can call builder.AddGraphJoin/AddGraphResultJoin faithfully. Wire up
    /// once that's added to Foundation.Metadata.GraphMetadata.
    /// </summary>
    private static string TranslateGraphEdge(
        FoundationQuery.GraphEdgeNode graphEdge,
        ref QueryPlanBuilder builder,
        Dictionary<string, int> aliasCounts)
    {
        var fromAlias = Walk(graphEdge.From, ref builder, aliasCounts);
        Walk(graphEdge.To, ref builder, aliasCounts);

        // TODO: builder.AddGraphJoin(...) / AddGraphResultJoin(...) once
        // GraphMetadata carries enough to fill those calls in.
        return fromAlias;
    }
}
