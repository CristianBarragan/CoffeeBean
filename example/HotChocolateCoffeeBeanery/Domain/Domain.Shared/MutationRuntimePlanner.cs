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
        // CTE join resolver can resolve the navigation's natural key
        // column -> its related entity's real surrogate/primary key.
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
                
                if (!EntityMeta.TryGetEntityId(
                        spec.RelatedEntityTypeName,
                        out var relatedEntityId))
                {
                    continue;
                }

                childNodes.Add(
                    new MutationCteNode(
                        relatedEntityId,
                        spec.RelatedStorageEntityId,
                        spec.NavigationAlias,
                        ImmutableArray.Create(
                            new FieldValue(
                                relatedEntityId,
                                // FieldId 0 is a placeholder here, same
                                // convention MutationMetadataEmitter/
                                // MutationMaterializerEmitter already use
                                // for navigation-key fields with no real
                                // FieldId of their own (see EmitFactory's
                                // "columnExpression = 0" branch and
                                // MutationMaterializerEmitter's dematerializer
                                // comment). The value that actually matters
                                // for CTE resolution is the ColumnId below,
                                // not this FieldId.
                                0,
                                spec.RelatedNaturalKeyColumnId,
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


        EmitMutationDependencies(
            entityId,
            node,
            metadata,
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

    private static void EmitMutationDependencies(
        ushort entityId,
        in MutationIR node,
        MutationEntityMetadata metadata,
        ref MutationPlanBuilder builder)
    {
        if (metadata.CteUpdateMeta.Length == 0)
            return;


        foreach (var spec in metadata.CteUpdateMeta)
        {
            var navigationField =
                node.Values.FirstOrDefault(v =>
                    metadata.TryResolveField(
                        v.FieldId,
                        out var field) &&
                    string.Equals(
                        field.FieldName,
                        spec.NavigationAlias + "Key",
                        StringComparison.OrdinalIgnoreCase));


            if (string.IsNullOrWhiteSpace(
                    navigationField.RawValue))
            {
                continue;
            }


            if (!metadata.TryResolveField(
                    navigationField.FieldId,
                    out var targetField))
            {
                continue;
            }


            if (!builder.TryGetRow(
                    spec.NavigationAlias,
                    out var sourceRow))
            {
                continue;
            }


            if (!builder.TryGetRow(
                    node.OutputAlias,
                    out var targetRow))
            {
                continue;
            }


            // ---------------------------------------------------------
            // FIXED: this used to hardcode the literal string "Id" as the
            // source row's join/surrogate column for EVERY navigation
            // dependency — the same class of bug as the "?? Id" fallback
            // already removed from FluentEntityNavigationConvention, and
            // wrong for exactly the same reason: this codebase's dominant
            // key convention is "{Entity}Key", not "Id" (see CustomerKey,
            // ContractKey, AccountKey throughout). spec.RelatedSurrogateIdColumn
            // already holds the correctly-resolved surrogate/primary key
            // column name for the source row's related entity (computed by
            // GetPkPropertyName/ResolveColumnId upstream in
            // MetadataEmitter.PopulateCteUpdateMeta or
            // MutationMetadataEmitter.BuildGraphEdgeCteResolutions) — use
            // that instead of assuming "Id" everywhere.
            // ---------------------------------------------------------
            builder.AddDependency(
                sourceRow,
                targetRow,
                spec.RelatedSurrogateIdColumn,
                targetField.FieldName);
        }
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

    var groupedValues =
        new Dictionary<ushort, ImmutableArray<FieldValue>.Builder>();

    var groupedLookups =
        new Dictionary<ushort, ImmutableArray<LookupValue>.Builder>();

    var groupedAliases =
        new Dictionary<ushort, string>();

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
            if (!groupedValues.TryGetValue(
                    target.StorageEntityId,
                    out var valueGroup))
            {
                valueGroup =
                    ImmutableArray.CreateBuilder<FieldValue>();

                groupedValues[target.StorageEntityId] =
                    valueGroup;
            }

            if (!groupedLookups.TryGetValue(
                    target.StorageEntityId,
                    out var lookupGroup))
            {
                lookupGroup =
                    ImmutableArray.CreateBuilder<LookupValue>();

                groupedLookups[target.StorageEntityId] =
                    lookupGroup;
            }

            var spec =
                metadata.CteUpdateMeta.FirstOrDefault(x =>
                    string.Equals(
                        x.NavigationAlias + "Key",
                        target.FieldName,
                        StringComparison.OrdinalIgnoreCase));

            if (spec != null)
            {
                groupedAliases[target.StorageEntityId] =
                    spec.NavigationAlias;

                lookupGroup.Add(
                    new LookupValue(
                        target.ColumnId,
                        spec.RelatedStorageEntityId,
                        spec.RelatedNaturalKeyColumnId,
                        spec.RelatedSurrogateIdColumnId,
                        value.RawValue,
                        spec.NavigationAlias +
                        spec.RelatedEntityTypeName));

                continue;
            }

            if (target.IsNavigationKey)
            {
                continue;
            }

            valueGroup.Add(value);
        }
    }

    foreach (var pair in groupedValues)
    {
        groupedLookups.TryGetValue(
            pair.Key,
            out var lookups);

        var alias =
            groupedAliases.TryGetValue(
                pair.Key,
                out var navigationAlias)
                ? navigationAlias
                : node.OutputAlias;

        builder.AddRow(
            entityId,
            pair.Key,
            alias,
            pair.Value.ToImmutable(),
            null,
            null,
            lookups?.ToImmutable()
                ?? ImmutableArray<LookupValue>.Empty);
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