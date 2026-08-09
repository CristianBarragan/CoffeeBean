using System.Runtime.CompilerServices;
using System.Text;
using Foundgine.Execution.Contracts;
using Foundgine.Metadata;

namespace Foundgine.Providers;

/// <summary>
/// One selected column in a SQL result set, in the same left-to-right order
/// the generated SELECT list uses.
///
/// OccurrenceIndex is critical when the same EntityMetadata appears more
/// than once in a provider plan. For example:
///
///     Employee #0 -> Employee #1 -> Employee #2
///
/// All three occurrences have the same EntityId, but they must remain
/// independently addressable in the result.
/// </summary>
public sealed record SqlColumnMap(
    EntityMetadata Entity,
    ushort ColumnId,
    string ResultAlias,
    int OccurrenceIndex);

/// <summary>
/// A compiled SQL statement plus the column map needed to read its results.
/// </summary>
public sealed record SqlTranslation(
    string CommandText,
    IReadOnlyList<SqlColumnMap> Columns);

/// <summary>
/// Turns a physical SQL provider plan into a single SELECT ... FROM ...
/// JOIN ... statement.
///
/// The translator is deliberately minimal. WHERE, ORDER BY, paging,
/// parameters, aggregation and other SQL features can be added later.
///
/// The important responsibility here is preserving occurrence identity:
/// two SqlScanNode instances containing the same EntityMetadata are still
/// two different result occurrences.
/// </summary>
public static class SqlTextTranslator
{
    public static SqlTranslation Translate(ProviderPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var (root, projectedFields) = UnwrapProjection(plan.Root);

        var orderedOccurrences =
            new List<(SqlScanNode Occurrence, string Alias, int OccurrenceIndex)>();

        var aliasByOccurrence =
            new Dictionary<SqlScanNode, string>(
                ScanNodeReferenceComparer.Instance);

        var occurrenceIndexByScan =
            new Dictionary<SqlScanNode, int>(
                ScanNodeReferenceComparer.Instance);

        // Entity-keyed fallback is intentionally retained only for plans
        // which do not provide occurrence references.
        var aliasByEntity =
            new Dictionary<EntityMetadata, string>();

        var counter = 0;
        var occurrenceCounter = 0;

        var fromClause = BuildFrom(
            root,
            orderedOccurrences,
            aliasByOccurrence,
            occurrenceIndexByScan,
            aliasByEntity,
            ref counter,
            ref occurrenceCounter);

        var selectItems = new List<string>();
        var columns = new List<SqlColumnMap>();

        if (projectedFields is not null)
        {
            foreach (var field in projectedFields)
            {
                var occurrenceIndex =
                    ResolveProjectionOccurrenceIndex(
                        field.Source.Entity,
                        orderedOccurrences);

                var alias =
                    ResolveProjectionAlias(
                        field.Source.Entity,
                        occurrenceIndex,
                        orderedOccurrences,
                        aliasByEntity);

                AddColumn(
                    field.Source.Entity,
                    field.Source.ColumnId,
                    alias,
                    occurrenceIndex,
                    selectItems,
                    columns);
            }
        }
        else
        {
            foreach (var (occurrence, alias, occurrenceIndex) in orderedOccurrences)
            {
                foreach (var column in occurrence.Entity.Columns)
                {
                    AddColumn(
                        occurrence.Entity,
                        column.Id.Value,
                        alias,
                        occurrenceIndex,
                        selectItems,
                        columns);
                }
            }
        }

        if (selectItems.Count == 0)
        {
            throw new InvalidOperationException(
                "The compiled plan selects no columns — every scanned entity has an empty " +
                "column list and no projection was supplied.");
        }

        var sql =
            $"SELECT {string.Join(", ", selectItems)} FROM {fromClause}";

        return new SqlTranslation(sql, columns);
    }

    private static string BuildFrom(
        ProviderNode node,
        List<(SqlScanNode Occurrence, string Alias, int OccurrenceIndex)> orderedOccurrences,
        Dictionary<SqlScanNode, string> aliasByOccurrence,
        Dictionary<SqlScanNode, int> occurrenceIndexByScan,
        Dictionary<EntityMetadata, string> aliasByEntity,
        ref int counter,
        ref int occurrenceCounter)
    {
        switch (node)
        {
            case SqlScanNode scan:
            {
                var alias = $"t{counter++}";
                var occurrenceIndex = occurrenceCounter++;

                orderedOccurrences.Add(
                    (scan, alias, occurrenceIndex));

                // IMPORTANT:
                // SqlScanNode is a record, therefore default record equality
                // is not suitable here. Two separate Employee occurrences
                // can otherwise compare equal.
                aliasByOccurrence[scan] = alias;
                occurrenceIndexByScan[scan] = occurrenceIndex;

                // Entity-keyed lookup remains only as a fallback for
                // non-occurrence-aware hand-built plans.
                aliasByEntity[scan.Entity] = alias;

                return
                    $"{Quote(scan.Entity.EffectiveStorageName)} AS {alias}";
            }

            case SqlJoinNode join:
            {
                // Register both sides before resolving the ON condition.
                var left = BuildFrom(
                    join.Left,
                    orderedOccurrences,
                    aliasByOccurrence,
                    occurrenceIndexByScan,
                    aliasByEntity,
                    ref counter,
                    ref occurrenceCounter);

                var right = BuildFrom(
                    join.Right,
                    orderedOccurrences,
                    aliasByOccurrence,
                    occurrenceIndexByScan,
                    aliasByEntity,
                    ref counter,
                    ref occurrenceCounter);

                var keyword = JoinKeyword(join.Join.Kind);

                var leftAlias = ResolveConditionAlias(
                    join.Join.Condition.Left,
                    join,
                    isConditionLeft: true,
                    aliasByOccurrence,
                    aliasByEntity);

                var rightAlias = ResolveConditionAlias(
                    join.Join.Condition.Right,
                    join,
                    isConditionLeft: false,
                    aliasByOccurrence,
                    aliasByEntity);

                var leftColumn =
                    ColumnName(
                        join.Join.Condition.Left.Entity,
                        join.Join.Condition.Left.ColumnId);

                var rightColumn =
                    ColumnName(
                        join.Join.Condition.Right.Entity,
                        join.Join.Condition.Right.ColumnId);

                return
                    $"{left} {keyword} {right} ON " +
                    $"{leftAlias}.{Quote(leftColumn)} = " +
                    $"{rightAlias}.{Quote(rightColumn)}";
            }

            default:
                throw new NotSupportedException(
                    $"{nameof(SqlTextTranslator)} cannot build a FROM clause for a " +
                    $"{node.GetType().Name}. Only {nameof(SqlScanNode)} and " +
                    $"{nameof(SqlJoinNode)} describe SQL table sources.");
        }
    }

    /// <summary>
    /// Resolves the alias belonging to a join condition side.
    ///
    /// When occurrence references are present, they are authoritative.
    ///
    /// For a normal join:
    ///
    ///     Customer -> Account
    ///
    /// the condition's Customer reference resolves to the Customer
    /// occurrence and Account resolves to the Account occurrence.
    ///
    /// For a self-join:
    ///
    ///     Employee #0 -> Employee #1
    ///
    /// both sides have the same EntityMetadata. In that case the position
    /// of the condition side determines the occurrence:
    ///
    ///     condition.Left  -> LeftOccurrence
    ///     condition.Right -> RightOccurrence
    ///
    /// This produces:
    ///
    ///     t0."ManagerId" = t1."Id"
    ///
    /// and, for the next join:
    ///
    ///     t1."ManagerId" = t2."Id"
    /// </summary>
    private static string ResolveConditionAlias(
        ColumnReference conditionSide,
        SqlJoinNode join,
        bool isConditionLeft,
        Dictionary<SqlScanNode, string> aliasByOccurrence,
        Dictionary<EntityMetadata, string> aliasByEntity)
    {
        if (join.LeftOccurrence is { } leftOccurrence &&
            join.RightOccurrence is { } rightOccurrence)
        {
            var leftMatches =
                Equals(leftOccurrence.Entity, conditionSide.Entity);

            var rightMatches =
                Equals(rightOccurrence.Entity, conditionSide.Entity);

            if (leftMatches && !rightMatches)
            {
                return AliasForOccurrence(
                    leftOccurrence,
                    aliasByOccurrence);
            }

            if (rightMatches && !leftMatches)
            {
                return AliasForOccurrence(
                    rightOccurrence,
                    aliasByOccurrence);
            }

            if (leftMatches && rightMatches)
            {
                var occurrence =
                    isConditionLeft
                        ? leftOccurrence
                        : rightOccurrence;

                return AliasForOccurrence(
                    occurrence,
                    aliasByOccurrence);
            }
        }

        // Backward-compatible fallback for hand-built plans that do not
        // provide occurrence references.
        return AliasOf(
            conditionSide.Entity,
            aliasByEntity);
    }

    private static string AliasForOccurrence(
        SqlScanNode occurrence,
        Dictionary<SqlScanNode, string> aliasByOccurrence)
    {
        if (!aliasByOccurrence.TryGetValue(
                occurrence,
                out var alias))
        {
            throw new InvalidOperationException(
                $"The SQL join references an occurrence of entity " +
                $"'{occurrence.Entity.Name}' that was not registered while " +
                "building the FROM clause. This indicates that the provider " +
                "plan contains an occurrence reference which is not part of " +
                "the compiled SQL tree.");
        }

        return alias;
    }

    private static string AliasOf(
        EntityMetadata entity,
        Dictionary<EntityMetadata, string> aliasByEntity)
    {
        if (!aliasByEntity.TryGetValue(
                entity,
                out var alias))
        {
            throw new InvalidOperationException(
                $"Join condition references entity '{entity.Name}', but it was " +
                "never scanned in this plan.");
        }

        return alias;
    }

    private static int ResolveProjectionOccurrenceIndex(
        EntityMetadata entity,
        List<(SqlScanNode Occurrence, string Alias, int OccurrenceIndex)> occurrences)
    {
        var matches =
            occurrences
                .Where(x => Equals(x.Occurrence.Entity, entity))
                .ToArray();

        if (matches.Length == 0)
        {
            throw new InvalidOperationException(
                $"Projection references entity '{entity.Name}', but that entity " +
                "was not scanned in the SQL plan.");
        }

        // FieldBinding currently identifies EntityMetadata + ColumnId, not
        // a specific occurrence. Therefore a projection over a repeated
        // entity is inherently ambiguous until FieldBinding itself carries
        // occurrence identity.
        //
        // Preserve the existing behavior for now by selecting the first
        // occurrence.
        return matches[0].OccurrenceIndex;
    }

    private static string ResolveProjectionAlias(
        EntityMetadata entity,
        int occurrenceIndex,
        List<(SqlScanNode Occurrence, string Alias, int OccurrenceIndex)> occurrences,
        Dictionary<EntityMetadata, string> aliasByEntity)
    {
        foreach (var occurrence in occurrences)
        {
            if (occurrence.OccurrenceIndex == occurrenceIndex &&
                Equals(occurrence.Occurrence.Entity, entity))
            {
                return occurrence.Alias;
            }
        }

        return AliasOf(entity, aliasByEntity);
    }

    private static void AddColumn(
        EntityMetadata entity,
        ushort columnId,
        string alias,
        int occurrenceIndex,
        List<string> selectItems,
        List<SqlColumnMap> columns)
    {
        var domainColumnName =
            DomainColumnName(entity, columnId);

        var physicalColumnName =
            ColumnName(entity, columnId);

        var resultAlias =
            $"{alias}_{domainColumnName}";

        selectItems.Add(
            $"{alias}.{Quote(physicalColumnName)} AS {Quote(resultAlias)}");

        columns.Add(
            new SqlColumnMap(
                entity,
                columnId,
                resultAlias,
                occurrenceIndex));
    }

    private static (ProviderNode Root, IReadOnlyList<FieldBinding>? Fields)
        UnwrapProjection(ProviderNode node) =>
        node is SqlProjectionNode projection
            ? (projection.Source, projection.Fields)
            : (node, null);

    private static string ColumnName(
        EntityMetadata entity,
        ushort columnId)
    {
        foreach (var column in entity.Columns)
        {
            if (column.Id.Value == columnId)
                return column.EffectiveStorageName;
        }

        throw new InvalidOperationException(
            $"Entity '{entity.Name}' has no column with id {columnId}.");
    }

    private static string DomainColumnName(
        EntityMetadata entity,
        ushort columnId)
    {
        foreach (var column in entity.Columns)
        {
            if (column.Id.Value == columnId)
                return column.Name;
        }

        throw new InvalidOperationException(
            $"Entity '{entity.Name}' has no column with id {columnId}.");
    }

    private static string JoinKeyword(JoinKind kind) =>
        kind switch
        {
            JoinKind.Inner => "INNER JOIN",
            JoinKind.Left => "LEFT JOIN",
            JoinKind.Right => "RIGHT JOIN",
            JoinKind.Full => "FULL JOIN",
            _ => throw new NotSupportedException(
                $"Unknown join kind '{kind}'."),
        };

    /// <summary>
    /// Double-quoted identifier quoting.
    ///
    /// Supported by SQLite and PostgreSQL and accepted by SQL Server for
    /// identifiers, keeping the first provider implementation independent
    /// of a backend-specific quoting abstraction.
    /// </summary>
    private static string Quote(string identifier) =>
        new StringBuilder(identifier.Length + 2)
            .Append('"')
            .Append(identifier.Replace("\"", "\"\""))
            .Append('"')
            .ToString();

    /// <summary>
    /// Reference-identity comparer for SqlScanNode.
    ///
    /// SqlScanNode is a record, so its default equality is structural.
    /// Repeated occurrences of the same entity therefore need reference
    /// identity here:
    ///
    ///     Employee #0 -> t0
    ///     Employee #1 -> t1
    ///     Employee #2 -> t2
    /// </summary>
    private sealed class ScanNodeReferenceComparer
        : IEqualityComparer<SqlScanNode>
    {
        public static readonly ScanNodeReferenceComparer Instance = new();

        public bool Equals(
            SqlScanNode? x,
            SqlScanNode? y) =>
            ReferenceEquals(x, y);

        public int GetHashCode(
            SqlScanNode obj) =>
            RuntimeHelpers.GetHashCode(obj);
    }
}