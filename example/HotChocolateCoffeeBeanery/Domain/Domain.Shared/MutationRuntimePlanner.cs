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
        BuildCore(entityId, node, parent: null, ref builder);
    }


    public static (MutationCteNode Cte, object Model) BuildWithResult(
        ushort entityId,
        in MutationIR node,
        ref MutationPlanBuilder builder)
    {
        return BuildCore(entityId, node, parent: null, ref builder);
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


        EmitRowsGroupedByStorageEntity(
            entityId,
            node,
            metadata,
            values,
            ref builder);


        if (metadata.Kind == MutationKind.GraphEdge)
        {
            EmitGraphMerge(
                node,
                metadata,
                ref builder);
        }


        if (!metadata.IsRoot)
        {
            return (default, model);
        }


        var cte =
            new MutationCteNode(
                entityId,
                metadata.StorageEntityId,
                node.OutputAlias,
                values,
                childNodes.ToImmutable(),
                metadata.Schema,
                metadata.Table);


        builder.AddCteRoot(cte);


        return (cte, model);
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


        var byStorageEntity =
            new Dictionary<ushort, ImmutableArray<FieldValue>.Builder>();


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
                if (!byStorageEntity.TryGetValue(
                        target.StorageEntityId,
                        out var group))
                {
                    group =
                        ImmutableArray.CreateBuilder<FieldValue>();

                    byStorageEntity[target.StorageEntityId] =
                        group;
                }


                if (target.ColumnId != value.ColumnId)
                    continue;


                group.Add(value);
            }
        }


        foreach (var pair in byStorageEntity)
        {
            builder.AddRow(
                entityId,
                pair.Key,
                node.OutputAlias,
                pair.Value.ToImmutable(),
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
            if (metadata.GraphFromFieldId.Value == value.FieldId)
            {
                fromKey = value.RawValue;
            }


            if (metadata.GraphToFieldId.Value == value.FieldId)
            {
                toKey = value.RawValue;
            }


            if (!metadata.TryResolveFields(
                    value.FieldId,
                    out var fields))
            {
                continue;
            }


            foreach (var field in fields)
            {
                if (field.IsPrimaryKey)
                {
                    edgeKey = value.RawValue;
                }
            }
        }


        if (string.IsNullOrWhiteSpace(fromKey) ||
            string.IsNullOrWhiteSpace(toKey))
        {
            return;
        }


        var edgeProperties =
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
                if (field.IsNavigationKey)
                    continue;


                if (field.IsPrimaryKey)
                    continue;
                
                edgeProperties[field.FieldName] =
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
            edgeProperties.ToImmutable());
    }
}
    