
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public static class MutationPlannerRuntime
{
    public static void Build(
    in MutationIR node,
    ref MutationPlanBuilder builder,
    MutationEntityMetadata metadata)
{
    var valuesByRow =
        new Dictionary<
            MutationRowKey,
            ImmutableArray<FieldValue>.Builder>();

    var lookupsByRow =
        new Dictionary<
            MutationRowKey,
            ImmutableArray<LookupValue>.Builder>();

    foreach (var value in node.Values)
    {
        if (!metadata.TryResolveField(
                value.FieldId,
                out var field))
        {
            continue;
        }

        //
        // Navigation keys are not written directly.
        // They become FK lookups.
        //
        if (field.IsNavigationKey)
        {
            var spec =
                metadata.CteUpdateMeta.FirstOrDefault(x =>
                    string.Equals(
                        x.NavigationAlias + "Key",
                        field.FieldName,
                        StringComparison.OrdinalIgnoreCase));

            if (spec == null)
            {
                continue;
            }

            var key =
                new MutationRowKey(
                    field.EntityId,
                    field.StorageEntityId,
                    spec.NavigationAlias);

            if (!lookupsByRow.TryGetValue(
                    key,
                    out var lookupValues))
            {
                lookupValues =
                    ImmutableArray.CreateBuilder<LookupValue>();

                lookupsByRow[key] = lookupValues;
            }

            lookupValues.Add(
                new LookupValue(
                    field.ColumnId,
                    metadata.StorageEntityId,
                    spec.RelatedNaturalKeyColumnId,
                    spec.RelatedSurrogateIdColumnId,
                    value.RawValue,
                    spec.NavigationAlias +
                    spec.RelatedEntityTypeName));

            continue;
        }

        AddRow(
            valuesByRow,
            field.EntityId,
            field.StorageEntityId,
            node.OutputAlias,
            new FieldValue(
                field.EntityId,
                value.FieldId,
                field.ColumnId,
                value.RawValue));
    }

    foreach (var child in node.Children)
    {
        Build(
            child,
            ref builder,
            metadata);
    }

    foreach (var row in valuesByRow)
    {
        lookupsByRow.TryGetValue(
            row.Key,
            out var lookups);

        builder.AddRow(
            row.Key.EntityId,
            row.Key.StorageEntityId,
            row.Key.Alias,
            row.Value.ToImmutable(),
            null,
            null,
            lookups?.ToImmutable()
                ?? ImmutableArray<LookupValue>.Empty);
    }

    if (!metadata.IsRoot)
    {
        return;
    }

    if (metadata.Kind == MutationKind.GraphEdge)
    {
        BuildGraphEdgeRoot(
            node,
            ref builder,
            metadata);

        return;
    }

    if (metadata.Kind == MutationKind.Entity)
    {
        BuildEntityRoot(
            node,
            ref builder,
            metadata);
    }
}


    private static void BuildEntityRoot(
        in MutationIR node,
        ref MutationPlanBuilder builder,
        MutationEntityMetadata metadata)
    {
        var values =
            ImmutableArray.CreateBuilder<FieldValue>();

        foreach (var value in node.Values)
        {
            if (!metadata.TryResolveField(
                    value.FieldId,
                    out var field))
            {
                continue;
            }

            if (!field.IsPrimaryKey)
            {
                continue;
            }

            values.Add(
                new FieldValue(
                    field.EntityId,
                    field.FieldId,
                    field.ColumnId,
                    value.RawValue));
        }


        builder.AddCteRoot(
            new MutationCteNode(
                metadata.EntityId,
                metadata.StorageEntityId,
                node.OutputAlias,
                values.ToImmutable(),
                ImmutableArray<MutationCteNode>.Empty,
                metadata.Schema,
                metadata.Table,
                metadata.PrimaryColumns));
    }


    private static void BuildGraphEdgeRoot(
    in MutationIR node,
    ref MutationPlanBuilder builder,
    MutationEntityMetadata metadata)
{
    string? fromKey = null;
    string? toKey = null;
    string? edgeKey = null;

    var values =
        ImmutableArray.CreateBuilder<FieldValue>();

    var edgeProperties =
        ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.OrdinalIgnoreCase);


    foreach (var value in node.Values)
    {
        if (value.EntityId != metadata.EntityId &&
            !metadata.IsNavigationField(value.FieldId))
        {
            continue;
        }


        if (!metadata.TryResolveField(
                value.FieldId,
                out var field))
        {
            continue;
        }


        values.Add(
            new FieldValue(
                value.EntityId,
                value.FieldId,
                field.ColumnId,
                value.RawValue));


        //
        // Resolve graph endpoints from navigation metadata.
        //
        if (metadata.GraphFromFieldId.HasValue &&
            value.FieldId == metadata.GraphFromFieldId.Value)
        {
            fromKey = value.RawValue;
        }


        if (metadata.GraphToFieldId.HasValue &&
            value.FieldId == metadata.GraphToFieldId.Value)
        {
            toKey = value.RawValue;
        }


        //
        // Edge primary key.
        //
        if (field.IsPrimaryKey)
        {
            edgeKey = value.RawValue;
            continue;
        }


        //
        // Edge properties only.
        //
        if (!field.IsNavigationKey)
        {
            edgeProperties[
                field.ColumnId.ToString()] =
                    value.RawValue;
        }
    }


    if (metadata.GraphName != null &&
        metadata.GraphEdgeLabel != null)
    {
        if (fromKey == null || toKey == null)
        {
            throw new InvalidOperationException(
                $"Graph edge '{metadata.GraphEdgeLabel}' is missing endpoint keys. " +
                $"From='{fromKey}', To='{toKey}'. " +
                $"Expected fields: " +
                $"From={metadata.GraphFromFieldId}, " +
                $"To={metadata.GraphToFieldId}");
        }


        builder.AddGraphMerge(
            metadata.GraphName,
            metadata.GraphEdgeLabel,

            metadata.GraphFromVertex!,
            metadata.GraphFromColumn!,
            fromKey,

            metadata.GraphToVertex!,
            metadata.GraphToColumn!,
            toKey,

            metadata.PrimaryColumns.Length > 0
                ? metadata.PrimaryColumns[0]
                : string.Empty,

            edgeKey,

            edgeProperties.ToImmutable());
    }


    builder.AddCteRoot(
        new MutationCteNode(
            metadata.EntityId,
            metadata.StorageEntityId,
            node.OutputAlias,
            values.ToImmutable(),
            ImmutableArray<MutationCteNode>.Empty,
            metadata.Schema,
            metadata.Table,
            metadata.PrimaryColumns));
}


    private static void AddRow(
        Dictionary<
            MutationRowKey,
            ImmutableArray<FieldValue>.Builder> rows,
        ushort entityId,
        ushort storageEntityId,
        string alias,
        FieldValue value)
    {
        var key =
            new MutationRowKey(
                entityId,
                storageEntityId,
                alias);


        if (!rows.TryGetValue(key, out var values))
        {
            values =
                ImmutableArray.CreateBuilder<FieldValue>();

            rows[key] = values;
        }


        foreach (var existing in values)
        {
            if (existing.FieldId == value.FieldId)
            {
                return;
            }
        }


        values.Add(value);
    }
}


public readonly record struct MutationRowKey(
    ushort EntityId,
    ushort StorageEntityId,
    string Alias);
