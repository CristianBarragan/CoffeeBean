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
        var metadata = MutationMetadataRegistry.Get(entityId);

        var model = MutationMaterializerRegistry.Materialize(entityId, node, metadata);

        var childModels = ImmutableArray.CreateBuilder<object>(node.Children.Length);
        var childNodes = ImmutableArray.CreateBuilder<MutationCteNode>(node.Children.Length);

        foreach (var child in node.Children)
        {
            var (childCte, childModel) = BuildCore(child.EntityId, child, node, ref builder);
            childNodes.Add(childCte);
            childModels.Add(childModel);
        }

        var context = new MutationInterceptorContext(node, parent, childModels.ToImmutable());
        MutationInterceptorRegistry.Apply(entityId, model, context);

        var values = MutationDematerializerRegistry.Dematerialize(entityId, model, metadata);

        EmitRowsGroupedByStorageEntity(entityId, node, metadata, values, ref builder);

        if (metadata.Kind == MutationKind.GraphEdge)
        {
            EmitGraphMerge(node, metadata, ref builder);
        }
        
        var cteNode = new MutationCteNode(
            entityId,
            metadata.StorageEntityId,
            node.OutputAlias,
            values,
            childNodes.ToImmutable(),
            schemaOverride: metadata.Schema,
            tableOverride: metadata.Table);

        if (!values.IsEmpty)
        {
            builder.AddCteRoot(cteNode);
        }

        return (cteNode, model);
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

        var byStorageEntity = new Dictionary<ushort, ImmutableArray<FieldValue>.Builder>();

        foreach (var value in values)
        {
            if (!metadata.TryResolveFields(value.FieldId, out var targets))
                continue;

            var target = targets.FirstOrDefault(t => t.ColumnId == value.ColumnId);
            if (target is null)
                continue;

            if (target.StorageEntityId == metadata.StorageEntityId)
                continue;

            if (!byStorageEntity.TryGetValue(target.StorageEntityId, out var group))
            {
                group = ImmutableArray.CreateBuilder<FieldValue>();
                byStorageEntity[target.StorageEntityId] = group;
            }

            group.Add(value);
        }

        foreach (var (storageEntityId, group) in byStorageEntity)
        {
            builder.AddRow(entityId, storageEntityId, node.OutputAlias, group.ToImmutable(), null, null);
        }
    }

    private static void EmitGraphMerge(
        in MutationIR node,
        MutationEntityMetadata metadata,
        ref MutationPlanBuilder builder)
    {
        string? fromKey = null;
        string? toKey = null;
        string? edgeKey = null;

        foreach (var value in node.Values)
        {
            // InnerCustomerKey/OuterCustomerKey are intentionally excluded
            // from metadata.TryResolveField (IsNavigationKey = true) so they
            // never get treated as ordinary INSERT columns — that's the
            // whole point of the earlier fix. That means they must be read
            // here directly by FieldId, not through metadata resolution.
            if (value.FieldId == FieldId.CustomerCustomerEdge.InnerCustomerKey)
            {
                fromKey = value.RawValue;
            }
            else if (value.FieldId == FieldId.CustomerCustomerEdge.OuterCustomerKey)
            {
                toKey = value.RawValue;
            }
            else if (metadata.TryResolveField(value.FieldId, out var field) && field.IsPrimaryKey)
            {
                edgeKey = value.RawValue;
            }
        }

        if (fromKey == null || toKey == null)
            return;

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
            ImmutableDictionary<string, string>.Empty);
    }
}