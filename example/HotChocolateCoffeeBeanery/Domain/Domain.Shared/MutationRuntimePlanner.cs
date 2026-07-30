using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

public static class MutationRuntimePlanner
{
    public static void Build(
        ushort entityId,
        in MutationIR node,
        ref MutationPlanBuilder builder)
    {
        BuildCore(
            entityId,
            node,
            parent: null,
            ref builder);
    }


    public static (MutationCteNode Cte, object Model) BuildWithResult(
        ushort entityId,
        in MutationIR node,
        ref MutationPlanBuilder builder)
    {
        return BuildCore(
            entityId,
            node,
            parent: null,
            ref builder);
    }


    private static (MutationCteNode Cte, object Model) BuildCore(
        ushort entityId,
        in MutationIR node,
        in MutationIR? parent,
        ref MutationPlanBuilder builder)
    {
        var metadata =
            MutationMetadataRegistry.Get(entityId);


        var model =
            MutationMaterializerRegistry.Materialize(
                entityId,
                node,
                metadata);


        var childModels =
            ImmutableArray.CreateBuilder<object>(
                node.Children.Length);


        var childNodes =
            ImmutableArray.CreateBuilder<MutationCteNode>(
                node.Children.Length);


        foreach (var child in node.Children)
        {
            var result =
                BuildCore(
                    child.EntityId,
                    child,
                    node,
                    ref builder);


            childNodes.Add(result.Cte);

            childModels.Add(result.Model);
        }


        var context =
            new MutationInterceptorContext(
                node,
                parent,
                childModels.ToImmutable());


        MutationInterceptorRegistry.Apply(
            entityId,
            model,
            context);


        var values =
            MutationDematerializerRegistry.Dematerialize(
                entityId,
                model,
                metadata);



        //
        // Graph edge endpoint resolution.
        // Creates synthetic CTE children so the normal
        // CTE join resolver can resolve CustomerKey -> Id.
        //
        if (metadata.Kind == MutationKind.GraphEdge)
        {
            foreach (var spec in metadata.CteUpdateMeta)
            {
                var navigationValue =
                    values.FirstOrDefault(v =>
                        metadata.TryResolveField(
                            v.FieldId,
                            out var field) &&
                        field.IsNavigationKey &&
                        string.Equals(
                            field.FieldName,
                            spec.NavigationAlias + "Key",
                            StringComparison.OrdinalIgnoreCase));


                if (string.IsNullOrWhiteSpace(
                        navigationValue.RawValue))
                {
                    continue;
                }


                childNodes.Add(
                    new MutationCteNode(
                        EntityId.Customer,
                        StorageEntityId.Customer,
                        spec.NavigationAlias,
                        ImmutableArray.Create(
                            new FieldValue(
                                EntityId.Customer,
                                FieldId.Customer.CustomerKey,
                                ColumnId.Customer.CustomerKey,
                                navigationValue.RawValue)),
                        ImmutableArray<MutationCteNode>.Empty,
                        null,
                        null,
                        ImmutableArray<string>.Empty));
            }
        }



        //
        // Navigation keys are not database columns.
        //
        var filteredValues =
            values
                .Where(v =>
                {
                    metadata.TryResolveField(
                        v.FieldId,
                        out var field);


                    return field == null ||
                           !field.IsNavigationKey;
                })
                .ToImmutableArray();



        EmitRowsGroupedByStorageEntity(
            entityId,
            node,
            metadata,
            filteredValues,
            ref builder);



        if (metadata.Kind == MutationKind.GraphEdge)
        {
            EmitGraphMerge(
                node,
                metadata,
                ref builder);
        }



        if (metadata.IsRoot ||
            metadata.CteUpdateMeta.Length > 0)
        {
            var cte =
                new MutationCteNode(
                    entityId,
                    metadata.StorageEntityId,
                    node.OutputAlias,
                    filteredValues,
                    childNodes.ToImmutable(),
                    metadata.Schema,
                    metadata.Table);


            builder.AddCteRoot(cte);


            return (
                cte,
                model);
        }



        return (
            default,
            model);
    }



    private static void EmitRowsGroupedByStorageEntity(
        ushort entityId,
        in MutationIR node,
        MutationEntityMetadata metadata,
        ImmutableArray<FieldValue> values,
        ref MutationPlanBuilder builder)
    {
        if (values.Length == 0)
            return;


        var groups =
            new Dictionary<
                ushort,
                ImmutableArray<FieldValue>.Builder>();


        foreach (var value in values)
        {
            if (!metadata.TryResolveFields(
                    value.FieldId,
                    out var targets))
            {
                continue;
            }


            foreach (var target in targets)
            {
                if (!groups.TryGetValue(
                        target.StorageEntityId,
                        out var group))
                {
                    group =
                        ImmutableArray.CreateBuilder<FieldValue>();

                    groups[target.StorageEntityId] =
                        group;
                }


                if (target.ColumnId != value.ColumnId)
                    continue;


                group.Add(value);
            }
        }



        foreach (var group in groups)
        {
            builder.AddRow(
                entityId,
                group.Key,
                node.OutputAlias,
                group.Value.ToImmutable(),
                null,
                null);
        }
    }



    private static void EmitGraphMerge(
        in MutationIR node,
        MutationEntityMetadata metadata,
        ref MutationPlanBuilder builder)
    {
        if (metadata.GraphName == null ||
            metadata.GraphEdgeLabel == null ||
            metadata.GraphFromVertex == null ||
            metadata.GraphToVertex == null ||
            metadata.GraphFromFieldId == null ||
            metadata.GraphToFieldId == null)
        {
            return;
        }



        string? fromKey = null;
        string? toKey = null;
        string? edgeKey = null;



        foreach (var value in node.Values)
        {
            if (value.FieldId ==
                metadata.GraphFromFieldId.Value)
            {
                fromKey = value.RawValue;
            }


            if (value.FieldId ==
                metadata.GraphToFieldId.Value)
            {
                toKey = value.RawValue;
            }



            if (metadata.TryResolveFields(
                    value.FieldId,
                    out var fields))
            {
                foreach (var field in fields)
                {
                    if (field.IsPrimaryKey)
                    {
                        edgeKey = value.RawValue;
                    }
                }
            }
        }



        if (string.IsNullOrWhiteSpace(fromKey) ||
            string.IsNullOrWhiteSpace(toKey))
        {
            return;
        }



        var properties =
            ImmutableDictionary.CreateBuilder<string, string>(
                StringComparer.OrdinalIgnoreCase);



        foreach (var value in node.Values)
        {
            if (!metadata.TryResolveFields(
                    value.FieldId,
                    out var fields))
            {
                continue;
            }


            foreach (var field in fields)
            {
                if (field.IsNavigationKey ||
                    field.IsPrimaryKey)
                {
                    continue;
                }


                properties[field.FieldName] =
                    value.RawValue;
            }
        }



        builder.AddGraphMerge(
            metadata.GraphName,
            metadata.GraphEdgeLabel,
            metadata.GraphFromVertex,
            metadata.GraphFromColumn!,
            fromKey,
            metadata.GraphToVertex,
            metadata.GraphToColumn!,
            toKey,
            metadata.PrimaryColumns.Length > 0
                ? metadata.PrimaryColumns[0]
                : string.Empty,
            edgeKey,
            properties.ToImmutable());
    }
}