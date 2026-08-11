using System.Text;
using Foundgine.Metadata;
using Foundgine.Abstractions;
using Foundgine.Planning;
using Foundgine.Semantics.Query;
using Foundgine.Sql.Query;

namespace Foundgine.Sql;

/// <summary>
/// Compiles the provider-independent execution plan into SQL, including
/// filtering, ordering, aggregation, and cursor pagination.
/// </summary>
public sealed class SqlCompiler
{
    private readonly IMetadataProvider _metadata;

    public SqlCompiler(IMetadataProvider metadata) =>
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

    public SqlPlan Compile(ExecutionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var occurrences = new List<NodeOccurrence>();
        Collect(plan.Root, occurrences);
        var aliases = occurrences.ToDictionary(x => x.Node.Id, x => $"t{x.Node.Id}");
        var select = new List<string>();
        var bindings = new List<SqlColumnBinding>();
        var authorization = occurrences
            .Where(x => x.Node.Authorization is not null)
            .Select(x => new SqlAuthorizationPredicate(x.Node.Id, x.Node.Authorization!))
            .ToArray();

        foreach (var occurrence in occurrences)
        {
            var entity = _metadata.GetEntity(occurrence.Node.EntityId);
            var fields = occurrence.Node.Fields.Count == 0
                ? entity.EffectiveFields.Select(x => x.Id).ToArray()
                : occurrence.Node.Fields;

            foreach (var fieldId in fields)
                AddFieldSelection(occurrence.Node, entity, fieldId, aliases, select, bindings);
        }

        if (select.Count == 0)
            throw new InvalidOperationException("The execution plan selects no fields.");

        var parameters = new List<SqlParameterBinding>();
        var root = occurrences[0];
        var rootEntity = _metadata.GetEntity(root.Node.EntityId);
        var rootOptions = plan.Root.QueryOptions;
        var requestedOrder = rootOptions?.EffectiveOrder ?? [];
        var hasCursor = rootOptions?.Limit is > 0 && rootOptions.After is not null;
        var hasForwardPagination = rootOptions?.Limit is > 0 && rootOptions.Offset is null;

        if (rootOptions?.After is not null && rootOptions.Limit is not > 0)
            throw new InvalidOperationException("Cursor pagination requires a positive first/limit value.");

        if (rootOptions?.After is not null && rootOptions.Offset is not null)
            throw new NotSupportedException("Cursor pagination cannot be combined with offset pagination.");

        if (rootOptions?.Limit is < 0 || rootOptions?.Offset is < 0)
            throw new InvalidOperationException("Query limit and offset must be non-negative.");

        SqlPaginationPlan? pagination = null;
        IReadOnlyList<SemanticOrderTerm> effectiveCursorOrder = Array.Empty<SemanticOrderTerm>();
        var resolvedOrder = new List<ResolvedOrderTerm>();

        if (hasForwardPagination)
        {
            effectiveCursorOrder = BuildCursorOrder(rootEntity, requestedOrder);
        }

        var orderTerms = hasForwardPagination ? effectiveCursorOrder : requestedOrder;
        foreach (var term in orderTerms)
        {
            ExecutionPlanNode orderNode;
            EntityMetadata orderEntity;
            FieldMetadata field;

            if (term.IsAggregate)
            {
                if (term.EffectivePath.Count != 1)
                    throw new NotSupportedException("Collection aggregation currently supports one relationship hop.");

                orderNode = ResolveOrderParentNode(root.Node, term.EffectivePath);
                orderEntity = _metadata.GetEntity(_metadata.GetRelationship(term.EffectivePath[0]).Target);
                field = orderEntity.EffectiveFields.FirstOrDefault(x => x.Id == term.Field)
                    ?? throw new InvalidOperationException(
                        $"Unknown aggregate order field '{term.Field}' on '{orderEntity.Name}'.");
            }
            else
            {
                orderNode = ResolveOrderNode(root.Node, term.EffectivePath);
                orderEntity = _metadata.GetEntity(orderNode.EntityId);
                field = orderEntity.EffectiveFields.FirstOrDefault(x => x.Id == term.Field)
                    ?? throw new InvalidOperationException(
                        $"Unknown order field '{term.Field}' on '{orderEntity.Name}'.");
            }

            if (!term.IsAggregate && field.Column is null)
                throw new InvalidOperationException(
                    $"Order field '{orderEntity.Name}.{field.Name}' has no storage column mapping.");

            if (term.IsAggregate && term.EffectivePath.Count == 0)
                throw new InvalidOperationException("Aggregate ordering requires a collection relationship path.");

            var resolved = new ResolvedOrderTerm(term, orderNode, orderEntity, aliases[orderNode.Id], field);
            resolvedOrder.Add(resolved);

            if (hasForwardPagination)
            {
                var cursorBindings = pagination?.CursorValues is not null
                    ? pagination.CursorValues.ToList()
                    : new List<SqlCursorBinding>();

                if (term.IsAggregate)
                {
                    AddHiddenAggregateCursorSelection(
                        term,
                        orderNode,
                        orderEntity,
                        field,
                        aliases,
                        select,
                        bindings,
                        cursorBindings);
                }
                else
                {
                    AddHiddenCursorSelection(
                        orderNode,
                        orderEntity,
                        field,
                        term.Direction,
                        aliases,
                        select,
                        bindings,
                        cursorBindings);
                }

                pagination = new SqlPaginationPlan(
                    rootOptions!.Limit!.Value,
                    cursorBindings,
                    rootOptions.After);
            }
        }

        var sql = new StringBuilder();
        sql.Append("SELECT ").Append(string.Join(", ", select));
        sql.Append(" FROM ").Append(QuoteIdentifier(rootEntity.EffectiveStorageName));
        sql.Append(" ").Append(QuoteIdentifier(aliases[root.Node.Id]));

        foreach (var occurrence in occurrences.Skip(1))
        {
            if (occurrence.Node.ViaRelationship is null)
                throw new InvalidOperationException($"Node {occurrence.Node.Id} has no relationship to its parent.");

            var relationship = _metadata.GetRelationship(occurrence.Node.ViaRelationship.Value);
            var parentNode = occurrences.First(x => x.Node.Id == occurrence.ParentId).Node;
            var left = RenderJoinColumn(relationship.SourceKey, parentNode, occurrence.Node, aliases);
            var right = RenderJoinColumn(relationship.TargetKey, parentNode, occurrence.Node, aliases);

            sql.Append(" INNER JOIN ")
                .Append(QuoteIdentifier(_metadata.GetEntity(occurrence.Node.EntityId).EffectiveStorageName))
                .Append(" ").Append(QuoteIdentifier(aliases[occurrence.Node.Id]))
                .Append(" ON ").Append(left).Append(" = ").Append(right);
        }

        var where = SemanticQuerySqlWriter.WriteWhere(
            rootOptions?.Filter,
            rootEntity,
            aliases[root.Node.Id],
            parameters,
            _metadata);

        foreach (var occurrence in occurrences)
        {
            if (occurrence.Node.Authorization is null)
                continue;

            var entity = _metadata.GetEntity(occurrence.Node.EntityId);
            var predicateSql = Query.SqlAuthorizationWriter.Write(
                occurrence.Node.Authorization,
                entity,
                aliases[occurrence.Node.Id],
                parameters);

            where = string.IsNullOrWhiteSpace(where)
                ? predicateSql
                : $"({where}) AND ({predicateSql})";
        }

        if (hasCursor)
        {
            var cursorValues = CursorCodec.Decode(rootOptions!.After!);
            if (cursorValues.Count != effectiveCursorOrder.Count)
            {
                throw new InvalidOperationException(
                    $"The pagination cursor contains {cursorValues.Count} values, but the current ordering requires {effectiveCursorOrder.Count}.");
            }

            var seek = BuildSeekPredicate(
                resolvedOrder,
                cursorValues,
                parameters);

            where = string.IsNullOrWhiteSpace(where)
                ? seek
                : $"({where}) AND ({seek})";
        }

        if (!string.IsNullOrWhiteSpace(where))
            sql.Append(" WHERE ").Append(where);

        if (hasForwardPagination)
        {
            var order = WriteResolvedOrder(resolvedOrder);

            sql.Append(" ").Append(order);
            sql.Append(" LIMIT ").Append(rootOptions!.Limit!.Value + 1);
        }
        else
        {
            var order = WriteResolvedOrder(resolvedOrder);

            if (!string.IsNullOrWhiteSpace(order))
                sql.Append(" ").Append(order);

            if (rootOptions?.Offset is { } offset && rootOptions.Limit is null)
                sql.Append(" LIMIT -1");
            else if (rootOptions?.Limit is { } limit)
                sql.Append(" LIMIT ").Append(limit);

            if (rootOptions?.Offset is { } actualOffset)
                sql.Append(" OFFSET ").Append(actualOffset);
        }

        return new SqlPlan(sql.ToString(), bindings, parameters, pagination, authorization);
    }

    private void AddFieldSelection(
        ExecutionPlanNode node,
        EntityMetadata entity,
        FieldId fieldId,
        IReadOnlyDictionary<int, string> aliases,
        ICollection<string> select,
        ICollection<SqlColumnBinding> bindings)
    {
        var field = entity.EffectiveFields.FirstOrDefault(x => x.Id == fieldId)
            ?? throw new InvalidOperationException($"Entity '{entity.Name}' has no field '{fieldId}'.");

        if (field.Column is null)
            throw new InvalidOperationException($"Field '{entity.Name}.{field.Name}' has no storage column mapping.");

        var column = entity.Columns.FirstOrDefault(x => x.Id == field.Column.ColumnId)
            ?? throw new InvalidOperationException(
                $"Field '{entity.Name}.{field.Name}' references missing column '{field.Column.ColumnId}'.");

        var resultName = $"n{node.Id}_{field.Name}";
        select.Add(
            $"{QuoteIdentifier(aliases[node.Id])}.{QuoteIdentifier(column.EffectiveStorageName)} AS {QuoteIdentifier(resultName)}");

        bindings.Add(new SqlColumnBinding(
            resultName,
            entity.EntityId,
            field.Id,
            column.EffectiveStorageName,
            node.Id));
    }

    private void AddHiddenAggregateCursorSelection(
        SemanticOrderTerm term,
        ExecutionPlanNode node,
        EntityMetadata entity,
        FieldMetadata field,
        IReadOnlyDictionary<int, string> aliases,
        ICollection<string> select,
        ICollection<SqlColumnBinding> bindings,
        ICollection<SqlCursorBinding> cursorBindings)
    {
        var expression = BuildAggregateReference(term, node, entity, field, aliases);
        var resultName = $"__fg_cursor_agg_{node.Id}_{field.Name}_{term.Aggregate}";

        select.Add($"{expression} AS {QuoteIdentifier(resultName)}");
        var cursorType = term.Aggregate == SemanticOrderAggregate.Count ? typeof(long) : field.ClrType;

        var targetEntityId = _metadata.GetRelationship(term.EffectivePath[0]).Target;
        bindings.Add(new SqlColumnBinding(
            resultName,
            targetEntityId,
            field.Id,
            term.Aggregate.ToString(),
            node.Id,
            IsCursor: true));

        cursorBindings.Add(new SqlCursorBinding(
            resultName,
            targetEntityId,
            field.Id,
            cursorType,
            term.Direction));
    }

    private static void AddHiddenCursorSelection(
        ExecutionPlanNode node,
        EntityMetadata entity,
        FieldMetadata field,
        SemanticSortDirection direction,
        IReadOnlyDictionary<int, string> aliases,
        ICollection<string> select,
        List<SqlColumnBinding> bindings,
        ICollection<SqlCursorBinding> cursorBindings)
    {
        if (field.Column is null)
            throw new InvalidOperationException(
                $"Order field '{entity.Name}.{field.Name}' has no storage column mapping.");

        var column = entity.Columns.FirstOrDefault(x => x.Id == field.Column.ColumnId)
            ?? throw new InvalidOperationException(
                $"Order field '{entity.Name}.{field.Name}' references a missing column.");

        var resultName = $"__fg_cursor_{node.Id}_{field.Name}";
        var alreadySelected = bindings.Any(x =>
            x.NodeId == node.Id &&
            x.FieldId == field.Id);

        if (!alreadySelected)
        {
            select.Add(
                $"{QuoteIdentifier(aliases[node.Id])}.{QuoteIdentifier(column.EffectiveStorageName)} AS {QuoteIdentifier(resultName)}");

            bindings.Add(new SqlColumnBinding(
                resultName,
                entity.EntityId,
                field.Id,
                column.EffectiveStorageName,
                node.Id,
                IsCursor: true));
        }
        else
        {
            var existing = bindings.First(x =>
                x.NodeId == node.Id &&
                x.FieldId == field.Id);

            resultName = existing.ResultName;
            var index = bindings.IndexOf(existing);
            bindings[index] = existing with { IsCursor = true };
        }

        cursorBindings.Add(new SqlCursorBinding(
            resultName,
            entity.EntityId,
            field.Id,
            field.ClrType,
            direction));
    }

    private static IReadOnlyList<SemanticOrderTerm> BuildCursorOrder(
        EntityMetadata entity,
        IReadOnlyList<SemanticOrderTerm> requested)
    {
        var result = requested.ToList();

        if (entity.PrimaryKey is null)
            throw new InvalidOperationException(
                $"Entity '{entity.Name}' has no primary-key metadata required for cursor pagination.");

        var primaryKeyField = entity.EffectiveFields.FirstOrDefault(f => f.Column == entity.PrimaryKey)
            ?? throw new InvalidOperationException(
                $"Entity '{entity.Name}' primary key is not mapped to a semantic field.");

        if (!result.Any(x => x.Field == primaryKeyField.Id))
            result.Add(new SemanticOrderTerm(primaryKeyField.Id, SemanticSortDirection.Asc));

        return result;
    }

    private string BuildSeekPredicate(
        IReadOnlyList<ResolvedOrderTerm> order,
        IReadOnlyList<System.Text.Json.JsonElement> cursorValues,
        ICollection<SqlParameterBinding> parameters)
    {
        if (order.Count == 0)
            throw new InvalidOperationException("Cursor pagination requires at least one ordering term.");

        var branches = new List<string>();

        for (var i = 0; i < order.Count; i++)
        {
            var prefix = new List<string>();

            for (var j = 0; j < i; j++)
            {
                var equality = BuildOrderReference(order[j]);
                var equalityParameter = AddCursorParameter(
                    cursorValues[j],
                    order[j].Field.ClrType,
                    parameters);
                prefix.Add($"{equality} = @{equalityParameter}");
            }

            var reference = BuildOrderReference(order[i]);
            var parameter = AddCursorParameter(
                cursorValues[i],
                order[i].Field.ClrType,
                parameters);

            var comparison = order[i].Term.Direction == SemanticSortDirection.Desc ? "<" : ">";
            prefix.Add($"{reference} {comparison} @{parameter}");
            branches.Add("(" + string.Join(" AND ", prefix) + ")");
        }

        return "(" + string.Join(" OR ", branches) + ")";
    }

    private string BuildOrderReference(ResolvedOrderTerm term)
    {
        if (term.Term.IsAggregate)
            return BuildAggregateReference(term.Term, term.Node, term.Entity, term.Field,
                new Dictionary<int, string> { [term.Node.Id] = term.Alias });

        var field = term.Field;
        if (field.Column is null)
            throw new InvalidOperationException(
                $"Order field '{term.Entity.Name}.{field.Name}' has no storage column mapping.");

        var column = term.Entity.Columns.FirstOrDefault(x => x.Id == field.Column.ColumnId)
            ?? throw new InvalidOperationException(
                $"Order field '{term.Entity.Name}.{field.Name}' references a missing column.");

        return $"{QuoteIdentifier(term.Alias)}.{QuoteIdentifier(column.EffectiveStorageName)}";
    }

    private string BuildAggregateReference(
        SemanticOrderTerm term,
        ExecutionPlanNode sourceNode,
        EntityMetadata sourceEntity,
        FieldMetadata field,
        IReadOnlyDictionary<int, string> aliases)
    {
        if (term.EffectivePath.Count == 0)
            throw new InvalidOperationException("Aggregate ordering requires a relationship path.");

        if (term.EffectivePath.Count != 1)
            throw new NotSupportedException("Collection aggregation currently supports one relationship hop.");

        var relationshipId = term.EffectivePath[0];
        var relationship = _metadata.GetRelationship(relationshipId);
        var targetEntity = _metadata.GetEntity(relationship.Target);
        var targetAlias = "a" + sourceNode.Id + "_agg";
        var targetReference = relationship.TargetKey;
        var sourceReference = relationship.SourceKey;

        var targetColumn = targetEntity.Columns.FirstOrDefault(c => c.Id == targetReference.ColumnId)
            ?? throw new InvalidOperationException($"Target entity '{targetEntity.Name}' has no join column '{targetReference.ColumnId}'.");
        var sourceColumn = sourceEntity.Columns.FirstOrDefault(c => c.Id == sourceReference.ColumnId)
            ?? throw new InvalidOperationException($"Source entity '{sourceEntity.Name}' has no join column '{sourceReference.ColumnId}'.");

        var correlation = $"{QuoteIdentifier(targetAlias)}.{QuoteIdentifier(targetColumn.EffectiveStorageName)} = " +
                         $"{QuoteIdentifier(aliases[sourceNode.Id])}.{QuoteIdentifier(sourceColumn.EffectiveStorageName)}";

        string aggregate;
        if (term.Aggregate == SemanticOrderAggregate.Count)
        {
            aggregate = "COUNT(*)";
        }
        else
        {
            if (field.Column is null)
                throw new InvalidOperationException($"Aggregate field '{targetEntity.Name}.{field.Name}' has no storage column mapping.");

            var valueColumn = targetEntity.Columns.FirstOrDefault(c => c.Id == field.Column.ColumnId)
                ?? throw new InvalidOperationException($"Aggregate field '{targetEntity.Name}.{field.Name}' references a missing column.");

            aggregate = $"{(term.Aggregate == SemanticOrderAggregate.Min ? "MIN" : "MAX")}({QuoteIdentifier(targetAlias)}.{QuoteIdentifier(valueColumn.EffectiveStorageName)})";
        }

        return $"(SELECT {aggregate} FROM {QuoteIdentifier(targetEntity.EffectiveStorageName)} {QuoteIdentifier(targetAlias)} WHERE {correlation})";
    }

    private static string AddCursorParameter(
        System.Text.Json.JsonElement value,
        Type type,
        ICollection<SqlParameterBinding> parameters)
    {
        var parameter = "p" + parameters.Count;
        var converted = Query.CursorCodec.ConvertValue(value, type);
        parameters.Add(new SqlParameterBinding(parameter, converted));
        return parameter;
    }

    private string WriteResolvedOrder(IReadOnlyList<ResolvedOrderTerm> terms)
    {
        if (terms.Count == 0)
            return string.Empty;

        var parts = terms.Select(term =>
        {
            if (term.Term.IsAggregate)
            {
                var expression = BuildAggregateReference(
                    term.Term,
                    term.Node,
                    term.Entity,
                    term.Field,
                    new Dictionary<int, string> { [term.Node.Id] = term.Alias });
                return $"{expression} " + (term.Term.Direction == SemanticSortDirection.Desc ? "DESC" : "ASC");
            }

            if (term.Field.Column is null)
                throw new InvalidOperationException(
                    $"Order field '{term.Entity.Name}.{term.Field.Name}' has no storage column mapping.");

            var column = term.Entity.Columns.First(x => x.Id == term.Field.Column.ColumnId);
            return $"{QuoteIdentifier(term.Alias)}.{QuoteIdentifier(column.EffectiveStorageName)} " +
                   (term.Term.Direction == SemanticSortDirection.Desc ? "DESC" : "ASC");
        });

        return "ORDER BY " + string.Join(", ", parts);
    }

    private ExecutionPlanNode ResolveOrderParentNode(ExecutionPlanNode root, IReadOnlyList<RelationshipId> path)
    {
        if (path.Count == 0)
            return root;

        var current = root;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var relationshipId = path[i];
            current = current.Children.FirstOrDefault(x => x.ViaRelationship == relationshipId)
                ?? throw new InvalidOperationException(
                    $"Order path relationship '{relationshipId}' is not part of the execution plan.");
        }

        return current;
    }

    private ExecutionPlanNode ResolveOrderNode(ExecutionPlanNode root, IReadOnlyList<RelationshipId> path)
    {
        var current = root;
        foreach (var relationshipId in path)
        {
            current = current.Children.FirstOrDefault(x => x.ViaRelationship == relationshipId)
                ?? throw new InvalidOperationException(
                    $"Order path relationship '{relationshipId}' is not part of the execution plan. " +
                    "The relationship must be selected before it can be used for ordering.");
        }

        return current;
    }

    private sealed record ResolvedOrderTerm(
        SemanticOrderTerm Term,
        ExecutionPlanNode Node,
        EntityMetadata Entity,
        string Alias,
        FieldMetadata Field);

    private static void Collect(ExecutionPlanNode node, ICollection<NodeOccurrence> result, int? parentId = null)
    {
        result.Add(new NodeOccurrence(node, parentId));
        foreach (var child in node.Children)
            Collect(child, result, node.Id);
    }

    private string RenderJoinColumn(
        ColumnReference reference,
        ExecutionPlanNode parent,
        ExecutionPlanNode child,
        IReadOnlyDictionary<int, string> aliases)
    {
        var node = parent.EntityId == reference.EntityId
            ? parent
            : child.EntityId == reference.EntityId
                ? child
                : throw new InvalidOperationException(
                    $"Join column entity '{reference.EntityId}' does not match the relationship endpoints.");

        var entity = _metadata.GetEntity(reference.EntityId);
        var column = entity.Columns.FirstOrDefault(x => x.Id == reference.ColumnId)
            ?? throw new InvalidOperationException(
                $"Entity '{entity.Name}' has no column '{reference.ColumnId}'.");

        return $"{QuoteIdentifier(aliases[node.Id])}.{QuoteIdentifier(column.EffectiveStorageName)}";
    }

    internal static string QuoteIdentifier(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private sealed record NodeOccurrence(ExecutionPlanNode Node, int? ParentId);
}
