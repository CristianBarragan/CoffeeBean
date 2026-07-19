#nullable enable

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
        var rows =
            new Dictionary<
                MutationRowKey,
                ImmutableArray<FieldValue>.Builder>();

        foreach (var value in node.Values)
        {
            if (!metadata.TryResolveField(
                    value.FieldId,
                    out var field))
            {
                continue;
            }

            AddRow(
                rows,
                field.EntityId,
                field.StorageEntityId,
                node.OutputAlias,
                new FieldValue(
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


        foreach (var row in rows)
        {
            builder.AddRow(
                row.Key.EntityId,
                row.Key.StorageEntityId,
                row.Key.Alias,
                row.Value.ToImmutable(),
                null,
                null);
        }


        if (!metadata.IsRoot)
        {
            return;
        }


        switch (metadata.Kind)
        {
            case MutationKind.Entity:
                BuildEntityRoot(
                    node,
                    ref builder,
                    metadata);
                break;


            case MutationKind.GraphEdge:
                BuildGraphEdgeRoot(
                    node,
                    ref builder,
                    metadata);
                break;
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


        foreach (var value in node.Values)
        {
            if (!metadata.TryResolveField(
                    value.FieldId,
                    out var field))
            {
                continue;
            }


            // if (field.ColumnId ==
            //     ColumnId.CustomerCustomerRelationship.InnerCustomerKey)
            // {
            //     fromKey = value.RawValue;
            // }
            //
            //
            // if (field.ColumnId ==
            //     ColumnId.CustomerCustomerRelationship.OuterCustomerKey)
            // {
            //     toKey = value.RawValue;
            // }


            if (field.IsPrimaryKey)
            {
                edgeKey = value.RawValue;
            }
        }


        if (fromKey != null &&
            toKey != null)
        {
            builder.AddGraphMerge(
                metadata.GraphName!,
                metadata.GraphEdgeLabel!,
                metadata.GraphFromVertex!,
                metadata.GraphFromColumn!,
                fromKey,
                metadata.GraphToVertex!,
                metadata.GraphToColumn!,
                toKey,
                metadata.PrimaryColumns[0],
                edgeKey,
                ImmutableDictionary<string,string>.Empty);
        }


        builder.AddCteRoot(
            new MutationCteNode(
                metadata.EntityId,
                metadata.StorageEntityId,
                node.OutputAlias,
                ImmutableArray<FieldValue>.Empty,
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