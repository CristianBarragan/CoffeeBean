using System.Collections.Immutable;
using System.Text;
using CoffeeBeanery.GraphQL.Core.Runtime;

namespace Domain.Shared;

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
            if (i > 0)
                sb.AppendLine(";");

            sb.Append(arms[i]);
        }
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
            arms.Add(BuildCteNodeUpsertMerged(root));

        return arms;
    }

    private string BuildRegularUpsert(in UpsertRow row)
    {
        var schema =
            row.SchemaOverride ??
            _meta.EntitySchema[row.StorageEntityId];

        var table =
            row.TableOverride ??
            _meta.EntityTable[row.StorageEntityId];

        var conflictCols =
            _meta.ConflictColumns[row.EntityId];

        var sb = new StringBuilder();

        sb.Append("INSERT INTO \"")
            .Append(schema)
            .Append("\".\"")
            .Append(table)
            .Append("\" (");

        for (int c = 0; c < row.Values.Length; c++)
        {
            if (c > 0)
                sb.Append(", ");

            var column =
                ResolveColumnName(
                    row.EntityId,
                    row.StorageEntityId,
                    row.Values[c].FieldId);

            sb.Append('"')
                .Append(column)
                .Append('"');
        }

        sb.Append(") VALUES (");

        for (int c = 0; c < row.Values.Length; c++)
        {
            if (c > 0)
                sb.Append(", ");

            AppendFieldValue(sb, row.StorageEntityId, row.Values[c].FieldId, row.Values[c].RawValue);
        }

        sb.Append(')');

        AppendDoUpdateSet(
            sb,
            row.EntityId,
            row.StorageEntityId,
            row.Values,
            conflictCols);

        return sb.ToString();
    }
    
    private string ResolveColumnName(
        ushort entityId,
        ushort storageEntityId,
        ushort columnId)
    {
        var cols = _meta.EntityColumnName[storageEntityId];

        if ((uint)columnId >= (uint)cols.Length)
        {
            throw new Exception(
                $"ColumnId {columnId} is outside EntityColumnName[{storageEntityId}] " +
                $"({_meta.EntityTable[storageEntityId]}, Length={cols.Length}). " +
                $"Entity={entityId}");
        }

        var columnName = cols[columnId];

        if (string.IsNullOrEmpty(columnName))
        {
            throw new Exception(
                $"Empty column name. StorageEntity={storageEntityId}, Column={columnId}");
        }

        return columnName;
    }
    
    private string BuildCteNodeUpsertMerged(in MutationCteNode root)
    {
        var owningSchema = root.SchemaOverride ?? _meta.EntitySchema[root.StorageEntityId];
        var owningTable  = root.TableOverride  ?? _meta.EntityTable[root.StorageEntityId];
        var rootCols     = _meta.EntityColumnName[root.StorageEntityId];
        var conflictCols = root.ConflictColumns.Length > 0
            ? root.ConflictColumns.ToArray()
            : _meta.ConflictColumns[root.EntityId];

        var resolutions = _meta.CteResolutions[root.EntityId];

        var matched = new List<(MutationCteNode child, CteResolutionSpec spec)>();
        var seenAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in root.Children)
        {
            if (!seenAliases.Add(child.Alias))
                continue;

            foreach (var r in resolutions)
            {
                if (string.Equals(r.NavigationAlias, child.Alias, StringComparison.OrdinalIgnoreCase))
                {
                    matched.Add((child, r));
                    break;
                }
            }
        }
        
        if (matched.Count == 0)
        {
            var plainSb = new StringBuilder();
            plainSb.Append("INSERT INTO \"").Append(owningSchema).Append("\".\"").Append(owningTable).Append("\" (");

            for (int c = 0; c < root.Values.Length; c++)
            {
                if (c > 0) plainSb.Append(", ");
                
                plainSb.Append('"').Append(rootCols[root.Values[c].FieldId]).Append('"');
            }
            plainSb.Append(") VALUES (");
            for (int c = 0; c < root.Values.Length; c++)
            {
                if (c > 0) plainSb.Append(", ");
                AppendFieldValue(plainSb, root.StorageEntityId, root.Values[c].FieldId, root.Values[c].RawValue);
            }
            plainSb.Append(')');

            AppendDoUpdateSetFromNames(plainSb, root.EntityId, root.StorageEntityId, root.Values, rootCols, conflictCols);
            return plainSb.ToString();
        }

        var sb = new StringBuilder();
        sb.Append("INSERT INTO \"").Append(owningSchema).Append("\".\"").Append(owningTable).Append("\" (");

        for (int c = 0; c < root.Values.Length; c++)
        {
            if (c > 0) sb.Append(", ");
            sb.Append('"').Append(rootCols[root.Values[c].FieldId]).Append('"');
        }
        for (int i = 0; i < matched.Count; i++)
        {
            if (root.Values.Length > 0 || i > 0) sb.Append(", ");
            sb.Append('"').Append(matched[i].spec.ForeignKeyColumn).Append('"');
        }
        sb.Append(") SELECT ");

        for (int c = 0; c < root.Values.Length; c++)
        {
            if (c > 0) sb.Append(", ");
            AppendFieldValue(sb, root.StorageEntityId, root.Values[c].FieldId, root.Values[c].RawValue);
        }
        for (int i = 0; i < matched.Count; i++)
        {
            if (root.Values.Length > 0 || i > 0) sb.Append(", ");
            var spec = matched[i].spec;
            sb.Append(spec.RelatedTableAlias).Append(".\"").Append(spec.RelatedSurrogateIdColumn).Append('"');
        }

        sb.Append(" FROM ");
        for (int i = 0; i < matched.Count; i++)
        {
            var (child, spec) = matched[i];
            var relatedSchema = _meta.EntitySchema[child.StorageEntityId];
            var relatedTable  = _meta.EntityTable[child.StorageEntityId];
            if (i > 0) sb.Append(" CROSS JOIN ");
            sb.Append('"').Append(relatedSchema).Append("\".\"").Append(relatedTable)
              .Append("\" ").Append(spec.RelatedTableAlias);
        }

        sb.Append(" WHERE ");
        for (int i = 0; i < matched.Count; i++)
        {
            var (child, spec) = matched[i];
            var naturalKeyValue = child.Values.Length > 0 ? child.Values[0].RawValue : "NULL";
            if (i > 0) sb.Append(" AND ");
            sb.Append(spec.RelatedTableAlias).Append(".\"").Append(spec.RelatedNaturalKeyColumn).Append("\" = ");
            AppendQuotedValue(sb, naturalKeyValue);
        }

        sb.Append(" ON CONFLICT (");
        for (int i = 0; i < conflictCols.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(conflictCols[i]).Append('"');
        }
        sb.Append(") DO UPDATE SET ");

        var firstSet = true;
        foreach (var v in root.Values)
        {
            // v.FieldId is already a ColumnId — index rootCols directly.
            var col = rootCols[v.FieldId];

            if (Array.IndexOf(conflictCols, col) >= 0)
                continue;
            if (!firstSet) sb.Append(", ");
            firstSet = false;
            sb.Append('"').Append(col).Append("\" = EXCLUDED.\"").Append(col).Append('"');
        }
        foreach (var (_, spec) in matched)
        {
            if (!firstSet) sb.Append(", ");
            firstSet = false;
            sb.Append('"').Append(spec.ForeignKeyColumn).Append("\" = EXCLUDED.\"").Append(spec.ForeignKeyColumn).Append('"');
        }

        return sb.ToString();
    }

    private void AppendSelect(StringBuilder sb, in QueryPlan plan)
    {
        sb.Append("SELECT DISTINCT");

        var first = true;

        foreach (var col in plan.Columns)
        {
            if (!first)
                sb.Append(',');

            first = false;

            var colNames = _meta.EntityColumnName[col.StorageEntityId];

            if (col.ColumnId >= colNames.Length)
            {
                throw new IndexOutOfRangeException(
                    $"ColumnId {col.ColumnId} out of range for StorageEntityId {col.StorageEntityId} " +
                    $"({_meta.EntityTable[col.StorageEntityId]}, Length={colNames.Length}), " +
                    $"ModelId={col.EntityId} ({_meta.ModelName[col.EntityId][0]})");
            }

            sb.Append("\n    ")
                .Append('"')
                .Append(col.EntityOutputAlias)
                .Append('"')
                .Append(".\"")
                .Append(colNames[col.ColumnId])
                .Append('"')
                .Append(" AS \"")
                .Append(col.ColumnOutputAlias)
                .Append('"');
        }

        if (first)
            sb.Append("\n    1");

        sb.Append("\nFROM ");
        AppendQualifiedTable(sb, plan.RootStorageEntityId);

        sb.Append(" \"")
            .Append(plan.RootOutputAlias)
            .Append('"');

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

    private void AppendFieldValue(
        StringBuilder sb,
        ushort storageEntityId,
        ushort columnId,
        string rawValue)
    {
        var converted = EnumConversions.TryConvert(storageEntityId, columnId, rawValue);
        if (converted != null)
        {
            sb.Append(converted);
            return;
        }
        AppendQuotedValue(sb, rawValue);
    }
    
    private static void AppendQuotedValue(StringBuilder sb, string value)
    {
        if (int.TryParse(value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            sb.Append(value);
            return;
        }
        sb.Append('\'').Append(value.Replace("'", "''")).Append('\'');
    }

    private void AppendDoUpdateSet(
        StringBuilder sb,
        ushort entityId,
        ushort storageEntityId,
        ImmutableArray<FieldValue> values,
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
            if (i > 0)
                sb.Append(", ");

            sb.Append('"')
                .Append(conflictCols[i])
                .Append('"');
        }

        sb.Append(") DO UPDATE SET ");

        var first = true;

        foreach (var value in values)
        {
            // value.FieldId is already a ColumnId — resolve the name directly
            // via EntityColumnName rather than through _meta.FieldToColumn (a
            // model-FieldId→ColumnId table, which this value has already passed
            // through once at codegen time).
            var columnName = ResolveColumnName(entityId, storageEntityId, value.FieldId);

            if (Array.Exists(
                    conflictCols,
                    x => string.Equals(
                        x,
                        columnName,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!first)
                sb.Append(", ");

            first = false;

            sb.Append('"')
                .Append(columnName)
                .Append("\" = EXCLUDED.\"")
                .Append(columnName)
                .Append('"');
        }

        if (first)
        {
            sb.Length -= " DO UPDATE SET ".Length;
            sb.Append(" DO NOTHING");
        }
    }

    private void AppendDoUpdateSetFromNames(
        StringBuilder sb,
        ushort entityId,
        ushort storageEntityId,
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
            if (i > 0)
                sb.Append(", ");

            sb.Append('"')
                .Append(conflictCols[i])
                .Append('"');
        }

        sb.Append(") DO UPDATE SET ");

        var firstUpdate = true;

        for (int c = 0; c < values.Length; c++)
        {
            // values[c].FieldId is already a ColumnId — index cols directly.
            var colName = cols[values[c].FieldId];

            var isConflict = false;

            foreach (var cc in conflictCols)
            {
                if (string.Equals(
                        cc,
                        colName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    isConflict = true;
                    break;
                }
            }

            if (isConflict)
                continue;

            if (!firstUpdate)
                sb.Append(", ");

            firstUpdate = false;

            sb.Append('"')
                .Append(colName)
                .Append("\" = EXCLUDED.\"")
                .Append(colName)
                .Append('"');
        }

        if (firstUpdate)
        {
            sb.Length -= " DO UPDATE SET ".Length;
            sb.Append(" DO NOTHING");
        }
    }
}