using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using CoffeeBeanery.GraphQL.Core.Runtime;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

/// <summary>
/// Converts a QueryPlan or MutationPlan into raw PostgreSQL.
/// Contains zero model-specific knowledge — all names come from
/// IEntityMetaProvider arrays indexed by generated ID constants.
///
/// Thread-safe: all mutable state is on the stack (StringBuilder
/// is the only allocation per call).
/// </summary>
public sealed class PostgresSqlWriter
{
    private readonly IEntityMetaProvider _meta;

    public PostgresSqlWriter(IEntityMetaProvider meta)
    {
        _meta = meta;
    }

    // ---------------------------------------------------------------
    // SELECT
    // ---------------------------------------------------------------

    public string WriteSelect(
        in QueryPlan plan,
        string? whereClause = null,
        string? orderByClause = null)
    {
        var sb = new StringBuilder(512);

        sb.Append("SELECT DISTINCT");

        var first = true;
        foreach (var col in plan.Columns)
        {
            if (!first) sb.Append(',');
            first = false;

            sb.Append("\n    ");
            sb.Append('"').Append(col.EntityOutputAlias).Append('"');
            sb.Append('.');
            sb.Append('"').Append(_meta.ColumnName[col.EntityId][col.ColumnId]).Append('"');
            sb.Append(" AS ");
            sb.Append('"').Append(col.ColumnOutputAlias).Append('"');
        }

        if (first)
            sb.Append("\n    1");

        sb.Append('\n');
        sb.Append("FROM ");
        AppendQualifiedTable(sb, plan.RootEntityId);
        sb.Append(' ');
        sb.Append('"').Append(plan.RootOutputAlias).Append('"');

        foreach (var join in plan.Joins)
        {
            sb.Append('\n');
            AppendJoin(sb, join, plan);
        }

        if (!string.IsNullOrWhiteSpace(whereClause))
        {
            sb.Append('\n');
            sb.Append("WHERE ").Append(whereClause);
        }

        if (!string.IsNullOrWhiteSpace(orderByClause))
        {
            sb.Append('\n');
            sb.Append("ORDER BY ").Append(orderByClause);
        }

        return sb.ToString();
    }

    // ---------------------------------------------------------------
    // UPSERT
    // ---------------------------------------------------------------

    public string WriteUpserts(
        in MutationPlan plan,
        ImmutableDictionary<ushort, string>? conflictColumnOverrides = null)
    {
        if (plan.Rows.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder(256 * plan.Rows.Length);

        foreach (var row in plan.Rows)
        {
            WriteUpsertRow(sb, row, conflictColumnOverrides);
            sb.Append(";\n");
        }

        return sb.ToString();
    }

    // ---------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------

    private void AppendJoin(StringBuilder sb, in JoinSpec join, in QueryPlan plan)
    {
        var keyword = join.Kind == JoinKind.Left ? "LEFT JOIN" : "JOIN";
        sb.Append(keyword).Append(' ');
        AppendQualifiedTable(sb, join.ToEntityId);
        sb.Append(' ');
        sb.Append('"').Append(join.ToOutputAlias).Append('"');
        sb.Append('\n');
        sb.Append("    ON ");

        var fromAlias   = ResolveOutputAlias(join.FromEntityId, plan);
        var fromColName = _meta.ColumnName[join.FromEntityId][join.FromColumnId];
        var toColName   = _meta.ColumnName[join.ToEntityId][join.ToColumnId];

        sb.Append('"').Append(join.ToOutputAlias).Append('"');
        sb.Append('.').Append('"').Append(toColName).Append('"');
        sb.Append(" = ");
        sb.Append('"').Append(fromAlias).Append('"');
        sb.Append('.').Append('"').Append(fromColName).Append('"');
    }

    private string ResolveOutputAlias(ushort entityId, in QueryPlan plan)
    {
        if (entityId == plan.RootEntityId)
            return plan.RootOutputAlias;

        foreach (var j in plan.Joins)
        {
            if (j.ToEntityId == entityId)
                return j.ToOutputAlias;
        }

        return _meta.Table[entityId];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AppendQualifiedTable(StringBuilder sb, ushort entityId)
    {
        sb.Append('"').Append(_meta.Schema[entityId]).Append('"');
        sb.Append('.');
        sb.Append('"').Append(_meta.Table[entityId]).Append('"');
    }

    private void WriteUpsertRow(
        StringBuilder sb,
        in UpsertRow row,
        ImmutableDictionary<ushort, string>? conflictColumnOverrides)
    {
        if (row.Values.IsEmpty)
            return;

        var schema = row.SchemaOverride ?? _meta.Schema[row.EntityId];
        var table  = row.TableOverride  ?? _meta.Table[row.EntityId];

        sb.Append("INSERT INTO ");
        sb.Append('"').Append(schema).Append('"');
        sb.Append('.');
        sb.Append('"').Append(table).Append('"');
        sb.Append(" (");

        for (var i = 0; i < row.Values.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"');
            sb.Append(_meta.ColumnName[row.EntityId][row.Values[i].FieldId]);
            sb.Append('"');
        }

        sb.Append(") VALUES (");

        for (var i = 0; i < row.Values.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('\'');
            sb.Append(EscapeValue(row.Values[i].RawValue));
            sb.Append('\'');
        }

        sb.Append(')');

        string conflictCol;
        if (conflictColumnOverrides is not null &&
            conflictColumnOverrides.TryGetValue(row.EntityId, out var overrideCol))
        {
            conflictCol = overrideCol;
        }
        else
        {
            conflictCol = _meta.ColumnName[row.EntityId][row.Values[0].FieldId];
        }

        sb.Append($" ON CONFLICT (\"{conflictCol}\") DO UPDATE SET ");

        var firstUpdate = true;
        foreach (var v in row.Values)
        {
            var colName = _meta.ColumnName[row.EntityId][v.FieldId];
            if (colName == conflictCol) continue;

            if (!firstUpdate) sb.Append(", ");
            firstUpdate = false;

            sb.Append('"').Append(colName).Append('"');
            sb.Append(" = EXCLUDED.");
            sb.Append('"').Append(colName).Append('"');
        }

        if (firstUpdate)
        {
            sb.Length -= "DO UPDATE SET ".Length;
            sb.Append("DO NOTHING");
        }
    }

    private static ReadOnlySpan<char> EscapeValue(string value) =>
        value.Replace("'", "''").AsSpan();
}