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
                graph.FromAlias,
                "GraphJoin");
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
            var candidate = $"{requested}_{index}";

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
            alias = $"{baseName}_{index}";
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
            alias = $"{baseName}_{index}";
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
            new List<string>();

        foreach (var row in plan.Rows)
        {
            arms.Add(
                BuildRegularUpsert(row));
        }

        foreach (var root in plan.CteRoots)
        {
            arms.Add(
                BuildCteNodeUpsertMerged(root));
        }

        return arms;
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


    private string BuildCteNodeUpsertMerged(
        in MutationCteNode root)
    {
        var schema =
            root.SchemaOverride ??
            _meta.EntitySchema[root.StorageEntityId];

        var table =
            root.TableOverride ??
            _meta.EntityTable[root.StorageEntityId];

        var conflictCols =
            root.ConflictColumns.Length > 0
                ? root.ConflictColumns.ToArray()
                : _meta.EntityConflictColumns[
                    root.StorageEntityId];

        if (root.Values.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Cannot build upsert for {schema}.{table}. No values.");
        }

        if (conflictCols.Length == 0)
        {
            throw new InvalidOperationException(
                $"Cannot build upsert for {schema}.{table}. No conflict columns.");
        }

        var resolutions =
            _meta.CteResolutions[root.EntityId];

        var matched =
            new List<(MutationCteNode Child, CteResolutionSpec Spec)>();

        foreach (var child in root.Children)
        {
            foreach (var spec in resolutions)
            {
                if (string.Equals(
                        spec.NavigationAlias,
                        child.Alias,
                        StringComparison.OrdinalIgnoreCase))
                {
                    matched.Add(
                        (child, spec));

                    break;
                }
            }
        }

        var columns =
            new Dictionary<string, FieldValue>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var value in root.Values)
        {
            var metadata =
                ResolveFieldMetadata(
                    root.EntityId,
                    value.FieldId);

            if (metadata.StorageEntityId != root.StorageEntityId)
                continue;

            var column =
                _meta.EntityColumnName[
                    metadata.StorageEntityId]
                [metadata.ColumnId];

            columns[column] = value;
        }

        var sb =
            new StringBuilder();

        sb.Append("INSERT INTO \"")
            .Append(schema)
            .Append("\".\"")
            .Append(table)
            .Append("\" (");

        var first = true;

        foreach (var column in columns.Keys)
        {
            if (!first)
                sb.Append(", ");

            first = false;

            sb.Append('"')
                .Append(column)
                .Append('"');
        }

        foreach (var (_, spec) in matched)
        {
            if (columns.ContainsKey(spec.ForeignKeyColumn))
                continue;

            if (!first)
                sb.Append(", ");

            first = false;

            sb.Append('"')
                .Append(spec.ForeignKeyColumn)
                .Append('"');
        }

        sb.Append(") SELECT ");

        first = true;

        foreach (var value in columns.Values)
        {
            if (!first)
                sb.Append(", ");

            first = false;

            var metadata =
                ResolveFieldMetadata(
                    root.EntityId,
                    value.FieldId);

            AppendFieldValue(
                sb,
                metadata.StorageEntityId,
                metadata.ColumnId,
                value.RawValue);
        }

        foreach (var (_, spec) in matched)
        {
            if (!first)
                sb.Append(", ");

            first = false;

            sb.Append(spec.RelatedTableAlias)
                .Append(".\"")
                .Append(spec.RelatedSurrogateIdColumn)
                .Append('"');
        }
        
                if (matched.Count > 0)
        {
            sb.Append(" FROM ");

            var whereConditions =
                new List<string>();

            for (var i = 0; i < matched.Count; i++)
            {
                var (child, spec) =
                    matched[i];

                var naturalKeyValue =
                    ResolveNaturalKeyValue(
                        child,
                        spec);

                if (i == 0)
                {
                    sb.Append('"')
                        .Append(_meta.EntitySchema[
                            child.StorageEntityId])
                        .Append("\".\"")
                        .Append(_meta.EntityTable[
                            child.StorageEntityId])
                        .Append("\" ")
                        .Append(spec.RelatedTableAlias);
                }
                else
                {
                    sb.Append(" JOIN \"")
                        .Append(_meta.EntitySchema[
                            child.StorageEntityId])
                        .Append("\".\"")
                        .Append(_meta.EntityTable[
                            child.StorageEntityId])
                        .Append("\" ")
                        .Append(spec.RelatedTableAlias)
                        .Append(" ON true");
                }

                whereConditions.Add(
                    $"{spec.RelatedTableAlias}.\"{spec.RelatedNaturalKeyColumn}\" = {QuotedValue(naturalKeyValue)}");
            }

            if (whereConditions.Count > 0)
            {
                sb.Append(" WHERE ")
                    .Append(string.Join(
                        " AND ",
                        whereConditions));
            }
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
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var column in columns.Keys)
        {
            if (!conflictCols.Contains(
                    column,
                    StringComparer.OrdinalIgnoreCase))
            {
                updates.Add(column);
            }
        }

        foreach (var (_, spec) in matched)
        {
            updates.Add(
                spec.ForeignKeyColumn);
        }

        if (updates.Count == 0)
        {
            sb.Length -=
                " DO UPDATE SET ".Length;

            sb.Append(" DO NOTHING");

            return sb.ToString();
        }

        first = true;

        foreach (var column in updates)
        {
            if (!first)
                sb.Append(", ");

            first = false;

            sb.Append('"')
                .Append(column)
                .Append("\" = EXCLUDED.\"")
                .Append(column)
                .Append('"');
        }

        return sb.ToString();
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


    private void AppendSelect(
        StringBuilder sb,
        QueryPlan plan)
    {
        sb.AppendLine("SELECT DISTINCT");

        for (int i = 0; i < plan.Columns.Length; i++)
        {
            var column = plan.Columns[i];

            if (i > 0)
            {
                sb.AppendLine(",");
            }

            if (column.Kind == ColumnKind.GraphSynthetic)
            {
                sb.Append("    \"")
                    .Append(column.EntityOutputAlias)
                    .Append("\".\"")
                    .Append(column.RawColumnName)
                    .Append("\" AS \"")
                    .Append(column.ColumnOutputAlias)
                    .Append('"');

                continue;
            }


            var tableAlias = column.EntityOutputAlias;

            if (string.IsNullOrEmpty(tableAlias))
            {
                tableAlias = plan.RootAlias;
            }


            var columnName =
                ResolveColumnName(
                    column.EntityId,
                    column.StorageEntityId,
                    column.ColumnId);


            sb.Append("    \"")
                .Append(tableAlias)
                .Append("\".\"")
                .Append(columnName)
                .Append("\" AS \"")
                .Append(column.ColumnOutputAlias)
                .Append('"');
        }

        sb.AppendLine();
    }


    private void AppendJoin(
        StringBuilder sb,
        in JoinSpec join,
        in QueryPlan plan,
        string alias)
    {
        var tableSchema =
            _meta.EntitySchema[join.ToStorageEntityId];

        var tableName =
            _meta.EntityTable[join.ToStorageEntityId];

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
                join.FromStorageEntityId,
                join.FromEntityId);

        var fromColumn =
            _meta.EntityColumnName[
                join.FromStorageEntityId]
            [join.FromColumnId];

        var toColumn =
            _meta.EntityColumnName[
                join.ToStorageEntityId]
            [join.ToColumnId];


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
        ushort entityId)
    {
        if (plan.RootStorageEntityId == storageEntityId &&
            plan.RootEntityId == entityId)
        {
            return plan.RootOutputAlias;
        }


        foreach (var join in plan.Joins)
        {
            if (join.ToEntityId == entityId &&
                join.ToStorageEntityId == storageEntityId)
            {
                return join.ToOutputAlias;
            }
        }


        foreach (var join in plan.Joins)
        {
            if (join.FromEntityId == entityId &&
                join.FromStorageEntityId == storageEntityId)
            {
                return join.ToOutputAlias;
            }
        }


        foreach (var resultJoin in plan.GraphResultJoins)
        {
            if (resultJoin.ToEntityId == entityId &&
                resultJoin.ToStorageEntityId == storageEntityId)
            {
                return resultJoin.ToOutputAlias;
            }
        }


        throw new InvalidOperationException(
            $"Cannot resolve join alias. Entity={entityId}, Storage={storageEntityId}");
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
            sb.Length -=
                " DO UPDATE SET ".Length;

            sb.Append(" DO NOTHING");

            return;
        }

        sb.Append(
            string.Join(", ", updates));
    }
}