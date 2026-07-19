using System.Collections.Immutable;
using System.Text;
using CoffeeBeanery.GraphQL.Core.Runtime;
using Domain.Shared;

namespace Domain.Shared;

public sealed class PostgresSqlWriter
{
    private readonly IEntityMetaProvider _meta;
    private readonly IGraphStrategy _graphStrategy;

    public PostgresSqlWriter(IEntityMetaProvider meta, IGraphStrategy graphStrategy)
    {
        _meta = meta;
        _graphStrategy = graphStrategy;
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

    private List<string> CollectUpsertArms(in MutationPlan plan)
    {
        var arms = new List<string>();

        foreach (var row in plan.Rows)
            arms.Add(BuildRegularUpsert(row));

        foreach (var root in plan.CteRoots)
            arms.Add(BuildCteNodeUpsertMerged(root));

        return arms;
    }

    // Graph merges are NOT part of the WITH ... AS (...) arms — the current
    // (AGE) strategy can't run inside a plain CTE, so they render as
    // separate statements. Delegated entirely to IGraphStrategy now; a
    // future strategy backed by ordinary tables could in principle return
    // something CTE-compatible, but the call site doesn't assume that.
    public string WriteGraphMerges(in MutationPlan plan)
    {
        var sb = new StringBuilder();
        foreach (var merge in plan.GraphMerges)
        {
            sb.AppendLine(_graphStrategy.BuildGraphMerge(merge));
        }
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
            if (!seenAliases.Add(child.Alias)) continue;
            foreach (var r in resolutions)
            {
                if (string.Equals(r.NavigationAlias, child.Alias, StringComparison.OrdinalIgnoreCase))
                {
                    matched.Add((child, r));
                    break;
                }
            }
        }

        var valueByColumn = new Dictionary<string, FieldValue>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in root.Values)
        {
            if (v.FieldId < rootCols.Length)
                valueByColumn[rootCols[v.FieldId]] = v;
        }

        var fkColumnNames = new HashSet<string>(
            matched.Select(m => m.spec.ForeignKeyColumn),
            StringComparer.OrdinalIgnoreCase);

        foreach (var cc in conflictCols)
        {
            if (!valueByColumn.ContainsKey(cc) && !fkColumnNames.Contains(cc))
            {
                throw new InvalidOperationException(
                    $"BuildCteNodeUpsertMerged: conflict column \"{cc}\" for " +
                    $"{owningSchema}.{owningTable} (EntityId={root.EntityId}) is not present " +
                    $"in root.Values and is not a CTE FK column. " +
                    $"The planner did not add this value to edgeValues — check " +
                    $"EmitCompositeMutation generates a case for this field.");
            }
        }

        if (matched.Count == 0)
        {
            var plainSb = new StringBuilder();
            plainSb.Append("INSERT INTO \"").Append(owningSchema)
                   .Append("\".\"").Append(owningTable).Append("\" (");

            var first = true;
            foreach (var v in root.Values)
            {
                if (!first) plainSb.Append(", ");
                first = false;
                plainSb.Append('"').Append(rootCols[v.FieldId]).Append('"');
            }
            plainSb.Append(") VALUES (");

            first = true;
            foreach (var v in root.Values)
            {
                if (!first) plainSb.Append(", ");
                first = false;
                AppendFieldValue(plainSb, root.StorageEntityId, v.FieldId, v.RawValue);
            }
            plainSb.Append(')');

            AppendDoUpdateSetFromNames(
                plainSb, root.EntityId, root.StorageEntityId,
                root.Values, rootCols, conflictCols);

            return plainSb.ToString();
        }

        var sb = new StringBuilder();

        sb.Append("INSERT INTO \"").Append(owningSchema)
          .Append("\".\"").Append(owningTable).Append("\" (");

        var firstCol = true;
        foreach (var v in root.Values)
        {
            if (!firstCol) sb.Append(", ");
            firstCol = false;
            sb.Append('"').Append(rootCols[v.FieldId]).Append('"');
        }
        foreach (var (_, spec) in matched)
        {
            if (!firstCol) sb.Append(", ");
            firstCol = false;
            sb.Append('"').Append(spec.ForeignKeyColumn).Append('"');
        }

        sb.Append(") SELECT ");

        var firstVal = true;
        foreach (var v in root.Values)
        {
            if (!firstVal) sb.Append(", ");
            firstVal = false;
            AppendFieldValue(sb, root.StorageEntityId, v.FieldId, v.RawValue);
        }
        foreach (var (_, spec) in matched)
        {
            if (!firstVal) sb.Append(", ");
            firstVal = false;
            sb.Append(spec.RelatedTableAlias).Append(".\"")
              .Append(spec.RelatedSurrogateIdColumn).Append('"');
        }

        sb.Append(" FROM ");

        for (int i = 0; i < matched.Count; i++)
        {
            var (child, spec) = matched[i];

            if (i == 0)
            {
                sb.Append('"')
                    .Append(_meta.EntitySchema[child.StorageEntityId])
                    .Append("\".\"")
                    .Append(_meta.EntityTable[child.StorageEntityId])
                    .Append("\" ")
                    .Append(spec.RelatedTableAlias);
            }
            else
            {
                sb.Append(" JOIN ")
                    .Append('"')
                    .Append(_meta.EntitySchema[child.StorageEntityId])
                    .Append("\".\"")
                    .Append(_meta.EntityTable[child.StorageEntityId])
                    .Append("\" ")
                    .Append(spec.RelatedTableAlias);

                sb.Append(" ON ")
                    .Append(spec.RelatedTableAlias)
                    .Append(".\"")
                    .Append(spec.RelatedNaturalKeyColumn)
                    .Append("\" = ");

                AppendQuotedValue(
                    sb,
                    child.Values.Length > 0
                        ? child.Values[0].RawValue
                        : string.Empty);
            }
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
            var col = rootCols[v.FieldId];
            if (Array.IndexOf(conflictCols, col) >= 0) continue;
            if (!firstSet) sb.Append(", ");
            firstSet = false;
            sb.Append('"').Append(col).Append("\" = EXCLUDED.\"").Append(col).Append('"');
        }
        foreach (var (_, spec) in matched)
        {
            if (!firstSet) sb.Append(", ");
            firstSet = false;
            sb.Append('"').Append(spec.ForeignKeyColumn)
              .Append("\" = EXCLUDED.\"").Append(spec.ForeignKeyColumn).Append('"');
        }

        if (firstSet)
        {
            sb.Length -= " DO UPDATE SET ".Length;
            sb.Append(" DO NOTHING");
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

            string columnName;

            if (col.Kind == ColumnKind.GraphSynthetic)
            {
                columnName = col.RawColumnName
                             ?? throw new InvalidOperationException(
                                 $"GraphSynthetic column has null RawColumnName. EntityOutputAlias={col.EntityOutputAlias}, ColumnOutputAlias={col.ColumnOutputAlias}");
            }
            else
            {
                var colNames = _meta.EntityColumnName[col.StorageEntityId];

                if (col.ColumnId >= colNames.Length)
                {
                    throw new IndexOutOfRangeException(
                        $"ColumnId {col.ColumnId} out of range for StorageEntityId {col.StorageEntityId} " +
                        $"({_meta.EntityTable[col.StorageEntityId]}, Length={colNames.Length}), " +
                        $"ModelId={col.EntityId} ({_meta.ModelName[col.EntityId][0]})");
                }

                columnName = colNames[col.ColumnId];
            }

            sb.Append("\n    ");
            AppendQuotedIdentifier(sb, col.EntityOutputAlias);
            sb.Append('.').Append('"').Append(columnName).Append('"');
            sb.Append(" AS ");
            AppendQuotedIdentifier(sb, col.ColumnOutputAlias);
        }

        if (first)
            sb.Append("\n    1");

        sb.Append("\nFROM ");
        AppendQualifiedTable(sb, plan.RootStorageEntityId);

        sb.Append(' ');
        AppendQuotedIdentifier(sb, plan.RootOutputAlias);

        foreach (var graphJoin in plan.GraphJoins)
        {
            sb.Append('\n');
            _graphStrategy.AppendGraphJoin(sb, graphJoin, plan.RootOutputAlias);
        }

        foreach (var join in plan.Joins)
        {
            sb.Append('\n');
            AppendJoin(sb, join, plan);
        }

        // Result-joins depend on a graph join's output alias already being
        // present in FROM, so these must be emitted after the GraphJoins loop.
        foreach (var resultJoin in plan.GraphResultJoins)
        {
            sb.Append('\n');
            _graphStrategy.AppendGraphResultJoin(sb, resultJoin);
        }
    }

    private void AppendJoin(StringBuilder sb, in JoinSpec join, in QueryPlan plan)
    {
        var keyword = join.Kind == JoinKind.Left ? "LEFT JOIN" : "JOIN";
        sb.Append(keyword).Append(' ');
        AppendQualifiedTable(sb, join.ToStorageEntityId);
        sb.Append(' ');
        AppendQuotedIdentifier(sb, join.ToOutputAlias);
        sb.Append("\n    ON ");

        var toColName = _meta.EntityColumnName[join.ToStorageEntityId][join.ToColumnId];
        AppendQuotedIdentifier(sb, join.ToOutputAlias);
        sb.Append(".\"").Append(toColName).Append('"');
        sb.Append(" = ");

        if (join.SourceKind == JoinSourceKind.GraphVertex)
        {
            AppendQuotedIdentifier(sb, join.FromGraphAlias);
            sb.Append(".\"").Append(join.FromRawColumnName).Append('"');
        }
        else
        {
            var fromAlias   = ResolveOutputAlias(join.FromEntityId, plan);
            var fromColName = _meta.EntityColumnName[join.FromStorageEntityId][join.FromColumnId];
            AppendQuotedIdentifier(sb, fromAlias);
            sb.Append(".\"").Append(fromColName).Append('"');
        }
    }

    private void AppendQualifiedTable(StringBuilder sb, ushort storageEntityId)
    {
        // Schema/table names come from _meta (compile-time mapping metadata),
        // never client input — no quoting-injection concern here.
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

    /// <summary>
    /// Quotes a SQL identifier (table/column alias), doubling any embedded
    /// double-quote — the standard Postgres identifier escape, parallel to
    /// AppendQuotedValue's single-quote doubling for string literals.
    ///
    /// Not currently reachable with a malicious value: every OutputAlias
    /// passed here originates from HotChocolateAdapter, which derives it
    /// from a parsed GraphQL Name token ([_A-Za-z][_0-9A-Za-z]*) that
    /// cannot contain a double-quote. This guard exists so that invariant
    /// doesn't have to hold for this code to be safe.
    /// </summary>
    private static void AppendQuotedIdentifier(StringBuilder sb, string identifier)
        => AppendQuotedIdentifierStatic(sb, identifier);

    /// <summary>
    /// Static form so IGraphStrategy implementations (which don't inherit
    /// from PostgresSqlWriter) can reuse the same escaping logic rather
    /// than duplicating it.
    /// </summary>
    internal static void AppendQuotedIdentifierStatic(StringBuilder sb, string identifier)
    {
        sb.Append('"').Append(identifier.Replace("\"", "\"\"")).Append('"');
    }

    private void AppendFieldValue(
        StringBuilder sb,
        ushort storageEntityId,
        ushort columnId,
        string rawValue)
    {
        var converted = EnumConversions.TryConvert(storageEntityId, columnId, rawValue);
        if (converted != null)
        {
            sb.Append((string)converted);
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
