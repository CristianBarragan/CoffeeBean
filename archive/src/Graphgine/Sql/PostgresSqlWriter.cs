using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using Graphgine.Execution;

    namespace Graphgine.Sql;

    public sealed class PostgresSqlWriter
    {
        private readonly IEntityMetaProvider _meta;
        private readonly IGraphStrategy _graphStrategy;
        private readonly IMutationMetadataProvider _mutationMetadata;
        private readonly IEnumConversionProvider _enumConversions;

        public PostgresSqlWriter(
            IEntityMetaProvider meta,
            IGraphStrategy graphStrategy,
            IMutationMetadataProvider mutationMetadata,
            IEnumConversionProvider enumConversions)
        {
            _meta = meta;
            _graphStrategy = graphStrategy;
            _mutationMetadata = mutationMetadata;
            _enumConversions = enumConversions;
        }


        internal static void AppendQuotedIdentifierStatic(
            StringBuilder sb,
            string identifier)
        {
            sb.Append('"')
                .Append(identifier.Replace("\"", "\"\""))
                .Append('"');
        }


        private static void ValidatePlanAliases(
            PhysicalQueryPlan plan)
        {
            var aliases =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);


            void Add(
                string? alias,
                string source)
            {
                if (string.IsNullOrWhiteSpace(alias))
                    return;


                if (aliases.TryGetValue(alias, out var existing))
                {
                    throw new InvalidOperationException(
                        $"Duplicate SQL alias '{alias}' detected.\n" +
                        $"Existing: {existing}\n" +
                        $"New: {source}");
                }


                aliases[alias] = source;
            }


            Add(
                plan.RootOutputAlias,
                "Root");


            foreach (var graph in plan.GraphJoins)
            {
                Add(
                    graph.JoinAlias,
                    "GraphJoin");
            }


            foreach (var graph in plan.GraphResultJoins)
            {
                Add(
                    graph.ToOutputAlias,
                    "GraphResultJoin");
            }


            foreach (var join in plan.Joins)
            {
                Add(
                    join.ChildAlias,
                    "Join");
            }
        }


        private static string AllocateJoinAlias(
            string requested,
            HashSet<string> usedAliases)
        {
            if (usedAliases.Add(requested))
                return requested;


            var index = 1;


            while (true)
            {
                var candidate =
                    $"{requested}_{index}";


                if (usedAliases.Add(candidate))
                    return candidate;


                index++;
            }
        }


        private static string CreateUniqueAlias(
            string baseName,
            HashSet<string> usedAliases)
        {
            var alias = baseName;
            var index = 1;


            while (!usedAliases.Add(alias))
            {
                alias =
                    $"{baseName}_{index}";

                index++;
            }


            return alias;
        }


        private static string CreateGraphAlias(
            string baseName,
            HashSet<string> usedAliases)
        {
            var alias = baseName;
            var index = 1;


            while (!usedAliases.Add(alias))
            {
                alias =
                    $"{baseName}_{index}";

                index++;
            }


            return alias;
        }


        public string WriteUpsertThenSelect(
            in PhysicalMutationPlan mutation,
            in PhysicalQueryPlan query)
        {
            var sb =
                new StringBuilder();

            var ctes =
                BuildMutationCtes(mutation);

            var graphCtes =
                CollectGraphMergeCtes(mutation);

            if (ctes.Count > 0 || graphCtes.Count > 0)
            {
                sb.AppendLine("WITH");

                var first = true;

                foreach (var cte in ctes)
                {
                    if (!first)
                    {
                        sb.AppendLine(",");
                    }

                    sb.Append(cte);

                    first = false;
                }

                foreach (var cte in graphCtes)
                {
                    if (!first)
                    {
                        sb.AppendLine(",");
                    }

                    sb.Append(cte);

                    first = false;
                }

                sb.AppendLine();
            }

            AppendSelect(
                sb,
                query);

            return sb.ToString();
        }
        
        private List<string> BuildMutationCtes(
            in PhysicalMutationPlan mutation)
        {
            var result =
                new List<string>();

            var completed =
                new HashSet<int>();

            var remaining =
                new HashSet<int>();

            for (var i = 0; i < mutation.Rows.Length; i++)
            {
                remaining.Add(i);
            }

            var cteIndex = 0;

            while (remaining.Count > 0)
            {
                var progress = false;

                foreach (var rowIndex in remaining.ToArray())
                {
                    if (!DependenciesSatisfied(
                            rowIndex,
                            completed,
                            mutation.Dependencies))
                    {
                        continue;
                    }

                    result.Add(
                        BuildMutationCte(
                            rowIndex,
                            cteIndex++,
                            mutation));

                    completed.Add(rowIndex);
                    remaining.Remove(rowIndex);

                    progress = true;
                }

                if (!progress)
                {
                    throw new InvalidOperationException(
                        "Circular mutation dependency detected.");
                }
            }

            return result;
        }
        
        private string BuildMutationCte(
            int rowIndex,
            int cteIndex,
            in PhysicalMutationPlan mutation)
        {
            var row =
                mutation.Rows[rowIndex];


            var dependencies =
                mutation.Dependencies
                    .Where(x =>
                        x.TargetRow == rowIndex)
                    .ToImmutableArray();


            Console.WriteLine(
                $"ROW {rowIndex} {row.EntityOutputAlias} deps={dependencies.Length}");


            foreach (var dep in dependencies)
            {
                Console.WriteLine(
                    $"  {dep.SourceRow}.{dep.SourceColumn} -> {dep.TargetRow}.{dep.TargetColumn}");
            }


            string sql;


            if (dependencies.Length > 0)
            {
                sql =
                    BuildDependentUpsert(
                        row,
                        dependencies);
            }
            else if (row.HasLookups)
            {
                sql =
                    BuildLookupUpsert(row);
            }
            else
            {
                sql =
                    BuildRegularUpsert(row);
            }


            return
                $"mut_{cteIndex} AS ({sql} RETURNING *)";
        }
        
        private static bool DependenciesSatisfied(
            int rowIndex,
            HashSet<int> completed,
            ImmutableArray<MutationDependency> dependencies)
        {
            foreach (var dependency in dependencies)
            {
                if (dependency.TargetRow != rowIndex)
                    continue;


                if (!completed.Contains(
                        dependency.SourceRow))
                {
                    return false;
                }
            }


            return true;
        }
        
        
        private string BuildDependentUpsert(
    in UpsertRow row,
    ImmutableArray<MutationDependency> dependencies)
{
    var schema =
        row.SchemaOverride ??
        _meta.EntitySchema[row.StorageEntityId];

    var table =
        row.TableOverride ??
        _meta.EntityTable[row.StorageEntityId];

    var insertColumns =
        new List<string>();

    var insertValues =
        new List<string>();

    foreach (var value in row.Values)
    {
        var column =
            _meta.EntityColumnName[
                row.StorageEntityId]
            [value.ColumnId];

        insertColumns.Add(column);

        MutationDependency? dependency = null;

        foreach (var dep in dependencies)
        {
            if (dep.TargetColumnId == value.ColumnId)
            {
                dependency = dep;
                break;
            }
        }

        if (dependency.HasValue)
        {
            insertValues.Add(
                BuildDependencyReference(
                    dependency.Value));
        }
        else
        {
            insertValues.Add(
                BuildLiteralExpression(
                    row.StorageEntityId,
                    value.ColumnId,
                    value.RawValue));
        }
    }

    var sb =
        new StringBuilder();

    sb.Append("INSERT INTO ");

    AppendQuotedIdentifier(
        sb,
        schema);

    sb.Append('.');

    AppendQuotedIdentifier(
        sb,
        table);

    sb.Append(" (");

    for (var i = 0; i < insertColumns.Count; i++)
    {
        if (i > 0)
        {
            sb.Append(", ");
        }

        AppendQuotedIdentifier(
            sb,
            insertColumns[i]);
    }

    sb.Append(") VALUES (");

    for (var i = 0; i < insertValues.Count; i++)
    {
        if (i > 0)
        {
            sb.Append(", ");
        }

        sb.Append(insertValues[i]);
    }

    sb.Append(')');

    AppendDoUpdateSet(
        sb,
        row.StorageEntityId,
        row.Values,
        row.ConflictColumns);

    return sb.ToString();
}
        
        private List<string> CollectGraphMergeCtes(
            in PhysicalMutationPlan plan)
        {
            var result =
                new List<string>();

            var seen =
                new HashSet<GraphMergeKey>();

            var index = 0;

            foreach (var merge in plan.GraphMerges)
            {
                var key =
                    new GraphMergeKey(
                        merge.GraphName,
                        merge.EdgeLabel,
                        merge.FromLabel,
                        merge.FromKeyColumn,
                        merge.FromKeyValue,
                        merge.ToLabel,
                        merge.ToKeyColumn,
                        merge.ToKeyValue,
                        merge.EdgeKeyColumn,
                        merge.EdgeKeyValue,
                        merge.EdgePropertiesHash);

                if (!seen.Add(key))
                {
                    continue;
                }

                result.Add(
                    _graphStrategy.BuildGraphMerge(
                        index++,
                        merge));
            }

            return result;
        }

        
        private static string BuildDependencyReference(
            MutationDependency dependency)
        {
            return
                $"(SELECT \"{dependency.SourceColumn}\" FROM mut_{dependency.SourceRow})";
        }

        public string WriteSelect(
            in PhysicalQueryPlan plan)
        {
            var sb =
                new StringBuilder(512);


            AppendSelect(
                sb,
                plan);


            return sb.ToString();
        }

        public string WriteUpserts(
            in PhysicalMutationPlan plan)
        {
            var sb =
                new StringBuilder();

            var arms =
                CollectUpsertArms(plan);

            for (var i = 0; i < arms.Count; i++)
            {
                if (i > 0)
                {
                    sb.AppendLine(";");
                }

                sb.Append(
                    arms[i]);
            }

            var graphSql =
                WriteGraphMerges(plan);

            if (!string.IsNullOrWhiteSpace(graphSql))
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine(";");
                }

                sb.Append(
                    graphSql.TrimEnd());
            }

            return sb.ToString();
        }


        public string WriteGraphMerges(
            in PhysicalMutationPlan plan)
        {
            var sb =
                new StringBuilder();

            var seen =
                new HashSet<GraphMergeKey>();

            var index = 0;

            foreach (var merge in plan.GraphMerges)
            {
                var key =
                    new GraphMergeKey(
                        merge.GraphName,
                        merge.EdgeLabel,
                        merge.FromLabel,
                        merge.FromKeyColumn,
                        merge.FromKeyValue,
                        merge.ToLabel,
                        merge.ToKeyColumn,
                        merge.ToKeyValue,
                        merge.EdgeKeyColumn,
                        merge.EdgeKeyValue,
                        GraphMergeKey.NormalizeProperties(
                            merge.EdgeProperties));

                if (!seen.Add(key))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.AppendLine(",");
                }

                sb.Append(
                    _graphStrategy.BuildGraphMerge(
                        index++,
                        merge));
            }

            return sb.ToString();
        }

        private List<string> CollectUpsertArms(
            in PhysicalMutationPlan plan)
        {
            var arms =
                new List<string>(plan.Rows.Length);


            foreach (var row in plan.Rows)
            {
                if (row.HasLookups)
                {
                    arms.Add(
                        BuildLookupUpsert(row));
                }
                else
                {
                    arms.Add(
                        BuildRegularUpsert(row));
                }
            }


            return arms;
        }

        private string BuildLookupUpsert(
            in UpsertRow row)
        {
            var schema =
                row.SchemaOverride ??
                _meta.EntitySchema[row.StorageEntityId];

            var table =
                row.TableOverride ??
                _meta.EntityTable[row.StorageEntityId];

            var conflictColumns =
                row.ConflictColumns;

            var insertColumns =
                new List<string>();

            //
            // Literal columns
            //
            foreach (var value in row.Values)
            {
                var column =
                    _meta.EntityColumnName[
                            row.StorageEntityId]
                        [value.ColumnId];

                if (!insertColumns.Contains(
                        column,
                        StringComparer.OrdinalIgnoreCase))
                {
                    insertColumns.Add(column);
                }
            }

            //
            // FK columns resolved through lookups
            //
            foreach (var lookup in row.Lookups)
            {
                var column =
                    _meta.EntityColumnName[
                            row.StorageEntityId]
                        [lookup.TargetColumnId];

                if (!insertColumns.Contains(
                        column,
                        StringComparer.OrdinalIgnoreCase))
                {
                    insertColumns.Add(column);
                }
            }

            var sb =
                new StringBuilder();

            sb.Append("INSERT INTO ");

            AppendQuotedIdentifier(
                sb,
                schema);

            sb.Append('.');

            AppendQuotedIdentifier(
                sb,
                table);

            sb.Append(" (");

            for (var i = 0;
                 i < insertColumns.Count;
                 i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                AppendQuotedIdentifier(
                    sb,
                    insertColumns[i]);
            }

            sb.AppendLine(")");

            WriteLookupSelect(
                sb,
                row);

            AppendLookupConflictClause(
                sb,
                row,
                insertColumns,
                conflictColumns);

            return sb.ToString();
        }
        
        private void AppendLookupConflictClause(
            StringBuilder sb,
            in UpsertRow row,
            List<string> insertColumns,
            ImmutableArray<ConflictColumn> conflictColumns)
        {
            if (conflictColumns.IsDefaultOrEmpty)
            {
                sb.Append("ON CONFLICT DO NOTHING");
                return;
            }

            sb.Append("ON CONFLICT (");

            for (var i = 0; i < conflictColumns.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                AppendQuotedIdentifier(
                    sb,
                    _meta.EntityColumnName[row.StorageEntityId]
                        [conflictColumns[i].ColumnId]);
            }

            sb.AppendLine(")");

            var updates =
                new List<string>();

            foreach (var column in insertColumns)
            {
                var isConflict = false;

                foreach (var conflict in conflictColumns)
                {
                    var conflictName =
                        _meta.EntityColumnName[row.StorageEntityId]
                            [conflict.ColumnId];

                    if (string.Equals(
                            conflictName,
                            column,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        isConflict = true;
                        break;
                    }
                }

                if (isConflict)
                {
                    continue;
                }

                updates.Add(
                    $"\"{column}\" = EXCLUDED.\"{column}\"");
            }

            if (updates.Count == 0)
            {
                sb.Append("DO NOTHING");
                return;
            }

            sb.Append("DO UPDATE SET ");
            sb.Append(string.Join(", ", updates));
        }
        
        private void WriteLookupSelect(
            StringBuilder sb,
            in UpsertRow row)
        {
            sb.Append("SELECT ");

            var first = true;

            //
            // Literal values
            //
            foreach (var value in row.Values)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                AppendFieldValue(
                    sb,
                    row.StorageEntityId,
                    value.ColumnId,
                    value.RawValue);

                first = false;
            }

            //
            // FK values resolved from lookup tables
            //
            foreach (var lookup in row.Lookups)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                AppendQuotedIdentifier(
                    sb,
                    lookup.Alias);

                sb.Append('.');

                AppendQuotedIdentifier(
                    sb,
                    _meta.EntityColumnName[
                            lookup.LookupStorageEntityId]
                        [lookup.ResultColumnId]);

                first = false;
            }

            sb.AppendLine();

            WriteLookupFrom(
                sb,
                row);
        }
        
        private string BuildLiteralExpression(
            ushort storageEntityId,
            ushort columnId,
            string rawValue)
        {
            var sb =
                new StringBuilder();

            AppendFieldValue(
                sb,
                storageEntityId,
                columnId,
                rawValue);

            return sb.ToString();
        }
        
        private void WriteLookupFrom(
            StringBuilder sb,
            in UpsertRow row)
        {
            for (var i = 0;
                 i < row.Lookups.Length;
                 i++)
            {
                var lookup =
                    row.Lookups[i];

                var schema =
                    _meta.EntitySchema[
                        lookup.LookupStorageEntityId];

                var table =
                    _meta.EntityTable[
                        lookup.LookupStorageEntityId];

                var naturalKeyColumn =
                    _meta.EntityColumnName[
                            lookup.LookupStorageEntityId]
                        [lookup.LookupColumnId];

                if (i == 0)
                {
                    sb.Append("FROM ");
                }
                else
                {
                    sb.Append("JOIN ");
                }

                AppendQuotedIdentifier(
                    sb,
                    schema);

                sb.Append('.');

                AppendQuotedIdentifier(
                    sb,
                    table);

                sb.Append(' ');

                AppendQuotedIdentifier(
                    sb,
                    lookup.Alias);

                sb.AppendLine();

                sb.Append("    ON ");

                AppendQuotedIdentifier(
                    sb,
                    lookup.Alias);

                sb.Append('.');

                AppendQuotedIdentifier(
                    sb,
                    naturalKeyColumn);

                sb.Append(" = ");

                AppendQuotedValue(
                    sb,
                    lookup.LookupValueLiteral?.ToString() ?? string.Empty);

                sb.AppendLine();
            }
        }
        
        private void WriteConflictClause(
            StringBuilder sb,
            ushort storageEntityId,
            ImmutableArray<FieldValue> values,
            ImmutableArray<LookupValue> lookups,
            ImmutableArray<ConflictColumn> conflictColumns)
        {
            if (conflictColumns.IsDefaultOrEmpty)
            {
                sb.Append(" ON CONFLICT DO NOTHING");
                return;
            }

            sb.Append(" ON CONFLICT (");

            for (var i = 0; i < conflictColumns.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                var column =
                    _meta.EntityColumnName[
                            storageEntityId]
                        [conflictColumns[i].FieldId];

                AppendQuotedIdentifier(
                    sb,
                    column);
            }

            sb.Append(") DO UPDATE SET ");

            var first = true;

            foreach (var value in values)
            {
                var column =
                    _meta.EntityColumnName[
                            storageEntityId]
                        [value.ColumnId];

                if (conflictColumns.Any(x =>
                        x.FieldId == value.ColumnId))
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(", ");
                }

                first = false;

                AppendQuotedIdentifier(sb, column);
                sb.Append(" = EXCLUDED.");
                AppendQuotedIdentifier(sb, column);
            }

            foreach (var lookup in lookups)
            {
                var column =
                    _meta.EntityColumnName[
                            storageEntityId]
                        [lookup.TargetColumnId];

                if (conflictColumns.Any(x =>
                        x.FieldId == lookup.TargetColumnId))
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(", ");
                }

                first = false;

                AppendQuotedIdentifier(sb, column);
                sb.Append(" = EXCLUDED.");
                AppendQuotedIdentifier(sb, column);
            }

            if (first)
            {
                sb.Length -= " DO UPDATE SET ".Length;
                sb.Append("DO NOTHING");
            }
        }
        
        private string BuildRegularUpsert(
            in UpsertRow row)
        {
            var schema =
                row.SchemaOverride ??
                _meta.EntitySchema[row.StorageEntityId];

            var table =
                row.TableOverride ??
                _meta.EntityTable[row.StorageEntityId];

            var columns =
                new Dictionary<string, FieldValue>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var value in row.Values)
            {
                var column =
                    _meta.EntityColumnName[
                            row.StorageEntityId]
                        [value.ColumnId];

                columns[column] = value;
            }

            var sb =
                new StringBuilder();

            sb.Append("INSERT INTO ");

            AppendQuotedIdentifier(
                sb,
                schema);

            sb.Append('.');

            AppendQuotedIdentifier(
                sb,
                table);

            sb.Append(" (");

            var first = true;

            foreach (var column in columns.Keys)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                AppendQuotedIdentifier(
                    sb,
                    column);

                first = false;
            }

            sb.Append(") VALUES (");

            first = true;

            foreach (var value in columns.Values)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                AppendFieldValue(
                    sb,
                    row.StorageEntityId,
                    value.ColumnId,
                    value.RawValue);

                first = false;
            }

            sb.Append(')');

            AppendDoUpdateSet(
                sb,
                row.StorageEntityId,
                row.Values,
                row.ConflictColumns);

            return sb.ToString();
        }

        private MutationFieldMetadata ResolveFieldMetadata(
            ushort entityId,
            ushort fieldId)
        {
            return _mutationMetadata.ResolveField(
                entityId,
                fieldId);
        }


        private void AppendSelect(
    StringBuilder sb,
    PhysicalQueryPlan plan)
{
    sb.AppendLine("SELECT DISTINCT");


    for (int i = 0; i < plan.Columns.Length; i++)
    {
        var column =
            plan.Columns[i];


        if (i > 0)
        {
            sb.AppendLine(",");
        }


        if (column.Kind == ColumnKind.GraphSynthetic)
        {
            var alias =
                ResolveGraphSyntheticAlias(
                    plan,
                    column);


            sb.Append("    \"")
                .Append(alias)
                .Append("\".\"")
                .Append(column.RawColumnName)
                .Append("\" AS \"")
                .Append(column.ColumnOutputAlias)
                .Append('"');


            continue;
        }
        
        var tableAlias =
            ResolveJoinAlias(
                plan,
                column.StorageEntityId,
                column.EntityId,
                column.EntityOutputAlias);


        var columnName =
            _meta.EntityColumnName[
                    column.StorageEntityId]
                [column.ColumnId];


        sb.Append("    \"")
            .Append(tableAlias)
            .Append("\".\"")
            .Append(columnName)
            .Append("\" AS \"")
            .Append(column.ColumnOutputAlias)
            .Append('"');
    }


    sb.AppendLine();


    sb.Append("FROM ");


    AppendQualifiedTable(
        sb,
        plan.RootStorageEntityId);


    sb.Append(' ');


    AppendQuotedIdentifier(
        sb,
        plan.RootAlias);


    foreach (var graphJoin in plan.GraphJoins)
    {
        sb.Append('\n');

        _graphStrategy.AppendGraphJoin(
            sb,
            graphJoin,
            plan.RootAlias);
    }


    foreach (var resultJoin in plan.GraphResultJoins)
    {
        sb.Append('\n');

        _graphStrategy.AppendGraphResultJoin(
            sb,
            resultJoin);
    }


    foreach (var join in plan.Joins)
    {
        sb.Append('\n');

        AppendJoin(
            sb,
            join,
            plan,
            join.ChildAlias);
    }
}


        private static string ResolveGraphSyntheticAlias(
            PhysicalQueryPlan plan,
            ColumnSpec column)
        {
            foreach (var graph in plan.GraphResultJoins)
            {
                if (string.Equals(
                        graph.ToOutputAlias,
                        column.ColumnOutputAlias,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return graph.ToOutputAlias;
                }


                if (string.Equals(
                        graph.FromAlias,
                        column.ColumnOutputAlias,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return graph.FromAlias;
                }
            }


            foreach (var graph in plan.GraphJoins)
            {
                if (string.Equals(
                        graph.JoinAlias,
                        column.ColumnOutputAlias,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return graph.JoinAlias;
                }


                if (string.Equals(
                        graph.FromAlias,
                        column.ColumnOutputAlias,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return graph.FromAlias;
                }
            }


            throw new InvalidOperationException(
                $"Cannot resolve graph synthetic alias. " +
                $"ColumnOutputAlias={column.ColumnOutputAlias}, " +
                $"RawColumn={column.RawColumnName}");
        }

        // -------------------------------------------------------------------
        // FIXED (2 bugs):
        //
        // 1. Identifier quoting: every schema/table/alias/column name below
        //    used to go through AppendQuotedValue, which wraps its argument
        //    in SINGLE quotes (it's the literal-VALUE quoter used elsewhere
        //    for things like `ON alias."col" = 'value'`). Applying it to
        //    identifiers produced syntactically invalid SQL such as
        //    LEFT JOIN 'Banking'.'Customer' 'SomeAlias' ON ...
        //    which Postgres parses as string literals, not table/column
        //    references, and fails outright. Every occurrence of
        //    AppendQuotedValue on an identifier here is now
        //    AppendQuotedIdentifier (double-quoted, with embedded quotes
        //    escaped) — consistent with every other identifier-emitting
        //    method in this class (AppendSelect, AppendQualifiedTable, etc).
        //
        // 2. Join parent alias: the ON clause used to hardcode
        //    `plan.RootAlias` as the left-hand side of every join, ignoring
        //    `join.ParentAlias` entirely. JoinSpec carries a per-join
        //    ParentAlias specifically so that PlannerEmitter.EmitNavigationJoins
        //    can emit multi-hop join chains, where hop N's parent is hop
        //    N-1's alias, not the query root. For single-hop joins straight
        //    off the root this coincidentally produced correct-looking SQL
        //    (join.ParentAlias == plan.RootAlias), which is why it wasn't
        //    obvious from a simple single-hop query. For any multi-hop
        //    navigation path it silently joined every hop back to the root
        //    table instead of chaining through the intermediate alias.
        //    Now uses join.ParentAlias, matching what JoinSpec/AddJoin
        //    already carry.
        // -------------------------------------------------------------------
        private void AppendJoin(
    StringBuilder sb,
    in JoinSpec join,
    in PhysicalQueryPlan plan,
    string alias)
{
    // ---------------------------------------------------------------
    // FIXED: bounds must be validated against EVERY metadata array this
    // method indexes into (EntityTable, EntitySchema, EntityColumnName)
    // BEFORE any of them are touched. Previously EntityTable/EntitySchema
    // were indexed unchecked, and only EntityColumnName was validated —
    // and only after the unchecked accesses already ran. If EntityTable
    // or EntitySchema is shorter than EntityColumnName for a given
    // StorageEntityId (or the ID is simply wrong, e.g. from a composite
    // multi-hop join chain that resolved to an entity your
    // IEntityMetaProvider never registered), this threw a bare
    // IndexOutOfRangeException with no context, instead of the
    // informative InvalidOperationException the method clearly intends
    // to give you. Now every array this method reads is bounds-checked
    // up front, and the exception names which side (parent/child), which
    // StorageEntityId, and which array came up short — enough to trace
    // straight back to the join/navigation that produced it.
    // ---------------------------------------------------------------
    ValidateStorageEntityId(
        join.ParentStorageEntityId,
        "Parent");

    ValidateStorageEntityId(
        join.ChildStorageEntityId,
        "Child");

    var parentTable =
        _meta.EntityTable[join.ParentStorageEntityId];

    var childTable =
        _meta.EntityTable[join.ChildStorageEntityId];

    var parentSchema =
        _meta.EntitySchema[join.ParentStorageEntityId];

    var childSchema =
        _meta.EntitySchema[join.ChildStorageEntityId];

    var parentColumns =
        _meta.EntityColumnName[join.ParentStorageEntityId];

    var childColumns =
        _meta.EntityColumnName[join.ChildStorageEntityId];

    if (join.ParentColumnId >= parentColumns.Length)
    {
        throw new InvalidOperationException(
            $"AppendJoin: cannot resolve FROM column. " +
            $"ParentStorageEntityId={join.ParentStorageEntityId}, " +
            $"ParentColumnId={join.ParentColumnId}, " +
            $"ArrayLength={parentColumns.Length}");
    }

    if (join.ChildColumnId >= childColumns.Length)
    {
        throw new InvalidOperationException(
            $"AppendJoin: cannot resolve TO column. " +
            $"ChildStorageEntityId={join.ChildStorageEntityId}, " +
            $"ChildColumnId={join.ChildColumnId}, " +
            $"ArrayLength={childColumns.Length}");
    }

    var parentColumn =
        parentColumns[join.ParentColumnId];

    var childColumn =
        childColumns[join.ChildColumnId];

    sb.Append(" LEFT JOIN ");
    AppendQuotedIdentifier(sb, childSchema);
    sb.Append('.');
    AppendQuotedIdentifier(sb, childTable);
    sb.Append(' ');
    AppendQuotedIdentifier(sb, alias);

    sb.Append(" ON ");

    AppendQuotedIdentifier(sb, alias);
    sb.Append('.');
    AppendQuotedIdentifier(sb, childColumn);

    sb.Append(" = ");

    AppendQuotedIdentifier(sb, join.ParentAlias);
    sb.Append('.');
    AppendQuotedIdentifier(sb, parentColumn);
}


        private string ResolveJoinAlias(
    in PhysicalQueryPlan plan,
    ushort storageEntityId,
    ushort entityId,
    string? requestedAlias = null)
{
    static string RequireAlias(
        string? alias,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new InvalidOperationException(
                $"Missing SQL alias. Reason={reason}");
        }

        return alias;
    }


    // Explicit alias
    if (!string.IsNullOrWhiteSpace(requestedAlias))
    {
        if (string.Equals(
                plan.RootAlias,
                requestedAlias,
                StringComparison.OrdinalIgnoreCase))
        {
            return requestedAlias;
        }


        if (string.Equals(
                plan.RootOutputAlias,
                requestedAlias,
                StringComparison.OrdinalIgnoreCase))
        {
            return requestedAlias;
        }


        foreach (var join in plan.Joins)
        {
            if (string.Equals(
                    join.ChildAlias,
                    requestedAlias,
                    StringComparison.OrdinalIgnoreCase))
            {
                return requestedAlias;
            }
        }


        foreach (var graph in plan.GraphJoins)
        {
            if (string.Equals(
                    graph.JoinAlias,
                    requestedAlias,
                    StringComparison.OrdinalIgnoreCase))
            {
                return requestedAlias;
            }
        }


        foreach (var graph in plan.GraphResultJoins)
        {
            if (string.Equals(
                    graph.ToOutputAlias,
                    requestedAlias,
                    StringComparison.OrdinalIgnoreCase))
            {
                return requestedAlias;
            }
        }
    }



    // Root storage
    if (plan.RootStorageEntityId == storageEntityId)
    {
        return RequireAlias(
            plan.RootAlias,
            "root storage");
    }



    // Normal relational joins
    foreach (var join in plan.Joins)
    {
        if (join.ChildStorageEntityId == storageEntityId)
        {
            return RequireAlias(
                join.ChildAlias,
                "child storage");
        }
    }


    foreach (var join in plan.Joins)
    {
        if (join.ParentStorageEntityId == storageEntityId)
        {
            return RequireAlias(
                join.ParentAlias,
                "parent storage");
        }
    }



    // Graph joins
    foreach (var graph in plan.GraphJoins)
    {
        if (graph.StorageEntityId == storageEntityId)
        {
            return RequireAlias(
                graph.JoinAlias,
                "graph storage");
        }
    }



    // Graph result joins
    foreach (var graph in plan.GraphResultJoins)
    {
        if (graph.ToStorageEntityId == storageEntityId)
        {
            return RequireAlias(
                graph.ToOutputAlias,
                "graph result target");
        }
    }

    throw new InvalidOperationException(
        $"Cannot resolve join alias.\n" +
        $"Entity={entityId}\n" +
        $"Storage={storageEntityId}\n" +
        $"RootAlias={plan.RootAlias}\n" +
        $"RootOutputAlias={plan.RootOutputAlias}");
}

        /// <summary>
        /// Validates a StorageEntityId against every metadata array
        /// AppendJoin reads (EntityTable, EntitySchema, EntityColumnName)
        /// before any of them are indexed. Throws an InvalidOperationException
        /// naming the side (parent/child), the offending ID, which array
        /// came up short, and its actual length — so a bad ID from a
        /// generated composite/navigation join chain fails loudly with
        /// enough context to trace back to its source, instead of a bare
        /// unhandled IndexOutOfRangeException.
        /// </summary>
        private void ValidateStorageEntityId(
            ushort storageEntityId,
            string side)
        {
            if (storageEntityId >= _meta.EntityTable.Length)
            {
                throw new InvalidOperationException(
                    $"{side}StorageEntityId={storageEntityId} is outside " +
                    $"EntityTable (Length={_meta.EntityTable.Length}). " +
                    $"This usually means a generated join references a " +
                    $"StorageEntityId that IEntityMetaProvider never " +
                    $"registered — check the composite/navigation join " +
                    $"chain that produced this join.");
            }

            if (storageEntityId >= _meta.EntitySchema.Length)
            {
                throw new InvalidOperationException(
                    $"{side}StorageEntityId={storageEntityId} is outside " +
                    $"EntitySchema (Length={_meta.EntitySchema.Length}). " +
                    $"This usually means a generated join references a " +
                    $"StorageEntityId that IEntityMetaProvider never " +
                    $"registered — check the composite/navigation join " +
                    $"chain that produced this join.");
            }

            if (storageEntityId >= _meta.EntityColumnName.Length)
            {
                throw new InvalidOperationException(
                    $"{side}StorageEntityId={storageEntityId} is outside " +
                    $"EntityColumnName (Length={_meta.EntityColumnName.Length}). " +
                    $"This usually means a generated join references a " +
                    $"StorageEntityId that IEntityMetaProvider never " +
                    $"registered — check the composite/navigation join " +
                    $"chain that produced this join.");
            }
        }


        private void AppendQualifiedTable(
            StringBuilder sb,
            ushort storageEntityId)
        {
            sb.Append('"')
                .Append(_meta.EntitySchema[storageEntityId])
                .Append("\".\"")
                .Append(_meta.EntityTable[storageEntityId])
                .Append('"');
        }


        private static void AppendQuotedIdentifier(
            StringBuilder sb,
            string identifier)
        {
            sb.Append('"')
                .Append(identifier.Replace("\"", "\"\""))
                .Append('"');
        }


        private void AppendFieldValue(
            StringBuilder sb,
            ushort storageEntityId,
            ushort columnId,
            string rawValue)
        {
            var converted =
                _enumConversions.TryConvert(
                    storageEntityId,
                    columnId,
                    rawValue);


            if (!string.IsNullOrEmpty(converted))
            {
                sb.Append((string)converted);
                return;
            }


            AppendQuotedValue(
                sb,
                rawValue);
        }


        private static void AppendQuotedValue(
            StringBuilder sb,
            string value)
        {
            if (int.TryParse(
                    value,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _))
            {
                sb.Append(value);
                return;
            }


            sb.Append('\'')
                .Append(value.Replace("'", "''"))
                .Append('\'');
        }


        private void AppendDoUpdateSet(
            StringBuilder sb,
            ushort storageEntityId,
            ImmutableArray<FieldValue> values,
            ImmutableArray<ConflictColumn> conflictColumns)
        {
            if (conflictColumns.IsDefaultOrEmpty)
            {
                sb.Append(" ON CONFLICT DO NOTHING");
                return;
            }

            sb.Append(" ON CONFLICT (");

            for (var i = 0; i < conflictColumns.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                AppendQuotedIdentifier(
                    sb,
                    _meta.EntityColumnName[storageEntityId][conflictColumns[i].ColumnId]);
            }

            sb.Append(") ");

            var updates = new List<string>();

            foreach (var value in values)
            {
                var column =
                    _meta.EntityColumnName[storageEntityId][value.ColumnId];

                var isConflict = false;

                foreach (var conflict in conflictColumns)
                {
                    if (conflict.ColumnId == value.ColumnId)
                    {
                        isConflict = true;
                        break;
                    }
                }

                if (isConflict)
                    continue;

                updates.Add(
                    $"\"{column}\" = EXCLUDED.\"{column}\"");
            }

            if (updates.Count == 0)
            {
                sb.Append("DO NOTHING");
                return;
            }

            sb.Append("DO UPDATE SET ");
            sb.Append(string.Join(", ", updates));
        }

        private List<string> BuildCteNodeUpsertMerged(
            in PhysicalMutationPlan plan)
        {
            var statements = new List<string>();

            foreach (var row in plan.Rows)
            {
                if (row.HasLookups)
                {
                    statements.Add(BuildLookupUpsert(row));
                }
                else
                {
                    statements.Add(BuildRegularUpsert(row));
                }
            }

            return statements;
        }
    }