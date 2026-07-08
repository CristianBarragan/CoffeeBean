using System.Collections.Immutable;
using System.Text;
using CoffeeBeanery.GraphQL.Core.Runtime;

namespace CoffeeBeanery.GraphQL.Core.Sql;

public sealed class PostgresSqlWriter
{
    private readonly IEntityMetaProvider _meta;

    public PostgresSqlWriter(IEntityMetaProvider meta)
    {
        _meta = meta;
    }
    
    public string WriteUpsertThenSelect(in MutationPlan mutation, in QueryPlan query)
    {
        var sb = new StringBuilder(1024);
        var arms = CollectUpsertArms(mutation);

        if (arms.Count > 0)
        {
            sb.AppendLine("WITH");
            for (var i = 0; i < arms.Count; i++)
            {
                sb.Append("  mut_").Append(i).Append(" AS (");
                sb.Append(arms[i]);
                sb.Append(" RETURNING 1)");
                sb.AppendLine(i < arms.Count - 1 ? "," : "");
            }
        }

        AppendSelect(sb, query);
        return sb.ToString();
    }

    public string WriteSelect(in QueryPlan plan)
    {
        var sb = new StringBuilder(512);
        AppendSelect(sb, plan);
        return sb.ToString();
    }

    public string WriteUpserts(in MutationPlan plan)
    {
        var arms = CollectUpsertArms(plan);
        if (arms.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        for (var i = 0; i < arms.Count; i++)
        {
            if (i > 0) sb.AppendLine(";");
            sb.Append(arms[i]);
        }
        sb.Append(';');
        return sb.ToString();
    }

    // ---------------------------------------------------------------
    // Collect all upsert SQL arms
    // ---------------------------------------------------------------

    private List<string> CollectUpsertArms(in MutationPlan plan)
    {
        var arms = new List<string>();

        foreach (var row in plan.Rows)
            arms.Add(BuildRegularUpsert(row));

        foreach (var root in plan.CteRoots)
        {
            arms.Add(BuildCteNodeUpsert(root));
            foreach (var child in root.Children)
            {
                var fkSql = BuildFkResolution(root, child);
                if (!string.IsNullOrEmpty(fkSql))
                    arms.Add(fkSql);
            }
        }

        return arms;
    }

    private string BuildRegularUpsert(in UpsertRow row)
    {
        var schema       = row.SchemaOverride ?? _meta.EntitySchema[row.StorageEntityId];
        var table        = row.TableOverride  ?? _meta.EntityTable[row.StorageEntityId];
        var cols         = _meta.EntityColumnName[row.StorageEntityId];      // ← EntityColumnName
        var conflictCols = _meta.ConflictColumns[row.EntityId];

        var sb = new StringBuilder();
        sb.Append("INSERT INTO \"").Append(schema).Append("\".\"").Append(table).Append("\" (");

        for (int c = 0; c < row.Values.Length; c++)
        {
            if (c > 0) sb.Append(", ");
            sb.Append('"').Append(cols[row.Values[c].FieldId]).Append('"');
        }

        sb.Append(") VALUES (");
        for (int c = 0; c < row.Values.Length; c++)
        {
            if (c > 0) sb.Append(", ");
            AppendQuotedValue(sb, row.Values[c].RawValue);
        }
        sb.Append(')');
        AppendDoUpdateSet(sb, row.Values, cols, conflictCols);
        return sb.ToString();
    }

    private string BuildCteNodeUpsert(in MutationCteNode node)
    {
        var schema       = node.SchemaOverride ?? _meta.EntitySchema[node.StorageEntityId];
        var table        = node.TableOverride  ?? _meta.EntityTable[node.StorageEntityId];
        var cols         = _meta.EntityColumnName[node.StorageEntityId];     // ← EntityColumnName
        var conflictCols = node.ConflictColumns.Length > 0
            ? node.ConflictColumns.ToArray()
            : _meta.ConflictColumns[node.EntityId];

        var sb = new StringBuilder();
        sb.Append("INSERT INTO \"").Append(schema).Append("\".\"").Append(table).Append("\" (");

        for (int c = 0; c < node.Values.Length; c++)
        {
            if (c > 0) sb.Append(", ");
            sb.Append('"').Append(cols[node.Values[c].FieldId]).Append('"');
        }

        sb.Append(") VALUES (");
        for (int c = 0; c < node.Values.Length; c++)
        {
            if (c > 0) sb.Append(", ");
            AppendQuotedValue(sb, node.Values[c].RawValue);
        }
        sb.Append(')');
        AppendDoUpdateSetFromNames(sb, node.Values, cols, conflictCols);
        return sb.ToString();
    }

    private string BuildFkResolution(in MutationCteNode root, in MutationCteNode child)
    {
        var resolutions = _meta.CteResolutions[root.EntityId];              // ← EntityId not StorageEntityId
        var specFound = false;
        CteResolutionSpec spec = default;

        foreach (var r in resolutions)
        {
            if (string.Equals(r.NavigationAlias, child.Alias, StringComparison.OrdinalIgnoreCase))
            {
                spec = r;
                specFound = true;
                break;
            }
        }

        if (!specFound) return string.Empty;

        // Find PK value using storage entity columns
        var pkValue  = string.Empty;
        var rootCols = _meta.EntityColumnName[root.StorageEntityId];         // ← EntityColumnName
        foreach (var v in root.Values)
        {
            if (string.Equals(rootCols[v.FieldId], spec.OwningPkColumn,
                    StringComparison.OrdinalIgnoreCase))
            {
                pkValue = v.RawValue;
                break;
            }
        }

        var naturalKeyValue = child.Values.Length > 0 ? child.Values[0].RawValue : "NULL";

        var owningSchema  = root.SchemaOverride ?? _meta.EntitySchema[root.StorageEntityId];
        var owningTable   = root.TableOverride  ?? _meta.EntityTable[root.StorageEntityId];
        var relatedSchema = _meta.EntitySchema[child.StorageEntityId];       // ← EntitySchema
        var relatedTable  = _meta.EntityTable[child.StorageEntityId];        // ← EntityTable

        var sb = new StringBuilder();
        sb.Append("INSERT INTO \"").Append(owningSchema).Append("\".\"").Append(owningTable)
          .Append("\" (\"").Append(spec.ForeignKeyColumn)
          .Append("\", \"").Append(spec.OwningPkColumn).Append("\") (");

        sb.Append("SELECT ").Append(spec.RelatedTableAlias)
          .Append(".\"").Append(spec.RelatedSurrogateIdColumn)
          .Append("\" AS \"").Append(spec.ForeignKeyColumn).Append("\", ");
        AppendQuotedValue(sb, pkValue);
        sb.Append(" AS \"").Append(spec.OwningPkColumn)
          .Append("\" FROM \"").Append(relatedSchema).Append("\".\"").Append(relatedTable)
          .Append("\" ").Append(spec.RelatedTableAlias)
          .Append(" WHERE \"").Append(spec.RelatedNaturalKeyColumn).Append("\" = ");
        AppendQuotedValue(sb, naturalKeyValue);
        sb.Append(')');

        sb.Append(" ON CONFLICT (\"").Append(spec.OwningPkColumn)
          .Append("\") DO UPDATE SET \"").Append(spec.ForeignKeyColumn)
          .Append("\" = EXCLUDED.\"").Append(spec.ForeignKeyColumn).Append('"');

        return sb.ToString();
    }

    private void AppendSelect(StringBuilder sb, in QueryPlan plan)
    {
        sb.Append("SELECT DISTINCT");

        var first = true;
        foreach (var col in plan.Columns)
        {
            if (!first) sb.Append(',');
            first = false;

            var colNames = _meta.EntityColumnName[col.StorageEntityId];  // ← EntityColumnName
            if (col.ColumnId >= colNames.Length)
                throw new IndexOutOfRangeException(
                    $"ColumnId {col.ColumnId} out of range for StorageEntityId {col.StorageEntityId} " +
                    $"({_meta.EntityTable[col.StorageEntityId]}, Length={colNames.Length}), " +
                    $"ModelId={col.EntityId} ({_meta.ModelName[col.EntityId][0]})");

            sb.Append("\n    ")
              .Append('"').Append(col.EntityOutputAlias).Append('"')
              .Append(".\"").Append(colNames[col.ColumnId]).Append('"')
              .Append(" AS \"").Append(col.ColumnOutputAlias).Append('"');
        }

        if (first) sb.Append("\n    1");

        sb.Append("\nFROM ");
        AppendQualifiedTable(sb, plan.RootStorageEntityId);
        sb.Append(" \"").Append(plan.RootOutputAlias).Append('"');

        foreach (var join in plan.Joins)
        {
            sb.Append('\n');
            AppendJoin(sb, join, plan);
        }
    }

    private void AppendJoin(StringBuilder sb, in JoinSpec join, in QueryPlan plan)
    {
        var keyword = join.Kind == JoinKind.Left ? "LEFT JOIN" : "JOIN";
        sb.Append(keyword).Append(' ');
        AppendQualifiedTable(sb, join.ToStorageEntityId);
        sb.Append(" \"").Append(join.ToOutputAlias).Append('"');
        sb.Append("\n    ON ");

        var fromAlias   = ResolveOutputAlias(join.FromEntityId, plan);
        var fromColName = _meta.EntityColumnName[join.FromStorageEntityId][join.FromColumnId];
        var toColName   = _meta.EntityColumnName[join.ToStorageEntityId][join.ToColumnId];

        sb.Append('"').Append(join.ToOutputAlias).Append("\".\"").Append(toColName).Append('"')
          .Append(" = \"").Append(fromAlias).Append("\".\"").Append(fromColName).Append('"');
    }

    private void AppendQualifiedTable(StringBuilder sb, ushort storageEntityId)
    {
        sb.Append('"').Append(_meta.EntitySchema[storageEntityId]).Append("\".\"")
          .Append(_meta.EntityTable[storageEntityId]).Append('"');
    }

    private string ResolveOutputAlias(ushort entityId, in QueryPlan plan)
    {
        if (entityId == plan.RootEntityId) return plan.RootOutputAlias;
        foreach (var j in plan.Joins)
            if (j.ToEntityId == entityId) return j.ToOutputAlias;
        return _meta.Table[plan.RootEntityId][0];
    }

    // ---------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------

    private static void AppendQuotedValue(StringBuilder sb, string value)
        => sb.Append('\'').Append(value.Replace("'", "''")).Append('\'');

    private static void AppendDoUpdateSet(
        StringBuilder sb,
        ImmutableArray<FieldValue> values,
        string[] cols,
        string[] conflictCols)
    {
        AppendDoUpdateSetFromNames(sb, values, cols, conflictCols);
    }

    private static void AppendDoUpdateSetFromNames(
        StringBuilder sb,
        ImmutableArray<FieldValue> values,
        string[] cols,
        string[] conflictCols)
    {
        if (conflictCols.Length == 0)
        {
            sb.Append(" ON CONFLICT DO NOTHING");
            return;
        }

        sb.Append(" ON CONFLICT (");
        for (int i = 0; i < conflictCols.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(conflictCols[i]).Append('"');
        }
        sb.Append(") DO UPDATE SET ");

        var firstUpdate = true;
        for (int c = 0; c < values.Length; c++)
        {
            var colName = cols[values[c].FieldId];
            var isConflict = false;
            foreach (var cc in conflictCols)
                if (string.Equals(cc, colName, StringComparison.OrdinalIgnoreCase))
                { isConflict = true; break; }
            if (isConflict) continue;

            if (!firstUpdate) sb.Append(", ");
            firstUpdate = false;
            sb.Append('"').Append(colName).Append("\" = EXCLUDED.\"").Append(colName).Append('"');
        }

        if (firstUpdate)
        {
            sb.Length -= " DO UPDATE SET ".Length;
            sb.Append(" DO NOTHING");
        }
    }
}