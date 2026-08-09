using System.Runtime.CompilerServices;
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

        var orderedOccurrences = new List<(SqlScanNode Occurrence, string Alias)>();
        var aliasByOccurrence = new Dictionary<SqlScanNode, string>(ScanNodeReferenceComparer.Instance);
        // Entity-keyed fallback, kept only for plans that don't carry
        // occurrence references (hand-built JoinNode trees that never scan
        // the same entity twice) and for field projections, which name a
        // column by EntityMetadata alone. See AliasOf's remarks.
        var aliasByEntity = new Dictionary<EntityMetadata, string>();
        var counter = 0;

        var fromClause = BuildFrom(root, orderedOccurrences, aliasByOccurrence, aliasByEntity, ref counter);

        var selectItems = new List<string>();
        var columns = new List<SqlColumnMap>();

        if (projectedFields is not null)
        {
            foreach (var field in projectedFields)
                AddColumn(field.Source.Entity, field.Source.ColumnId, AliasOf(field.Source.Entity, aliasByEntity), selectItems, columns);
        }
        else
        {
            foreach (var (occurrence, alias) in orderedOccurrences)
                foreach (var column in occurrence.Entity.Columns)
                    AddColumn(occurrence.Entity, column.Id.Value, alias, selectItems, columns);
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
        string alias,
        List<string> selectItems,
        List<SqlColumnMap> columns)
    {
        var domainColumnName = DomainColumnName(entity, columnId);
        var physicalColumnName = ColumnName(entity, columnId);
        var resultAlias = $"{alias}_{domainColumnName}";

        selectItems.Add($"{alias}.{Quote(physicalColumnName)} AS {Quote(resultAlias)}");
        columns.Add(new SqlColumnMap(entity, columnId, resultAlias));
    }

    private static string BuildFrom(
        ProviderNode node,
        List<(SqlScanNode Occurrence, string Alias)> orderedOccurrences,
        Dictionary<SqlScanNode, string> aliasByOccurrence,
        Dictionary<EntityMetadata, string> aliasByEntity,
        ref int counter)
    {
        switch (node)
        {
            case SqlScanNode scan:
            {
                var alias = $"t{counter++}";
                orderedOccurrences.Add((scan, alias));
                aliasByOccurrence[scan] = alias;
                aliasByEntity[scan.Entity] = alias;
                return $"{Quote(scan.Entity.EffectiveStorageName)} AS {alias}";
            }

            case SqlJoinNode join:
            {
                // Both sides must be resolved (and their aliases registered)
                // before the ON clause is built, since the join condition's
                // Left/Right columns describe foreign-key direction, not
                // which side of *this* node they were scanned on.
                var left = BuildFrom(join.Left, orderedOccurrences, aliasByOccurrence, aliasByEntity, ref counter);
                var right = BuildFrom(join.Right, orderedOccurrences, aliasByOccurrence, aliasByEntity, ref counter);

                var keyword = JoinKeyword(join.Join.Kind);
                var leftAlias = ResolveConditionAlias(
                    join.Join.Condition.Left, join, isConditionLeft: true, aliasByOccurrence, aliasByEntity);
                var rightAlias = ResolveConditionAlias(
                    join.Join.Condition.Right, join, isConditionLeft: false, aliasByOccurrence, aliasByEntity);
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

    /// <summary>
    /// Resolves which alias a join condition's column reference (<see
    /// cref="JoinCondition.Left"/> or <see cref="JoinCondition.Right"/>)
    /// binds to for a specific <see cref="SqlJoinNode"/>.
    ///
    /// When <see cref="SqlJoinNode.LeftOccurrence"/>/<see cref="SqlJoinNode.RightOccurrence"/>
    /// are populated (every join <see cref="SqlPlanCompiler"/> compiles
    /// from a <see cref="Foundgine.Builders.CompositeNode"/> sets these),
    /// resolution prefers occurrence identity over entity type:
    ///
    ///  - If exactly one of the two occurrences has the condition side's
    ///    entity, that's an unambiguous match (the common case — different
    ///    entities on each side of the join).
    ///  - If BOTH occurrences share the condition side's entity (a
    ///    self-join, e.g. <c>Employee -> Manager</c>), entity identity
    ///    alone can't disambiguate them, since <see cref="ColumnReference.Entity"/>
    ///    is the same <see cref="EntityMetadata"/> instance either way. In
    ///    that case resolution falls back to positional correspondence:
    ///    <see cref="JoinCondition.Left"/> binds to <see cref="SqlJoinNode.LeftOccurrence"/>
    ///    and <see cref="JoinCondition.Right"/> binds to <see cref="SqlJoinNode.RightOccurrence"/>
    ///    — which is exactly how <see cref="SqlPlanCompiler"/> and any
    ///    other occurrence-aware caller is expected to construct a
    ///    self-referencing join's condition and occurrences together.
    ///
    /// When occurrence references are absent (a hand-built
    /// <see cref="SqlJoinNode"/> that predates this fix, or one that
    /// deliberately bypasses <see cref="SqlPlanCompiler"/>), this falls all
    /// the way back to <see cref="AliasOf"/>'s entity-type lookup — correct
    /// as long as that plan doesn't itself scan the same entity twice.
    /// </summary>
    private static string ResolveConditionAlias(
        ColumnReference conditionSide,
        SqlJoinNode join,
        bool isConditionLeft,
        Dictionary<SqlScanNode, string> aliasByOccurrence,
        Dictionary<EntityMetadata, string> aliasByEntity)
    {
        if (join.LeftOccurrence is { } leftOccurrence && join.RightOccurrence is { } rightOccurrence)
        {
            var leftMatches = Equals(leftOccurrence.Entity, conditionSide.Entity);
            var rightMatches = Equals(rightOccurrence.Entity, conditionSide.Entity);

            if (leftMatches && !rightMatches)
                return aliasByOccurrence[leftOccurrence];

            if (rightMatches && !leftMatches)
                return aliasByOccurrence[rightOccurrence];

            if (leftMatches && rightMatches)
                return aliasByOccurrence[isConditionLeft ? leftOccurrence : rightOccurrence];
        }

        return AliasOf(conditionSide.Entity, aliasByEntity);
    }

    /// <summary>
    /// Entity-type-keyed alias lookup — the pre-occurrence-tracking
    /// resolution strategy, kept as the fallback <see cref="ResolveConditionAlias"/>
    /// uses when a join has no occurrence references, and as the only
    /// resolution strategy for explicit field projections (<see cref="FieldBinding"/>
    /// names a column by <see cref="EntityMetadata"/> alone, with no
    /// occurrence to disambiguate). Correct exactly when the plan being
    /// translated never scans the same entity more than once.
    /// </summary>
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
                return column.EffectiveStorageName;
        }

        throw new InvalidOperationException(
            $"Entity '{entity.Name}' has no column with id {columnId}.");
    }

    private static string DomainColumnName(EntityMetadata entity, ushort columnId)
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

    /// <summary>
    /// Reference-identity comparer for <see cref="SqlScanNode"/>. Deliberately
    /// NOT the default record equality: two distinct occurrences of the same
    /// entity (e.g. two <c>Employee</c> scans in a self-join) are structurally
    /// equal records — same <see cref="EntityMetadata"/> — but must still be
    /// tracked as separate dictionary entries with separate aliases.
    /// </summary>
    private sealed class ScanNodeReferenceComparer : IEqualityComparer<SqlScanNode>
    {
        public static readonly ScanNodeReferenceComparer Instance = new();

        public bool Equals(SqlScanNode? x, SqlScanNode? y) => ReferenceEquals(x, y);

        public int GetHashCode(SqlScanNode obj) => RuntimeHelpers.GetHashCode(obj);
    }
}