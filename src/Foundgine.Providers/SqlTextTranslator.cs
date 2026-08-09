using System.Text;
using Foundgine.Execution.Contracts;
using Foundgine.Metadata;

namespace Foundgine.Providers;

/// <summary>
/// One selected column in a <see cref="SqlTranslation"/>'s result set, in
/// the same left-to-right order the generated SELECT list uses. Lets
/// <see cref="SqlExecutionProvider"/> map each ADO.NET reader ordinal back
/// to "which entity, which column" without re-parsing SQL.
/// </summary>
public sealed record SqlColumnMap(EntityMetadata Entity, ushort ColumnId, string ResultAlias);

/// <summary>A compiled SQL statement plus the column map needed to read its results.</summary>
public sealed record SqlTranslation(string CommandText, IReadOnlyList<SqlColumnMap> Columns);

/// <summary>
/// Turns a physical <see cref="ProviderPlan"/> (SQL provider nodes only)
/// into a single parameterless <c>SELECT ... FROM ... JOIN ...</c>
/// statement. Deliberately minimal — no WHERE/ORDER BY/paging yet, since
/// those are explicitly "🟡 NEXT" items, not required for the first E2E.
/// </summary>
public static class SqlTextTranslator
{
    public static SqlTranslation Translate(ProviderPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var (root, projectedFields) = UnwrapProjection(plan.Root);

        var orderedAliases = new List<(EntityMetadata Entity, string Alias)>();
        var aliasByEntity = new Dictionary<EntityMetadata, string>();
        var counter = 0;

        var fromClause = BuildFrom(root, orderedAliases, aliasByEntity, ref counter);

        var selectItems = new List<string>();
        var columns = new List<SqlColumnMap>();

        if (projectedFields is not null)
        {
            foreach (var field in projectedFields)
                AddColumn(field.Source.Entity, field.Source.ColumnId, aliasByEntity, selectItems, columns);
        }
        else
        {
            foreach (var (entity, _) in orderedAliases)
                foreach (var column in entity.Columns)
                    AddColumn(entity, column.Id.Value, aliasByEntity, selectItems, columns);
        }

        if (selectItems.Count == 0)
        {
            throw new InvalidOperationException(
                "The compiled plan selects no columns — every scanned entity has an empty " +
                "column list and no projection was supplied.");
        }

        var sql = $"SELECT {string.Join(", ", selectItems)} FROM {fromClause}";
        return new SqlTranslation(sql, columns);
    }

    private static (ProviderNode Root, IReadOnlyList<FieldBinding>? Fields) UnwrapProjection(ProviderNode node) =>
        node is SqlProjectionNode projection
            ? (projection.Source, projection.Fields)
            : (node, null);

    private static void AddColumn(
        EntityMetadata entity,
        ushort columnId,
        Dictionary<EntityMetadata, string> aliasByEntity,
        List<string> selectItems,
        List<SqlColumnMap> columns)
    {
        if (!aliasByEntity.TryGetValue(entity, out var alias))
        {
            throw new InvalidOperationException(
                $"Cannot select a column from entity '{entity.Name}': it was never scanned " +
                "anywhere in the FROM/JOIN clause. Every entity a projection references must " +
                "also appear as a Scan somewhere in the plan.");
        }

        var columnName = ColumnName(entity, columnId);
        var resultAlias = $"{alias}_{columnName}";

        selectItems.Add($"{alias}.{Quote(columnName)} AS {Quote(resultAlias)}");
        columns.Add(new SqlColumnMap(entity, columnId, resultAlias));
    }

    private static string BuildFrom(
        ProviderNode node,
        List<(EntityMetadata Entity, string Alias)> orderedAliases,
        Dictionary<EntityMetadata, string> aliasByEntity,
        ref int counter)
    {
        switch (node)
        {
            case SqlScanNode scan:
            {
                var alias = $"t{counter++}";
                orderedAliases.Add((scan.Entity, alias));
                aliasByEntity[scan.Entity] = alias;
                return $"{Quote(scan.Entity.Name)} AS {alias}";
            }

            case SqlJoinNode join:
            {
                // Both sides must be resolved (and their aliases registered)
                // before the ON clause is built, since the join condition's
                // Left/Right columns describe foreign-key direction, not
                // which side of *this* node they were scanned on.
                var left = BuildFrom(join.Left, orderedAliases, aliasByEntity, ref counter);
                var right = BuildFrom(join.Right, orderedAliases, aliasByEntity, ref counter);

                var keyword = JoinKeyword(join.Join.Kind);
                var leftAlias = AliasOf(join.Join.Condition.Left.Entity, aliasByEntity);
                var rightAlias = AliasOf(join.Join.Condition.Right.Entity, aliasByEntity);
                var leftColumn = ColumnName(join.Join.Condition.Left.Entity, join.Join.Condition.Left.ColumnId);
                var rightColumn = ColumnName(join.Join.Condition.Right.Entity, join.Join.Condition.Right.ColumnId);

                return $"{left} {keyword} {right} ON " +
                       $"{leftAlias}.{Quote(leftColumn)} = {rightAlias}.{Quote(rightColumn)}";
            }

            default:
                throw new NotSupportedException(
                    $"{nameof(SqlTextTranslator)} cannot build a FROM clause for a " +
                    $"{node.GetType().Name}. Only {nameof(SqlScanNode)} and {nameof(SqlJoinNode)} " +
                    "describe SQL table sources.");
        }
    }

    private static string AliasOf(EntityMetadata entity, Dictionary<EntityMetadata, string> aliasByEntity)
    {
        if (!aliasByEntity.TryGetValue(entity, out var alias))
        {
            throw new InvalidOperationException(
                $"Join condition references entity '{entity.Name}', but it was never scanned " +
                "in this plan.");
        }

        return alias;
    }

    private static string ColumnName(EntityMetadata entity, ushort columnId)
    {
        foreach (var column in entity.Columns)
        {
            if (column.Id.Value == columnId)
                return column.Name;
        }

        throw new InvalidOperationException(
            $"Entity '{entity.Name}' has no column with id {columnId}.");
    }

    private static string JoinKeyword(JoinKind kind) => kind switch
    {
        JoinKind.Inner => "INNER JOIN",
        JoinKind.Left => "LEFT JOIN",
        JoinKind.Right => "RIGHT JOIN",
        JoinKind.Full => "FULL JOIN",
        _ => throw new NotSupportedException($"Unknown join kind '{kind}'."),
    };

    /// <summary>
    /// Double-quoted ("ANSI") identifier quoting — supported by SQLite (the
    /// first real backend this targets) as well as Postgres and SQL Server,
    /// so this doesn't need to become provider-specific yet.
    /// </summary>
    private static string Quote(string identifier) =>
        new StringBuilder(identifier.Length + 2)
            .Append('"')
            .Append(identifier.Replace("\"", "\"\""))
            .Append('"')
            .ToString();
}
