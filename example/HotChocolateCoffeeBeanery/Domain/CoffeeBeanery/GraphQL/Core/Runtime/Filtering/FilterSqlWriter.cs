using System;
using System.Collections.Generic;
using System.Text;
using CoffeeBeanery.GraphQL.Core.Foundation;

namespace CoffeeBeanery.GraphQL.Core.Runtime.Filtering;

/// <summary>
/// Turns an EntityFilterMetadata tree into a parameterized SQL WHERE
/// clause -- always emits @pN placeholders and returns the bound values
/// separately (via FilterCompilationContext.Parameters); never inlines a
/// filter value into the SQL text. This is a hard requirement, not a
/// style choice: filter values come directly from the caller of a GraphQL
/// API, and this project has no other parameterization mechanism wired up
/// yet (ExecuteAndMaterializeAsync currently only ever runs a plain,
/// literal-free SQL string) -- inlining values here would be a real SQL
/// injection surface on a banking dataset. Whatever executes the returned
/// SQL MUST bind the returned parameters as actual command parameters,
/// never string-concatenate them.
///
/// SCOPE: only EntityFilterMetadata.Field is handled, and only for
/// columns on rootStorageEntityId -- Navigation and Collection filters
/// throw NotSupportedException (see RuntimeEntityMetadataRegistry remarks
/// for why: no runtime navigation map exists yet to resolve them
/// correctly, and resolving a non-root field's SQL alias isn't handled
/// either). Only Eq/Neq/In are handled -- WhereCompiler itself never
/// produces any other operator today.
/// </summary>
public static class FilterSqlWriter
{
    public static (string Sql, IReadOnlyDictionary<string, object?> Parameters) Write(
        EntityFilterMetadata filter,
        ushort rootStorageEntityId,
        string rootAlias)
    {
        var context =
            new FilterCompilationContext(rootStorageEntityId);

        var sb = new StringBuilder();

        WriteExpression(
            filter,
            rootStorageEntityId,
            rootAlias,
            context,
            sb);

        return (sb.ToString(), context.Parameters);
    }

    private static void WriteExpression(
        EntityFilterMetadata filter,
        ushort rootStorageEntityId,
        string rootAlias,
        FilterCompilationContext context,
        StringBuilder sb)
    {
        switch (filter)
        {
            case EntityFilterMetadata.Field field:
                WriteField(field, rootStorageEntityId, rootAlias, context, sb);
                break;

            case EntityFilterMetadata.And and:
                WriteConjunction(and.Items, "AND", rootStorageEntityId, rootAlias, context, sb);
                break;

            case EntityFilterMetadata.Or or:
                WriteConjunction(or.Items, "OR", rootStorageEntityId, rootAlias, context, sb);
                break;

            case EntityFilterMetadata.Navigation:
                throw new NotSupportedException(
                    "Navigation filters (e.g. 'customer: { firstName: { eq: \"Bob\" } }') " +
                    "are not supported yet -- see RuntimeEntityMetadataRegistry remarks.");

            case EntityFilterMetadata.Collection:
                throw new NotSupportedException(
                    "Collection filters ('some'/'all'/'none') are not supported yet -- " +
                    "see RuntimeEntityMetadataRegistry remarks.");

            default:
                throw new NotSupportedException(filter.GetType().Name);
        }
    }

    private static void WriteConjunction(
        IReadOnlyList<EntityFilterMetadata> items,
        string op,
        ushort rootStorageEntityId,
        string rootAlias,
        FilterCompilationContext context,
        StringBuilder sb)
    {
        if (items.Count == 0)
            return;

        sb.Append('(');

        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
                sb.Append(' ').Append(op).Append(' ');

            WriteExpression(items[i], rootStorageEntityId, rootAlias, context, sb);
        }

        sb.Append(')');
    }

    private static void WriteField(
        EntityFilterMetadata.Field field,
        ushort rootStorageEntityId,
        string rootAlias,
        FilterCompilationContext context,
        StringBuilder sb)
    {
        if (field.FieldMetadata.StorageEntityId != rootStorageEntityId)
        {
            throw new NotSupportedException(
                $"Filtering on field '{field.FieldMetadata.Name}' is not supported yet -- " +
                "it belongs to a joined/composite entity, not the query's root entity, and " +
                "alias resolution for non-root filter columns isn't implemented.");
        }

        var entity =
            GeneratedMetadata.GetEntity(field.FieldMetadata.StorageEntityId);

        var columnName =
            ResolveColumnName(entity, field.FieldMetadata.ColumnId);

        var columnRef =
            $"\"{rootAlias}\".\"{columnName}\"";

        switch (field.Operator)
        {
            case FilterOperator.Eq:
            {
                if (field.Value is null)
                {
                    sb.Append(columnRef).Append(" IS NULL");
                    break;
                }

                var param = context.AddParameter(field.Value);
                sb.Append(columnRef).Append(" = @").Append(param);
                break;
            }

            case FilterOperator.Neq:
            {
                if (field.Value is null)
                {
                    sb.Append(columnRef).Append(" IS NOT NULL");
                    break;
                }

                var param = context.AddParameter(field.Value);
                sb.Append(columnRef).Append(" <> @").Append(param);
                break;
            }

            case FilterOperator.In:
            {
                var items = FilterValue.NormalizeList(field.Value);

                if (items.Count == 0)
                {
                    // An empty IN list matches nothing -- no parameters
                    // needed, this is a structural constant, not a value
                    // that could ever come from user input.
                    sb.Append("1 = 0");
                    break;
                }

                sb.Append('(');

                for (var i = 0; i < items.Count; i++)
                {
                    if (i > 0)
                        sb.Append(" OR ");

                    var param = context.AddParameter(items[i]);
                    sb.Append(columnRef).Append(" = @").Append(param);
                }

                sb.Append(')');
                break;
            }

            default:
                throw new NotSupportedException(
                    $"Filter operator '{field.Operator}' is not supported yet.");
        }
    }

    private static string ResolveColumnName(
        CoffeeBeanery.GraphQL.Core.Foundation.Metadata.EntityMetadata entity,
        ushort columnId)
    {
        foreach (var column in entity.Columns)
        {
            if (column.Id.Value == columnId)
                return column.Name;
        }

        throw new InvalidOperationException(
            $"Column id {columnId} not found on entity '{entity.Name}'.");
    }
}
