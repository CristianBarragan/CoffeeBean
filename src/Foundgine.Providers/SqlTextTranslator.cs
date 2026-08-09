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
/// One bound SQL parameter — a name (e.g. <c>@p0</c>) plus its value.
/// <see cref="Foundgine.Providers.SqlExecutionProvider"/> binds these onto
/// the ADO.NET command instead of the translator ever interpolating a
/// filter value directly into <see cref="SqlTranslation.CommandText"/>.
/// </summary>
public sealed record SqlParameter(string Name, object? Value);

/// <summary>
/// A compiled SQL statement plus the column map needed to read its results
/// and the parameters (from WHERE-clause filter values) it references.
/// </summary>
public sealed record SqlTranslation(
    string CommandText,
    IReadOnlyList<SqlColumnMap> Columns,
    IReadOnlyList<SqlParameter> Parameters);

/// <summary>
/// One compiled INSERT/UPDATE/DELETE statement plus the parameters (from
/// mutation column values and/or WHERE-clause filter values) it references,
/// and which entity it targets (so
/// <see cref="Foundgine.Providers.SqlExecutionProvider"/> can report
/// <see cref="Foundgine.Execution.Contracts.MutationResult"/> per operation).
/// </summary>
public sealed record SqlMutationStatement(
    EntityId EntityId,
    string CommandText,
    IReadOnlyList<SqlParameter> Parameters);

/// <summary>
/// The mutation counterpart of <see cref="SqlTranslation"/>: one
/// <see cref="SqlMutationStatement"/> per
/// <see cref="Foundgine.Execution.Contracts.ProviderMutationPlan.Operations"/>
/// entry, in the same order, so
/// <see cref="Foundgine.Providers.SqlExecutionProvider"/> can execute them as
/// one atomic unit and still report a result per operation.
/// </summary>
public sealed record SqlMutationTranslation(
    IReadOnlyList<SqlMutationStatement> Statements);

/// <summary>
/// Turns a physical SQL provider plan into a single SELECT ... FROM ...
/// JOIN ... [WHERE ...] [ORDER BY ...] [LIMIT ... OFFSET ...] statement.
///
/// Aggregation and other SQL features can still be added later. WHERE,
/// ORDER BY, and LIMIT/OFFSET are handled by unwrapping SqlFilterNode /
/// SqlSortNode / SqlPageNode off the plan (in whatever order they were
/// nested — see <see cref="Unwrap"/>) before building the FROM clause,
/// then resolving each filter/sort column's alias the same way projected
/// columns already are.
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

        var (root, projectedFields, filter, sortTerms, page) = Unwrap(plan.Root);

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

        var parameters = new List<SqlParameter>();

        var whereClause = filter is null
            ? null
            : BuildFilterExpression(
                filter,
                orderedOccurrences,
                aliasByEntity,
                parameters);

        var orderByClause = BuildOrderBy(
            sortTerms,
            orderedOccurrences,
            aliasByEntity);

        var limitOffsetClause = BuildLimitOffset(page);

        var sql =
            $"SELECT {string.Join(", ", selectItems)} FROM {fromClause}";

        if (whereClause is not null)
            sql += $" WHERE {whereClause}";

        if (orderByClause is not null)
            sql += $" ORDER BY {orderByClause}";

        if (limitOffsetClause is not null)
            sql += $" {limitOffsetClause}";

        return new SqlTranslation(sql, columns, parameters);
    }

    /// <summary>
    /// Turns a physical SQL mutation plan into one INSERT/UPDATE/DELETE
    /// statement per operation — the mutation counterpart of
    /// <see cref="Translate(ProviderPlan)"/>. Every operation targets exactly
    /// one table (no aliases, no joins), unlike the read side.
    /// </summary>
    public static SqlMutationTranslation TranslateMutation(ProviderMutationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var statements = plan.Operations
            .Select(TranslateMutationOperation)
            .ToArray();

        return new SqlMutationTranslation(statements);
    }

    private static SqlMutationStatement TranslateMutationOperation(ProviderMutationNode node) => node switch
    {
        SqlInsertNode insert => TranslateInsert(insert),
        SqlUpdateNode update => TranslateUpdate(update),
        SqlDeleteNode delete => TranslateDelete(delete),

        _ => throw new NotSupportedException(
            $"{nameof(SqlTextTranslator)} does not know how to translate a {node.GetType().Name}."),
    };

    private static SqlMutationStatement TranslateInsert(SqlInsertNode insert)
    {
        if (insert.Columns.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot translate an INSERT into '{insert.Entity.Name}' with no columns.");
        }

        var parameters = new List<SqlParameter>();
        var columnNames = new List<string>();
        var placeholders = new List<string>();

        foreach (var column in insert.Columns)
        {
            var columnName = ColumnName(insert.Entity, column.Column.ColumnId);
            var parameterName = $"@p{parameters.Count}";

            columnNames.Add(Quote(columnName));
            placeholders.Add(parameterName);
            parameters.Add(new SqlParameter(parameterName, ResolveMutationValue(column)));
        }

        var sql =
            $"INSERT INTO {Quote(insert.Entity.EffectiveStorageName)} " +
            $"({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", placeholders)})";

        return new SqlMutationStatement(insert.Entity.EntityId, sql, parameters);
    }

    private static SqlMutationStatement TranslateUpdate(SqlUpdateNode update)
    {
        if (update.Columns.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot translate an UPDATE on '{update.Entity.Name}' with no columns to set.");
        }

        var parameters = new List<SqlParameter>();
        var setClauses = new List<string>();

        foreach (var column in update.Columns)
        {
            var columnName = ColumnName(update.Entity, column.Column.ColumnId);
            var parameterName = $"@p{parameters.Count}";

            setClauses.Add($"{Quote(columnName)} = {parameterName}");
            parameters.Add(new SqlParameter(parameterName, ResolveMutationValue(column)));
        }

        var whereClause = BuildMutationFilterExpression(update.Filter, update.Entity, parameters);

        var sql =
            $"UPDATE {Quote(update.Entity.EffectiveStorageName)} SET {string.Join(", ", setClauses)} " +
            $"WHERE {whereClause}";

        return new SqlMutationStatement(update.Entity.EntityId, sql, parameters);
    }

    private static SqlMutationStatement TranslateDelete(SqlDeleteNode delete)
    {
        var parameters = new List<SqlParameter>();
        var whereClause = BuildMutationFilterExpression(delete.Filter, delete.Entity, parameters);

        var sql =
            $"DELETE FROM {Quote(delete.Entity.EffectiveStorageName)} WHERE {whereClause}";

        return new SqlMutationStatement(delete.Entity.EntityId, sql, parameters);
    }

    /// <summary>
    /// Resolves the literal to bind for one <see cref="MutationColumn"/>.
    /// Only <see cref="MutationValueKind.Input"/> and
    /// <see cref="MutationValueKind.Constant"/> carry a literal
    /// <see cref="MutationColumn.Value"/> today — <see cref="MutationValueKind.Generated"/>
    /// (e.g. an AUTOINCREMENT key) and <see cref="MutationValueKind.Expression"/>
    /// (a computed SQL expression) need dialect-specific handling this
    /// translator doesn't implement yet (see docs/CURRENT-STATUS.md).
    /// </summary>
    private static object? ResolveMutationValue(MutationColumn column) => column.ValueKind switch
    {
        MutationValueKind.Input or MutationValueKind.Constant => column.Value,

        _ => throw new NotSupportedException(
            $"{nameof(SqlTextTranslator)} does not yet support {nameof(MutationValueKind)}." +
            $"{column.ValueKind} (column '{column.Column.Entity.Name}." +
            $"{ColumnName(column.Column.Entity, column.Column.ColumnId)}'). Only " +
            $"{nameof(MutationValueKind.Input)} and {nameof(MutationValueKind.Constant)} are " +
            "translated today."),
    };

    /// <summary>
    /// Builds a WHERE-clause fragment (no leading "WHERE") for a single-table
    /// mutation statement from a provider-agnostic <see cref="FilterExpression"/>.
    /// Unlike <see cref="BuildFilterExpression"/> on the read side, there are
    /// no table aliases to resolve — a mutation always targets exactly one
    /// table — so every column in <paramref name="filter"/> must belong to
    /// <paramref name="entity"/>; <see cref="Foundgine.Planning.MutationPlanner"/>
    /// already validates this when a mutation is planned via
    /// <see cref="Foundgine.Planning.MutationIntent"/>, and this is the
    /// defense-in-depth check for plans built by hand.
    /// </summary>
    private static string BuildMutationFilterExpression(
        FilterExpression expression,
        EntityMetadata entity,
        List<SqlParameter> parameters)
    {
        switch (expression)
        {
            case ComparisonFilter comparison:
            {
                if (!Equals(comparison.Column.Entity, entity))
                {
                    throw new InvalidOperationException(
                        $"Cannot translate a mutation Filter on '{comparison.Column.Entity.Name}' " +
                        $"while mutating '{entity.Name}': a mutation's Filter may only reference " +
                        "columns on the entity being mutated, since it targets a single table.");
                }

                var columnName = ColumnName(entity, comparison.Column.ColumnId);
                var parameterName = $"@p{parameters.Count}";
                parameters.Add(new SqlParameter(parameterName, comparison.Value));

                return $"{Quote(columnName)} {ComparisonOperatorSql(comparison.Operator)} {parameterName}";
            }

            case CompositeFilter composite:
            {
                if (composite.Operands.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"A {nameof(CompositeFilter)} must have at least one operand.");
                }

                var keyword = composite.Combinator == FilterCombinator.And ? "AND" : "OR";

                var parts = composite.Operands.Select(operand =>
                    BuildMutationFilterExpression(operand, entity, parameters));

                return "(" + string.Join($" {keyword} ", parts) + ")";
            }

            default:
                throw new NotSupportedException(
                    $"{nameof(SqlTextTranslator)} does not know how to compile a " +
                    $"{expression.GetType().Name}.");
        }
    }

    /// <summary>
    /// Builds a WHERE-clause fragment (no leading "WHERE") from a
    /// provider-agnostic <see cref="FilterExpression"/>, recording every
    /// literal value it encounters as a <see cref="SqlParameter"/> instead
    /// of interpolating it into the SQL text.
    /// </summary>
    private static string BuildFilterExpression(
        FilterExpression expression,
        List<(SqlScanNode Occurrence, string Alias, int OccurrenceIndex)> orderedOccurrences,
        Dictionary<EntityMetadata, string> aliasByEntity,
        List<SqlParameter> parameters)
    {
        switch (expression)
        {
            case ComparisonFilter comparison:
            {
                var occurrenceIndex = ResolveProjectionOccurrenceIndex(
                    comparison.Column.Entity,
                    orderedOccurrences);

                var alias = ResolveProjectionAlias(
                    comparison.Column.Entity,
                    occurrenceIndex,
                    orderedOccurrences,
                    aliasByEntity);

                var columnName = ColumnName(
                    comparison.Column.Entity,
                    comparison.Column.ColumnId);

                var parameterName = $"@p{parameters.Count}";
                parameters.Add(new SqlParameter(parameterName, comparison.Value));

                return $"{alias}.{Quote(columnName)} " +
                    $"{ComparisonOperatorSql(comparison.Operator)} {parameterName}";
            }

            case CompositeFilter composite:
            {
                if (composite.Operands.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"A {nameof(CompositeFilter)} must have at least one operand.");
                }

                var keyword = composite.Combinator == FilterCombinator.And ? "AND" : "OR";

                var parts = composite.Operands.Select(operand =>
                    BuildFilterExpression(operand, orderedOccurrences, aliasByEntity, parameters));

                return "(" + string.Join($" {keyword} ", parts) + ")";
            }

            default:
                throw new NotSupportedException(
                    $"{nameof(SqlTextTranslator)} does not know how to compile a " +
                    $"{expression.GetType().Name}.");
        }
    }

    private static string ComparisonOperatorSql(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Equal => "=",
        ComparisonOperator.NotEqual => "<>",
        ComparisonOperator.GreaterThan => ">",
        ComparisonOperator.GreaterThanOrEqual => ">=",
        ComparisonOperator.LessThan => "<",
        ComparisonOperator.LessThanOrEqual => "<=",
        _ => throw new NotSupportedException(
            $"Unknown {nameof(ComparisonOperator)} '{op}'."),
    };

    private static string? BuildOrderBy(
        IReadOnlyList<SortTerm>? terms,
        List<(SqlScanNode Occurrence, string Alias, int OccurrenceIndex)> orderedOccurrences,
        Dictionary<EntityMetadata, string> aliasByEntity)
    {
        if (terms is not { Count: > 0 })
            return null;

        var parts = terms.Select(term =>
        {
            var occurrenceIndex = ResolveProjectionOccurrenceIndex(
                term.Column.Entity,
                orderedOccurrences);

            var alias = ResolveProjectionAlias(
                term.Column.Entity,
                occurrenceIndex,
                orderedOccurrences,
                aliasByEntity);

            var columnName = ColumnName(term.Column.Entity, term.Column.ColumnId);

            var direction = term.Direction == SortDirection.Descending ? "DESC" : "ASC";

            return $"{alias}.{Quote(columnName)} {direction}";
        });

        return string.Join(", ", parts);
    }

    /// <summary>
    /// SQLite (and most SQL dialects) requires LIMIT to precede OFFSET and
    /// doesn't allow OFFSET on its own — <c>-1</c> is SQLite's documented
    /// "no limit" sentinel, used here so <c>Page(Offset: 20)</c> alone
    /// still compiles.
    /// </summary>
    private static string? BuildLimitOffset(PageSpec? page)
    {
        if (page is not { } p || (p.Limit is null && p.Offset is null))
            return null;

        var limit = p.Limit ?? -1;

        return p.Offset is { } offset
            ? $"LIMIT {limit} OFFSET {offset}"
            : $"LIMIT {limit}";
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

    /// <summary>
    /// Peels SqlProjectionNode / SqlPageNode / SqlSortNode / SqlFilterNode
    /// off the top of the plan, in whatever order they were nested — the
    /// planner nests them Composite -> Filter -> Sort -> Page -> Projection
    /// to mirror SQL clause order, but nothing here depends on that exact
    /// order — leaving the join-chain root (SqlScanNode/SqlJoinNode) that
    /// <see cref="BuildFrom"/> knows how to read.
    /// </summary>
    private static (
        ProviderNode Root,
        IReadOnlyList<FieldBinding>? Fields,
        FilterExpression? Filter,
        IReadOnlyList<SortTerm>? Sort,
        PageSpec? Page) Unwrap(ProviderNode node)
    {
        IReadOnlyList<FieldBinding>? fields = null;
        FilterExpression? filter = null;
        IReadOnlyList<SortTerm>? sort = null;
        PageSpec? page = null;

        var current = node;

        while (true)
        {
            switch (current)
            {
                case SqlProjectionNode projection:
                    fields = projection.Fields;
                    current = projection.Source;
                    continue;

                case SqlPageNode pageNode:
                    page = pageNode.Page;
                    current = pageNode.Source;
                    continue;

                case SqlSortNode sortNode:
                    sort = sortNode.Terms;
                    current = sortNode.Source;
                    continue;

                case SqlFilterNode filterNode:
                    filter = filterNode.Filter;
                    current = filterNode.Source;
                    continue;
            }

            break;
        }

        return (current, fields, filter, sort, page);
    }

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