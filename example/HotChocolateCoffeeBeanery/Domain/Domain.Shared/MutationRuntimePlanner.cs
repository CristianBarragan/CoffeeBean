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


    // For graph-edge models, synthesize a fake MutationCteNode per
    // endpoint navigation from the raw navigation-key value, so the
    // existing BuildCteNodeUpsertMerged natural-key-JOIN machinery
    // (originally built for real nested children) can resolve
    // InnerCustomerKey/OuterCustomerKey -> Customer.Id via JOIN, exactly
    // like it already does for ordinary composite children.
    if (metadata.Kind == MutationKind.GraphEdge)
    {
        var resolutions = metadata.CteUpdateMeta;

        foreach (var spec in resolutions)
        {
            var navValue = values.FirstOrDefault(v =>
                metadata.TryResolveField(v.FieldId, out var m) &&
                m.IsNavigationKey &&
                string.Equals(
                    m.FieldName,
                    spec.NavigationAlias + "Key",
                    StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(navValue.RawValue))
                continue;

            childNodes.Add(new MutationCteNode(
                entityId: EntityId.Customer,
                storageEntityId: StorageEntityId.Customer,
                alias: spec.NavigationAlias,
                values: ImmutableArray.Create(new FieldValue(
                    EntityId.Customer,
                    FieldId.Customer.CustomerKey,
                    ColumnId.Customer.CustomerKey,
                    navValue.RawValue)),
                children: ImmutableArray<MutationCteNode>.Empty));
        }
    }


    // Navigation-key values must never appear in the entity's own INSERT
    // column list — they have no real column (placeholder ColumnId = 0)
    // and are resolved via the synthesized child JOINs above instead.
    var filteredValues =
        values
            .Where(v =>
            {
                metadata.TryResolveField(v.FieldId, out var m);
                return m == null || !m.IsNavigationKey;
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


    // CTE-node/JOIN-resolution path fires whenever this entity is either
    // the true mutation root, OR it has its own natural-key resolutions
    // to perform (as graph-edge models do for their endpoint
    // navigations) — even though a graph-edge model is never itself
    // "IsRoot", it still needs BuildCteNodeUpsertMerged's INSERT ...
    // SELECT ... JOIN ... ON CONFLICT machinery to resolve
    // InnerCustomer/OuterCustomer's surrogate ids at upsert time.
    var cteResolutions = metadata.CteUpdateMeta;

    if (metadata.IsRoot || cteResolutions.Length > 0)
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


        return (cte, model);
    }


    return (default, model);
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
    