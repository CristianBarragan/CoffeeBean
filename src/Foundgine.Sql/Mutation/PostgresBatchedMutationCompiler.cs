using System.Text;
using System.Text.RegularExpressions;
using Foundgine.Abstractions;
using Foundgine.Metadata;
using Foundgine.Planning.Mutation;
using Foundgine.Sql.Query;

namespace Foundgine.Sql.Mutation;

/// <summary>
/// Compiles an entire mutation batch into one PostgreSQL statement.
///
/// Create/Upsert operations at the same dependency level are grouped by shape and
/// expanded with PostgreSQL unnest(array parameters). Reference-valued fields use
/// the source group's 1-based unnest ordinal and an ord-map CTE, so generated
/// values flow between levels without client-side round trips.
///
/// Update/Delete operations remain one CTE each, but are still folded into the
/// same physical statement. If a batch cannot be represented safely, TryCompile
/// returns null and the caller can use the existing sequential SQL compiler.
/// </summary>
public sealed class PostgresBatchedMutationCompiler
{
    private readonly IMetadataProvider _metadata;

    public PostgresBatchedMutationCompiler(IMetadataProvider metadata) =>
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

    public SqlBatchedMutationPlan Compile(MutationBatchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Operations.Count == 0)
            throw new InvalidOperationException("A mutation batch must contain at least one operation.");

        var ops = plan.Operations;
        var levels = ComputeLevels(ops.Count, plan.Dependencies);
        var opToGroup = new int[ops.Count];
        var groups = new List<OpGroup>();
        var forcedReturns = new Dictionary<int, HashSet<FieldId>>();

        foreach (var level in levels.Distinct().OrderBy(x => x))
        {
            var indexes = Enumerable.Range(0, ops.Count).Where(i => levels[i] == level).ToArray();
            var byShape = new Dictionary<string, OpGroup>(StringComparer.Ordinal);

            foreach (var opIndex in indexes)
            {
                var op = ops[opIndex];
                if (op.Kind is MutationKind.Create or MutationKind.Upsert)
                {
                    var shape = ShapeKey(op, opToGroup);
                    if (!byShape.TryGetValue(shape, out var group))
                    {
                        group = new OpGroup(groups.Count, op.Entity.Id, op.Kind, op);
                        byShape.Add(shape, group);
                        groups.Add(group);
                    }

                    group.OpIndexes.Add(opIndex);
                    opToGroup[opIndex] = group.GroupId;
                }
                else
                {
                    var group = new OpGroup(groups.Count, op.Entity.Id, op.Kind, op);
                    group.OpIndexes.Add(opIndex);
                    groups.Add(group);
                    opToGroup[opIndex] = group.GroupId;
                }
            }

            foreach (var opIndex in indexes)
            {
                foreach (var field in ops[opIndex].Fields)
                {
                    if (field.Source is null)
                        continue;

                    var sourceGroup = opToGroup[field.Source.SourceOperationIndex];
                    if (!forcedReturns.TryGetValue(sourceGroup, out var set))
                        forcedReturns[sourceGroup] = set = [];
                    set.Add(field.Source.SourceField);
                }
            }
        }

        // A reference can only point to an earlier dependency level. If the plan
        // is malformed, do not silently turn it into a different execution plan.
        foreach (var dependency in plan.Dependencies)
        {
            if (dependency.SourceOperationIndex < 0 ||
                dependency.SourceOperationIndex >= ops.Count ||
                dependency.TargetOperationIndex < 0 ||
                dependency.TargetOperationIndex >= ops.Count ||
                levels[dependency.TargetOperationIndex] <= levels[dependency.SourceOperationIndex])
            {
                throw new InvalidOperationException(
                    $"Invalid mutation dependency {dependency.SourceOperationIndex} -> {dependency.TargetOperationIndex}.");
            }
        }

        var sql = new StringBuilder("WITH ");
        var parameters = new List<SqlParameterBinding>();
        var output = new List<GroupOutputMeta>(groups.Count);

        for (var i = 0; i < groups.Count; i++)
        {
            if (i > 0)
                sql.Append(",\n");

            var group = groups[i];
            forcedReturns.TryGetValue(group.GroupId, out var forced);
            forced ??= [];

            GroupOutputMeta meta = group.Kind switch
            {
                MutationKind.Create =>
                    WriteCreateOrUpsertGroup(sql, parameters, group, ops, output, forced, false),
                MutationKind.Upsert =>
                    WriteCreateOrUpsertGroup(sql, parameters, group, ops, output, forced, true),
                MutationKind.Update =>
                    WriteUpdateOrDeleteGroup(sql, parameters, group, ops[group.OpIndexes[0]], forced, false),
                MutationKind.Delete =>
                    WriteUpdateOrDeleteGroup(sql, parameters, group, ops[group.OpIndexes[0]], forced, true),
                _ => throw new NotSupportedException($"Unsupported mutation kind '{group.Kind}'.")
            };

            output.Add(meta);
        }

        sql.Append("\nSELECT * FROM (\n");
        for (var i = 0; i < output.Count; i++)
        {
            if (i > 0)
                sql.Append("\nUNION ALL\n");

            var meta = output[i];
            sql.Append("  SELECT ").Append(i).Append(" AS __grp, ");

            // Ordinal-addressable mutation groups must expose the correlation
            // ordinal as an actual JSON property. Do not rely on PostgreSQL's
            // row_to_json(record) preserving the synthetic CTE column when the
            // record shape is assembled through UNION/CTE projections.
            //
            // The executor treats __ord as part of the protocol, not as a
            // user-returned field. Explicitly construct it and merge the
            // remaining row properties so unchanged ON CONFLICT rows and
            // INSERT/UPDATE rows follow exactly the same correlation contract.
            if (meta.IsOrdinalAddressable)
            {
                sql.Append("jsonb_build_object('__ord', f.__ord) || (to_jsonb(f) - '__ord')");
            }
            else
            {
                sql.Append("to_jsonb(f)");
            }

            sql.Append(" AS __row FROM ")
               .Append(meta.IsOrdinalAddressable && meta.OrdMapCteName is not null
                   ? meta.OrdMapCteName
                   : meta.ResultCteName)
               .Append(" f");

            if (meta.IsSingleResult)
                sql.Append("\n  LIMIT 1");
        }
        sql.Append("\n) __all ORDER BY __grp");

        var rowKeys = new List<BatchedOperationRowKey>(ops.Count);
        for (var opIndex = 0; opIndex < ops.Count; opIndex++)
        {
            var group = groups[opToGroup[opIndex]];
            var ordinal = group.OpIndexes.IndexOf(opIndex) + 1;
            rowKeys.Add(new BatchedOperationRowKey(opIndex, group.GroupId, ordinal));
        }

        var groupMetas = output.Select(x => new BatchedGroupMeta(
            x.GroupId,
            x.OpIndexes.ToArray(),
            x.IsOrdinalAddressable,
            x.ReturnedFieldTypes)).ToArray();

        return new SqlBatchedMutationPlan(
            sql.ToString(),
            parameters,
            groupMetas,
            rowKeys,
            plan.Dependencies);
    }

    public SqlBatchedMutationPlan? TryCompile(MutationBatchPlan plan)
    {
        try
        {
            return Compile(plan);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private GroupOutputMeta WriteCreateOrUpsertGroup(
        StringBuilder sql,
        List<SqlParameterBinding> parameters,
        OpGroup group,
        IReadOnlyList<MutationOperation> operations,
        IReadOnlyList<GroupOutputMeta> priorGroups,
        HashSet<FieldId> forcedReturns,
        bool isUpsert)
    {
        var entity = _metadata.GetEntity(group.Entity);
        var rows = group.OpIndexes.Select(i => operations[i]).ToArray();
        var columns = group.Template.Fields.Select(x => x.Column).ToArray();

        if (columns.Length == 0)
            throw new InvalidOperationException($"Mutation group {group.GroupId} has no fields.");

        var conflicts = isUpsert
            ? group.Template.ConflictColumns?.ToArray()
              ?? (entity.PrimaryKey is { } pk ? [pk.ColumnId] : Array.Empty<ColumnId>())
            : columns.Where(c => rows.All(r => r.Fields.First(f => f.Column == c).Source is null)).ToArray();

        if (isUpsert && conflicts.Length == 0)
            throw new InvalidOperationException($"Upsert '{entity.Name}' has no conflict identity.");

        if (isUpsert && conflicts.Any(c => !columns.Contains(c)))
            throw new NotSupportedException(
                $"Upsert '{entity.Name}' does not supply every conflict column; falling back to sequential execution.");

        if (isUpsert && rows.Any(r => conflicts.Any(c =>
                r.Fields.First(f => f.Column == c).Source is null &&
                r.Fields.First(f => f.Column == c).Value is null)))
            throw new NotSupportedException(
                $"Upsert '{entity.Name}' contains a NULL literal conflict value; falling back to sequential execution.");

        if (!isUpsert && rows.Length > 1 && conflicts.Length == 0)
            throw new NotSupportedException(
                $"Create '{entity.Name}' has no literal-valued column with which to correlate batched rows.");

        if (!isUpsert && rows.Length > 1 && HasDuplicateLiteralKey(rows, conflicts))
            throw new NotSupportedException(
                $"Create '{entity.Name}' contains duplicate literal correlation keys; falling back to sequential execution.");

        var columnNames = columns.Select(c => ResolveColumn(entity, c)).ToArray();
        var conflictNames = conflicts.Select(c => ResolveColumn(entity, c)).ToArray();

        var src = $"g{group.GroupId}_src";
        var keys = $"g{group.GroupId}_keys";
        var ins = $"g{group.GroupId}_ins";
        var final = $"g{group.GroupId}_final";
        var ordmap = $"g{group.GroupId}_ordmap";

        var sourceExpressions = new List<string>();
        var sourceAliases = new List<string>();

        foreach (var column in columns)
        {
            var field = rows[0].Fields.First(f => f.Column == column);
            var name = ResolveColumn(entity, column);

            if (field.Source is null)
            {
                var values = rows
                    .Select(r => r.Fields.First(f => f.Column == column).Value)
                    .ToArray();

                var parameterName = BindTypedArrayParameter(
                    parameters,
                    $"g{group.GroupId}_{name}",
                    values);

                sourceExpressions.Add("@" + parameterName);
                sourceAliases.Add(name);
            }
            else
            {
                var sourceOperationIndexes = rows.Select(r =>
                    r.Fields.First(f => f.Column == column).Source!.SourceOperationIndex).ToArray();

                var ordinals = new object?[rows.Length];
                for (var i = 0; i < rows.Length; i++)
                {
                    var sourceIndex = sourceOperationIndexes[i];
                    var sourceGroup = priorGroups.FirstOrDefault(g => g.OpIndexes.Contains(sourceIndex));
                    if (sourceGroup is null || !sourceGroup.IsOrdinalAddressable)
                        throw new NotSupportedException(
                            $"Mutation operation {sourceIndex} cannot be used as a batched reference source.");

                    ordinals[i] = GroupOrdinal(sourceGroup.OpIndexes, sourceIndex);
                }

                var parameterName = BindTypedArrayParameter(
                    parameters,
                    $"g{group.GroupId}_{name}_ord",
                    ordinals);

                sourceExpressions.Add("@" + parameterName);
                sourceAliases.Add(name + "__ord");
            }
        }

        sql.Append(src)
           .Append(" AS (\n  SELECT * FROM unnest(")
           .Append(string.Join(", ", sourceExpressions))
           .Append(") WITH ORDINALITY AS s(")
           .Append(string.Join(", ", sourceAliases.Select(Q)))
           .Append(", __ord)\n)");

        sql.Append(",\n");

        // Resolve reference columns once in a sibling CTE. This is also used to
        // produce the key values used by the ord-map.
        var resolved = $"g{group.GroupId}_resolved";
        sql.Append(resolved).Append(" AS (\n  SELECT s.__ord");

        foreach (var column in columns)
        {
            var field = rows[0].Fields.First(f => f.Column == column);
            var name = ResolveColumn(entity, column);
            sql.Append(", ");

            if (field.Source is null)
            {
                sql.Append("s.").Append(Q(name)).Append(" AS ").Append(Q(name));
            }
            else
            {
                var source = priorGroups.FirstOrDefault(
                    g => g.OpIndexes.Contains(field.Source.SourceOperationIndex))
                    ?? throw new NotSupportedException(
                        $"Source operation {field.Source.SourceOperationIndex} is not available to resolve references.");

                var sourceField = source.ReturnedFieldNames.TryGetValue(
                    field.Source.SourceField,
                    out var returnName)
                    ? returnName
                    : throw new NotSupportedException(
                        $"Source operation {field.Source.SourceOperationIndex} does not return field " +
                        $"{field.Source.SourceField.Value}.");

                sql.Append("p").Append(column.Value)
                   .Append(".").Append(Q(sourceField))
                   .Append(" AS ").Append(Q(name));
            }
        }

        sql.Append("\n  FROM ").Append(src).Append(" s");

        var referenceColumns = columns
            .Where(c => rows[0].Fields.First(f => f.Column == c).Source is not null)
            .ToArray();

        var joinAliases = new Dictionary<ColumnId, string>();
        foreach (var column in referenceColumns)
        {
            var field = rows[0].Fields.First(f => f.Column == column);
            var source = priorGroups.First(g => g.OpIndexes.Contains(field.Source!.SourceOperationIndex));
            var alias = "p" + column.Value;
            joinAliases[column] = alias;

            sql.Append("\n  JOIN ").Append(source.OrdMapCteName)
               .Append(' ').Append(alias)
               .Append(" ON ").Append(alias).Append(".__ord = s.")
               .Append(Q(ResolveColumn(entity, column) + "__ord"));
        }
        sql.Append("\n)");

        var input = $"g{group.GroupId}_input";
        if (isUpsert)
        {
            sql.Append(",\n").Append(input).Append(" AS (\n  SELECT DISTINCT ON (")
               .Append(string.Join(", ", conflictNames.Select(Q)))
               .Append(") * FROM ").Append(resolved)
               .Append("\n  ORDER BY ")
               .Append(string.Join(", ", conflictNames.Select(Q)))
               .Append(", __ord DESC\n)");
        }
        else
        {
            sql.Append(",\n").Append(input).Append(" AS (SELECT * FROM ").Append(resolved).Append(")");
        }

        sql.Append(",\n").Append(keys).Append(" AS (\n  SELECT __ord, ")
           .Append(string.Join(", ", conflictNames.Select(c => Q(c))))
           .Append("\n  FROM ").Append(input).Append("\n)");
        var requestedFields = (group.Template.ReturnFields is { Count: > 0 }
                ? group.Template.ReturnFields
                : entity.EffectiveFields.Where(f => f.Column is not null).Select(f => f.Id).ToArray())
            .Concat(forcedReturns)
            .Distinct()
            .ToArray();

        var returnColumns = BuildReturnColumns(entity, requestedFields, conflicts);
        var returnedTypes = BuildReturnedTypes(entity, requestedFields);
        var returnNames = requestedFields.ToDictionary(
            fieldId => fieldId,
            fieldId => "r_" + fieldId.Value);

        sql.Append(",\n").Append(ins).Append(" AS (\n  INSERT INTO ")
           .Append(Table(entity.EffectiveStorageName))
           .Append(" (").Append(string.Join(", ", columnNames.Select(Q))).Append(")\n")
           .Append("  SELECT ")
           .Append(string.Join(", ", columnNames.Select(c => "r." + Q(c))))
           .Append("\n  FROM ").Append(input).Append(" r");

        if (isUpsert)
        {
            var updateColumns = columnNames
                .Where((_, i) => !conflicts.Contains(columns[i]))
                .ToArray();

            sql.Append("\n  ON CONFLICT (")
               .Append(string.Join(", ", conflictNames.Select(Q)))
               .Append(") ");

            if (updateColumns.Length == 0)
            {
                sql.Append("DO NOTHING");
            }
            else
            {
                sql.Append("DO UPDATE SET ")
                   .Append(string.Join(", ", updateColumns.Select(c =>
                       $"{Q(c)} = EXCLUDED.{Q(c)}")))
                   .Append("\n  WHERE ")
                   .Append(string.Join(" OR ", updateColumns.Select(c =>
                       $"{Table(entity.EffectiveStorageName)}.{Q(c)} IS DISTINCT FROM EXCLUDED.{Q(c)}")));
            }
        }

        sql.Append("\n  RETURNING ")
           .Append(string.Join(", ", returnColumns.Select(x =>
               Q(x.ColumnName) + " AS " + Q(x.Alias))))
           .Append("\n)");

        if (isUpsert)
        {
            sql.Append(",\n").Append(final).Append(" AS (\n  SELECT * FROM ").Append(ins);

            sql.Append("\n  UNION ALL\n  SELECT ")
               .Append(string.Join(", ", returnColumns.Select(x =>
                   "t." + Q(x.ColumnName) + " AS " + Q(x.Alias))))
               .Append("\n  FROM ").Append(Table(entity.EffectiveStorageName)).Append(" t\n")
               .Append("  JOIN ").Append(keys).Append(" k ON ")
               .Append(string.Join(" AND ", conflictNames.Select(c =>
                   $"t.{Q(c)} IS NOT DISTINCT FROM k.{Q(c)}")))
               .Append("\n  WHERE NOT EXISTS (SELECT 1 FROM ").Append(ins).Append(" i WHERE ")
               .Append(string.Join(" AND ", conflictNames.Select(c =>
                   $"i.{Q(ReturnAliasForColumn(returnColumns, c))} IS NOT DISTINCT FROM k.{Q(c)}")))
               .Append(")\n)");
        }
        else
        {
            sql.Append(",\n").Append(final).Append(" AS (SELECT * FROM ").Append(ins).Append(")");
        }

        // Always materialize ordinal correlation for Create/Upsert. The final
        // result set reads from this CTE so __ord survives row_to_json().
        sql.Append(",\n").Append(ordmap).Append(" AS (\n  SELECT k.__ord, f.*\n")
           .Append("  FROM ").Append(keys).Append(" k\n")
           .Append("  JOIN ").Append(final).Append(" f ON ")
           .Append(string.Join(" AND ", conflictNames.Select(c =>
               $"k.{Q(c)} IS NOT DISTINCT FROM f.{Q(ReturnAliasForColumn(returnColumns, c))}")))
           .Append("\n)");

        return new GroupOutputMeta(
            group.GroupId,
            final,
            ordmap,
            group.OpIndexes.ToArray(),
            true,
            false,
            returnNames,
            returnedTypes);
    }

    private GroupOutputMeta WriteUpdateOrDeleteGroup(
        StringBuilder sql,
        List<SqlParameterBinding> parameters,
        OpGroup group,
        MutationOperation operation,
        HashSet<FieldId> forcedReturns,
        bool isDelete)
    {
        var entity = _metadata.GetEntity(group.Entity);
        var returnFields = (
                operation.ReturnFields is { Count: > 0 }
                    ? operation.ReturnFields
                    : (!isDelete
                        ? entity.EffectiveFields.Where(f => f.Column is not null).Select(f => f.Id).ToArray()
                        : Array.Empty<FieldId>()))
            .Concat(forcedReturns)
            .Distinct()
            .ToArray();

        if (isDelete && forcedReturns.Count > 0)
            throw new NotSupportedException(
                "A Delete cannot currently be a source of a batched reference; falling back to sequential execution.");

        var effective = operation with
        {
            ReturnFields = returnFields.Length == 0 && !isDelete ? null : returnFields
        };

        var single = new SqlMutationCompiler(_metadata).Compile(new MutationPlan([effective]));
        var offset = parameters.Count;

        var rewritten = Regex.Replace(
            single.CommandText,
            @"@p(\d+)",
            m => "@p" + (offset + int.Parse(m.Groups[1].Value)));

        if (isDelete && !rewritten.Contains(" RETURNING ", StringComparison.OrdinalIgnoreCase))
            rewritten += " RETURNING 1 AS \"__affected\"";

        sql.Append($"g{group.GroupId}_op AS (\n  ")
           .Append(rewritten)
           .Append("\n)");

        parameters.AddRange(single.Parameters.Select(p =>
            p with { Name = "p" + (offset + int.Parse(p.Name[1..])) }));

        var resultNames = new Dictionary<FieldId, string>();
        var resultTypes = new Dictionary<FieldId, Type>();

        foreach (var fieldId in returnFields)
        {
            var field = entity.EffectiveFields.First(f => f.Id == fieldId);
            resultNames[fieldId] = "r_" + fieldId.Value;
            resultTypes[fieldId] = field.ClrType;
        }

        var resultCte = $"g{group.GroupId}_op";
        if (!isDelete)
        {
            var ordmap = $"g{group.GroupId}_ordmap";
            sql.Append(",\n").Append(ordmap)
               .Append(" AS (SELECT 1 AS __ord, f.* FROM ")
               .Append(resultCte)
               .Append(" f LIMIT 1)");

            return new GroupOutputMeta(
                group.GroupId,
                ordmap,
                ordmap,
                group.OpIndexes.ToArray(),
                true,
                true,
                resultNames,
                resultTypes);
        }

        return new GroupOutputMeta(
            group.GroupId,
            resultCte,
            null,
            group.OpIndexes.ToArray(),
            false,
            true,
            resultNames,
            resultTypes);
    }

    private static IReadOnlyList<ReturnColumn> BuildReturnColumns(
        EntityMetadata entity,
        IReadOnlyList<FieldId> requestedFields,
        IReadOnlyList<ColumnId> conflicts)
    {
        var result = new List<ReturnColumn>();
        foreach (var fieldId in requestedFields)
        {
            var field = entity.EffectiveFields.FirstOrDefault(f => f.Id == fieldId)
                ?? throw new InvalidOperationException($"Unknown return field '{fieldId.Value}'.");
            if (field.Column is null)
                throw new InvalidOperationException($"Return field '{field.Name}' has no storage column.");

            result.Add(new ReturnColumn(
                fieldId,
                field.Column.ColumnId,
                ResolveColumn(entity, field.Column.ColumnId),
                "r_" + fieldId.Value));
        }

        foreach (var conflict in conflicts)
        {
            if (result.Any(x => x.ColumnId == conflict))
                continue;

            result.Add(new ReturnColumn(
                default,
                conflict,
                ResolveColumn(entity, conflict),
                "r__k_" + ResolveColumn(entity, conflict)));
        }

        return result;
    }

    private static Dictionary<FieldId, Type> BuildReturnedTypes(
        EntityMetadata entity,
        IReadOnlyList<FieldId> fields)
    {
        var result = new Dictionary<FieldId, Type>();
        foreach (var fieldId in fields)
        {
            var field = entity.EffectiveFields.FirstOrDefault(f => f.Id == fieldId);
            if (field is not null)
                result[fieldId] = field.ClrType;
        }
        return result;
    }

    private static string ReturnAliasForColumn(
        IReadOnlyList<ReturnColumn> columns,
        string columnName)
    {
        var match = columns.FirstOrDefault(x => x.ColumnName == columnName);
        if (match is null)
            throw new InvalidOperationException($"Return column '{columnName}' was not projected.");
        return match.Alias;
    }

    private static bool HasDuplicateLiteralKey(
        IReadOnlyList<MutationOperation> rows,
        IReadOnlyList<ColumnId> keyColumns)
    {
        if (keyColumns.Count == 0)
            return true;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var values = keyColumns.Select(c => row.Fields.First(f => f.Column == c).Value);
            var key = string.Join("\u001f", values.Select(StableValue));
            if (!seen.Add(key))
                return true;
        }
        return false;
    }

    private static string StableValue(object? value)
    {
        if (value is null)
            return "<null>";
        if (value is byte[] bytes)
            return Convert.ToBase64String(bytes);
        return value.GetType().AssemblyQualifiedName + ":" + value;
    }

    private static string BindTypedArrayParameter(
        List<SqlParameterBinding> parameters,
        string hint,
        object?[] values)
    {
        var first = values.FirstOrDefault(v => v is not null);
        if (first is null)
            throw new NotSupportedException(
                $"Column '{hint}' is NULL for every row in its batch group; its PostgreSQL array type cannot be inferred.");

        var elementType = first.GetType();
        if (values.Any(v => v is not null && !elementType.IsInstanceOfType(v)))
            throw new NotSupportedException(
                $"Column '{hint}' contains heterogeneous CLR value types; falling back to sequential execution.");

        var hasNull = values.Any(v => v is null);

        if (hasNull && elementType.IsValueType && Nullable.GetUnderlyingType(elementType) is null)
            elementType = typeof(Nullable<>).MakeGenericType(elementType);

        var array = Array.CreateInstance(elementType, values.Length);
        for (var i = 0; i < values.Length; i++)
            array.SetValue(values[i], i);

        var name = "p" + parameters.Count;
        parameters.Add(new SqlParameterBinding(name, array));
        return name;
    }

    private static string ShapeKey(MutationOperation operation, int[] opToGroup)
    {
        var fields = operation.Fields
            .OrderBy(f => f.Column.Value)
            .Select(f => f.Source is null
                ? $"{f.Column.Value}:lit"
                : $"{f.Column.Value}:ref:{opToGroup[f.Source.SourceOperationIndex]}:{f.Source.SourceField.Value}");

        var conflicts = operation.ConflictColumns is null
            ? ""
            : string.Join(",", operation.ConflictColumns.OrderBy(x => x.Value).Select(x => x.Value));

        var returns = operation.ReturnFields is null
            ? ""
            : string.Join(",", operation.ReturnFields.OrderBy(x => x.Value).Select(x => x.Value));

        return string.Join("|",
            operation.Entity.Id.Value,
            operation.Kind,
            string.Join(",", fields),
            conflicts,
            returns);
    }

    private static int[] ComputeLevels(
        int count,
        IReadOnlyList<MutationDependency> dependencies)
    {
        var incoming = dependencies
            .GroupBy(x => x.TargetOperationIndex)
            .ToDictionary(x => x.Key, x => x.ToArray());

        var level = new int[count];
        var visiting = new bool[count];
        var visited = new bool[count];

        int Visit(int index)
        {
            if (visited[index])
                return level[index];
            if (visiting[index])
                throw new InvalidOperationException("Mutation dependency graph contains a cycle.");

            visiting[index] = true;
            if (incoming.TryGetValue(index, out var deps))
            {
                var max = 0;
                foreach (var dependency in deps)
                {
                    if (dependency.SourceOperationIndex < 0 ||
                        dependency.SourceOperationIndex >= count)
                        throw new InvalidOperationException("Mutation dependency references an invalid source operation.");

                    max = Math.Max(max, Visit(dependency.SourceOperationIndex) + 1);
                }
                level[index] = max;
            }

            visiting[index] = false;
            visited[index] = true;
            return level[index];
        }

        for (var i = 0; i < count; i++)
            Visit(i);

        return level;
    }
    
    private static int GroupOrdinal(IReadOnlyList<int> operationIndexes, int operationIndex)
    {
        for (var i = 0; i < operationIndexes.Count; i++)
        {
            if (operationIndexes[i] == operationIndex)
                return i + 1;
        }

        throw new InvalidOperationException(
            $"Operation {operationIndex} is not present in its mutation group.");
    }

    private static string ResolveColumn(EntityMetadata entity, ColumnId id) =>
        entity.Columns.FirstOrDefault(c => c.Id == id)?.EffectiveStorageName
        ?? throw new InvalidOperationException(
            $"Column '{id.Value}' is not registered on '{entity.Name}'.");

    private static string Q(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static string Table(string storageName) =>
        string.Join(".",
            storageName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Q));

    private sealed class OpGroup(
        int groupId,
        EntityId entity,
        MutationKind kind,
        MutationOperation template)
    {
        public int GroupId { get; } = groupId;
        public EntityId Entity { get; } = entity;
        public MutationKind Kind { get; } = kind;
        public MutationOperation Template { get; } = template;
        public List<int> OpIndexes { get; } = [];
    }

    private sealed record ReturnColumn(
        FieldId FieldId,
        ColumnId ColumnId,
        string ColumnName,
        string Alias);

    private sealed record GroupOutputMeta(
        int GroupId,
        string ResultCteName,
        string? OrdMapCteName,
        IReadOnlyList<int> OpIndexes,
        bool IsOrdinalAddressable,
        bool IsSingleResult,
        Dictionary<FieldId, string> ReturnedFieldNames,
        Dictionary<FieldId, Type> ReturnedFieldTypes);
}