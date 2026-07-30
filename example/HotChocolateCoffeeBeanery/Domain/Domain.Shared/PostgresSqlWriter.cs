    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using CoffeeBeanery.GraphQL.Core.Runtime;

    namespace Domain.Shared;

    public sealed class PostgresSqlWriter
    {
        private readonly IEntityMetaProvider _meta;
        private readonly IGraphStrategy _graphStrategy;

        public PostgresSqlWriter(
            IEntityMetaProvider meta,
            IGraphStrategy graphStrategy)
        {
            _meta = meta;
            _graphStrategy = graphStrategy;
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
            QueryPlan plan)
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
            in MutationPlan mutation,
            in QueryPlan query)
        {
            var sb =
                new StringBuilder(1024);


            var arms =
                CollectUpsertArms(mutation);


            if (arms.Count > 0)
            {
                sb.AppendLine("WITH");


                for (var i = 0; i < arms.Count; i++)
                {
                    sb.Append("  mut_")
                        .Append(i)
                        .Append(" AS (");


                    sb.Append(arms[i]);


                    sb.Append(" RETURNING 1)");


                    sb.AppendLine(
                        i < arms.Count - 1
                            ? ","
                            : "");
                }
            }


            AppendSelect(
                sb,
                query);


            return sb.ToString();
        }


        public string WriteSelect(
            in QueryPlan plan)
        {
            var sb =
                new StringBuilder(512);


            AppendSelect(
                sb,
                plan);


            return sb.ToString();
        }

        public string WriteUpserts(
            in MutationPlan plan)
        {
            var sb =
                new StringBuilder();


            var arms =
                CollectUpsertArms(plan);


            for (var i = 0; i < arms.Count; i++)
            {
                if (i > 0)
                    sb.AppendLine(";");


                sb.Append(
                    arms[i]);
            }


            var graphSql =
                WriteGraphMerges(plan);


            if (!string.IsNullOrWhiteSpace(graphSql))
            {
                if (sb.Length > 0)
                    sb.AppendLine(";");


                sb.Append(
                    graphSql.TrimEnd());
            }


            return sb.ToString();
        }


        public string WriteGraphMerges(
            in MutationPlan plan)
        {
            var sb =
                new StringBuilder();


            foreach (var merge in plan.GraphMerges)
            {
                sb.AppendLine(
                    _graphStrategy.BuildGraphMerge(merge));
            }


            return sb.ToString();
        }


        private List<string> CollectUpsertArms(
            in MutationPlan plan)
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

        private string BuildLookupUpsert(in UpsertRow row)
        {
            var schema =
                row.SchemaOverride ??
                _meta.EntitySchema[row.StorageEntityId];

            var table =
                row.TableOverride ??
                _meta.EntityTable[row.StorageEntityId];

            var conflictCols =
                _meta.EntityConflictColumns[row.StorageEntityId];

            var insertColumns = new List<string>();

            foreach (var value in row.Values)
            {
                insertColumns.Add(
                    ResolveColumnName(
                        row.EntityId,
                        row.StorageEntityId,
                        value.FieldId));
            }

            foreach (var lookup in row.Lookups)
            {
                insertColumns.Add(
                    _meta.EntityColumnName
                            [lookup.LookupStorageEntityId]
                        [lookup.TargetColumnId]);
            }

            var sb = new StringBuilder();

            sb.Append("INSERT INTO \"")
                .Append(schema)
                .Append("\".\"")
                .Append(table)
                .Append("\" (");

            sb.Append(string.Join(", ",
                insertColumns.Select(x => $"\"{x}\"")));

            sb.AppendLine(")");

            WriteLookupSelect(sb, row);

            AppendLookupConflictClause(
                sb,
                row,
                insertColumns,
                conflictCols);

            return sb.ToString();
        }
        
        private void AppendLookupConflictClause(
            StringBuilder sb,
            in UpsertRow row,
            List<string> insertColumns,
            string[] conflictColumns)
        {
            if (conflictColumns.Length == 0)
            {
                sb.Append("ON CONFLICT DO NOTHING");
                return;
            }

            sb.Append("ON CONFLICT (");
            sb.Append(string.Join(", ",
                conflictColumns.Select(x => $"\"{x}\"")));
            sb.AppendLine(")");

            var updates = insertColumns
                .Where(x =>
                    !conflictColumns.Contains(
                        x,
                        StringComparer.OrdinalIgnoreCase))
                .Select(x =>
                    $"\"{x}\" = EXCLUDED.\"{x}\"")
                .ToList();

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

            foreach (var value in row.Values)
            {
                if (!first)
                    sb.Append(", ");

                AppendFieldValue(
                    sb,
                    row.StorageEntityId,
                    value.ColumnId,
                    value.RawValue);

                first = false;
            }

            foreach (var lookup in row.Lookups)
            {
                if (!first)
                    sb.Append(", ");

                sb.Append(lookup.Alias)
                    .Append(".\"")
                    .Append(
                        _meta.EntityColumnName
                                [lookup.LookupStorageEntityId]
                            [lookup.ResultColumnId])
                    .Append('"');

                first = false;
            }

            sb.AppendLine();

            WriteLookupFrom(sb, row);
        }
        
        private void WriteLookupFrom(
            StringBuilder sb,
            in UpsertRow row)
        {
            for (var i = 0; i < row.Lookups.Length; i++)
            {
                var lookup = row.Lookups[i];

                var schema =
                    _meta.EntitySchema[
                        lookup.LookupStorageEntityId];

                var table =
                    _meta.EntityTable[
                        lookup.LookupStorageEntityId];

                if (i == 0)
                    sb.Append("FROM ");
                else
                    sb.Append("JOIN ");

                sb.Append('"')
                    .Append(schema)
                    .Append("\".\"")
                    .Append(table)
                    .Append("\" ")
                    .Append(lookup.Alias)
                    .AppendLine();

                sb.Append("    ON ")
                    .Append(lookup.Alias)
                    .Append(".\"")
                    .Append(
                        _meta.EntityColumnName
                                [lookup.LookupStorageEntityId]
                            [lookup.LookupColumnId])
                    .Append("\" = ");

                AppendQuotedValue(
                    sb,
                    lookup.LookupValueLiteral?.ToString() ?? "");

                sb.AppendLine();
            }
        }
        
        private void WriteConflictClause(
            StringBuilder sb,
            ushort storageEntityId,
            ImmutableArray<FieldValue> values,
            ImmutableArray<LookupValue> lookups)
        {
            var conflicts =
                _meta.EntityConflictColumns[storageEntityId];

            if (conflicts.Length == 0)
            {
                sb.Append(" ON CONFLICT DO NOTHING");
                return;
            }

            sb.Append(" ON CONFLICT (");

            sb.Append(string.Join(", ",
                conflicts.Select(x => $"\"{x}\"")));

            sb.Append(") DO UPDATE SET ");

            var assignments =
                new List<string>();

            foreach (var value in values)
            {
                var column =
                    _meta.EntityColumnName[storageEntityId][value.ColumnId];

                if (conflicts.Contains(
                        column,
                        StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                assignments.Add(
                    $"\"{column}\" = EXCLUDED.\"{column}\"");
            }


            foreach (var lookup in lookups)
            {
                var column =
                    _meta.EntityColumnName[storageEntityId]
                        [lookup.TargetColumnId];


                if (conflicts.Contains(
                        column,
                        StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                assignments.Add(
                    $"\"{column}\" = EXCLUDED.\"{column}\"");
            }


            if (assignments.Count == 0)
            {
                sb.Append("DO NOTHING");
                return;
            }


            sb.Append(
                string.Join(", ", assignments));
        }

        private string ResolveStorageColumnName(
            ushort entityId,
            ushort fieldId)
        {
            var entity =
                MutationMetadataRegistry.Get(entityId);


            if (!entity.TryResolveField(
                    fieldId,
                    out var mapping))
            {
                throw new InvalidOperationException(
                    $"Missing field metadata. Entity={entityId}, Field={fieldId}");
            }


            return _meta.EntityColumnName[
                    mapping.StorageEntityId]
                [mapping.ColumnId];
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


            var conflictCols =
                _meta.EntityConflictColumns[
                    row.StorageEntityId];


            var columns =
                new Dictionary<string, FieldValue>(
                    StringComparer.OrdinalIgnoreCase);


            foreach (var value in row.Values)
            {
                var column =
                    ResolveColumnName(
                        row.EntityId,
                        row.StorageEntityId,
                        value.FieldId);


                columns[column] = value;
            }


            var sb =
                new StringBuilder();


            sb.Append("INSERT INTO \"")
                .Append(schema)
                .Append("\".\"")
                .Append(table)
                .Append("\" (");


            var index = 0;


            foreach (var column in columns.Keys)
            {
                if (index++ > 0)
                    sb.Append(", ");


                sb.Append('"')
                    .Append(column)
                    .Append('"');
            }


            sb.Append(") VALUES (");


            index = 0;


            foreach (var value in columns.Values)
            {
                if (index++ > 0)
                    sb.Append(", ");


                AppendFieldValue(
                    sb,
                    row.StorageEntityId,
                    value.FieldId,
                    value.RawValue);
            }


            sb.Append(')');


            AppendDoUpdateSet(
                sb,
                row.EntityId,
                row.StorageEntityId,
                columns.Values.ToImmutableArray(),
                conflictCols);


            return sb.ToString();
        }


        private string ResolveColumnName(
            ushort entityId,
            ushort storageEntityId,
            ushort fieldId)
        {
            var entity =
                MutationMetadataRegistry.Get(entityId);


            if (!entity.TryResolveField(
                    fieldId,
                    out var mapping))
            {
                throw new Exception(
                    $"Field mapping missing.\n" +
                    $"Entity={entityId}\n" +
                    $"Field={fieldId}");
            }


            if (mapping.StorageEntityId != storageEntityId)
            {
                throw new Exception(
                    $"Storage mismatch.\n" +
                    $"Entity={entityId}\n" +
                    $"Field={fieldId}\n" +
                    $"ExpectedStorage={storageEntityId}\n" +
                    $"ActualStorage={mapping.StorageEntityId}");
            }


            var columns =
                _meta.EntityColumnName[storageEntityId];


            if (mapping.ColumnId >= columns.Length)
            {
                throw new Exception(
                    $"Column mapping out of range.\n" +
                    $"Entity={entityId}\n" +
                    $"Field={fieldId}\n" +
                    $"StorageEntity={storageEntityId}\n" +
                    $"ColumnId={mapping.ColumnId}\n" +
                    $"ColumnCount={columns.Length}");
            }


            return columns[mapping.ColumnId];
        }

        private MutationFieldMetadata ResolveFieldMetadata(
            ushort entityId,
            ushort fieldId)
        {
            var entity =
                MutationMetadataRegistry.Get(entityId);


            if (!entity.TryResolveField(
                    fieldId,
                    out var mapping))
            {
                throw new Exception(
                    $"Field metadata missing.\n" +
                    $"Entity={entityId}\n" +
                    $"Field={fieldId}");
            }


            return mapping;
        }


        private void AppendSelect(
            StringBuilder sb,
            QueryPlan plan)
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
                        column.EntityId);


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
            QueryPlan plan,
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

        private void AppendJoin(
            StringBuilder sb,
            in JoinSpec join,
            in QueryPlan plan,
            string alias)
        {
            var tableSchema =
                _meta.EntitySchema[join.ChildStorageEntityId];


            var tableName =
                _meta.EntityTable[join.ChildStorageEntityId];


            sb.Append("LEFT JOIN \"")
                .Append(tableSchema)
                .Append("\".\"")
                .Append(tableName)
                .Append("\" ");


            AppendQuotedIdentifier(
                sb,
                alias);


            sb.Append(" ON ");


            var fromAlias =
                ResolveJoinAlias(
                    plan,
                    join.ParentStorageEntityId,
                    join.ParentEntityId,
                    join.ParentAlias);


            var fromColumn =
                _meta.EntityColumnName[
                        join.ParentStorageEntityId]
                    [join.ParentColumnId];


            var toColumn =
                _meta.EntityColumnName[
                        join.ChildStorageEntityId]
                    [join.ChildColumnId];


            AppendQuotedIdentifier(
                sb,
                fromAlias);


            sb.Append(".\"")
                .Append(fromColumn)
                .Append("\" = ");


            AppendQuotedIdentifier(
                sb,
                alias);


            sb.Append(".\"")
                .Append(toColumn)
                .Append('"');
        }


        private string ResolveJoinAlias(
            in QueryPlan plan,
            ushort storageEntityId,
            ushort entityId,
            string? requestedAlias = null)
        {
            static string Validate(
                string? alias,
                string reason)
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    throw new InvalidOperationException(
                        $"SQL alias was empty ({reason}).");
                }

                return alias;
            }


            if (!string.IsNullOrWhiteSpace(requestedAlias))
            {
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
            }


            if (plan.RootStorageEntityId == storageEntityId)
            {
                return Validate(
                    plan.RootOutputAlias,
                    "root");
            }


            foreach (var join in plan.Joins)
            {
                if (join.ChildStorageEntityId == storageEntityId)
                {
                    return Validate(
                        join.ChildAlias,
                        "child storage");
                }
            }


            foreach (var join in plan.Joins)
            {
                if (join.ParentStorageEntityId == storageEntityId)
                {
                    return Validate(
                        join.ParentAlias,
                        "parent storage");
                }
            }


            foreach (var graph in plan.GraphResultJoins)
            {
                if (graph.ToStorageEntityId == storageEntityId)
                {
                    return Validate(
                        graph.ToOutputAlias,
                        "graph result");
                }
            }


            throw new InvalidOperationException(
                $"Cannot resolve join alias.\n" +
                $"Entity={entityId}\n" +
                $"Storage={storageEntityId}\n" +
                $"RootAlias={plan.RootAlias}\n" +
                $"RootOutputAlias={plan.RootOutputAlias}");
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
                EnumConversions.TryConvert(
                    storageEntityId,
                    columnId,
                    rawValue);


            if (!string.IsNullOrEmpty(converted))
            {
                sb.Append(converted);
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


            for (var i = 0; i < conflictCols.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");


                sb.Append('"')
                    .Append(conflictCols[i])
                    .Append('"');
            }


            sb.Append(") DO UPDATE SET ");


            var updates =
                new List<string>();


            foreach (var value in values)
            {
                var columnName =
                    ResolveColumnName(
                        entityId,
                        storageEntityId,
                        value.FieldId);


                if (conflictCols.Any(x =>
                        string.Equals(
                            x,
                            columnName,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }


                updates.Add(
                    $"\"{columnName}\" = EXCLUDED.\"{columnName}\"");
            }


            if (updates.Count == 0)
            {
                sb.Length -= " DO UPDATE SET ".Length;
                sb.Append(" DO NOTHING");
                return;
            }


            sb.Append(
                string.Join(", ", updates));
        }

        private List<string> BuildCteNodeUpsertMerged(
            in MutationPlan plan)
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
        
        private string ResolvePrimaryKeyColumn(
            ushort storageEntityId)
        {
            var conflictColumns =
                _meta.EntityConflictColumns[
                    storageEntityId];

            if (conflictColumns.Length == 1)
            {
                return conflictColumns[0];
            }


            var columns =
                _meta.EntityColumnName[
                    storageEntityId];


            foreach (var column in columns)
            {
                if (string.Equals(
                        column,
                        "Id",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return column;
                }
            }


            throw new InvalidOperationException(
                $"Cannot resolve primary key column for storage entity {storageEntityId}");
        }


        private string ResolveNaturalKeyValue(
            in MutationCteNode child,
            CteResolutionSpec spec)
        {
            foreach (var value in child.Values)
            {
                var metadata =
                    ResolveFieldMetadata(
                        child.EntityId,
                        value.FieldId);


                var columnName =
                    _meta.EntityColumnName[
                            metadata.StorageEntityId]
                        [metadata.ColumnId];


                if (string.Equals(
                        columnName,
                        spec.RelatedNaturalKeyColumn,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return value.RawValue;
                }
            }


            throw new InvalidOperationException(
                $"No FieldValue on child '{child.Alias}' matches natural key column '{spec.RelatedNaturalKeyColumn}'.");
        }

        private static string QuotedValue(
            string value)
        {
            var sb =
                new StringBuilder();


            AppendQuotedValue(
                sb,
                value);


            return sb.ToString();
        }

    }