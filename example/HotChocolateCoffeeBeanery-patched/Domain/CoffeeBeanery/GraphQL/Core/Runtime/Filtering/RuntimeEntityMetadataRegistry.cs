using System.Collections.Generic;
using System.Collections.Immutable;
using CoffeeBeanery.GraphQL.Core.Foundation;

namespace CoffeeBeanery.GraphQL.Core.Runtime.Filtering;

/// <summary>
/// Builds the RuntimeEntityMetadata graph FilterMetadataResolver (and now
/// OrderCompiler) needs, from data GeneratedMetadata.{Model} (IdEmitter's
/// output) already carries -- no separate runtime navigation-resolution
/// logic needed; this just walks ModelMetadata.Navigations (itself
/// resolved once at generation time by EntityNavigationConvention, the
/// same resolution PlannerEmitter's join emission uses).
///
/// Fields whose FieldMetadata.Column is null (computed/derived fields
/// with no direct column, e.g. enum-mapped fields) are skipped -- there
/// is no column to filter/order on.
/// </summary>
public static class RuntimeEntityMetadataRegistry
{
    /// <summary>
    /// Root entity only, no navigation traversal -- kept for callers that
    /// only ever need root-level fields (cheaper than walking the full
    /// graph). Equivalent to GetGraph(modelEntityId) with maxDepth: 0.
    /// </summary>
    public static RuntimeEntityMetadata GetRootOnly(ushort modelEntityId) =>
        BuildEntity(modelEntityId);

    /// <summary>
    /// Root entity plus every entity transitively reachable through
    /// ModelMetadata.Navigations, up to maxDepth hops -- needed for
    /// navigation filters/ordering (`customer: { firstName: { eq: ... } }`,
    /// `order: { customer: { firstNaming: ASC } }`). Cycle-safe: an entity
    /// already built is never rebuilt, so a navigation cycle just stops
    /// expanding rather than looping forever.
    /// </summary>
    public static ImmutableArray<RuntimeEntityMetadata> GetGraph(
        ushort modelEntityId,
        int maxDepth = 4)
    {
        var built =
            new Dictionary<ushort, RuntimeEntityMetadata>();

        Expand(modelEntityId, maxDepth, built);

        return built.Values.ToImmutableArray();
    }

    private static void Expand(
        ushort modelEntityId,
        int depthRemaining,
        Dictionary<ushort, RuntimeEntityMetadata> built)
    {
        if (built.ContainsKey(modelEntityId))
            return;

        var entity =
            BuildEntity(modelEntityId);

        built[modelEntityId] = entity;

        if (depthRemaining <= 0)
            return;

        foreach (var navigation in entity.Navigations)
        {
            Expand(
                navigation.TargetEntityId,
                depthRemaining - 1,
                built);
        }
    }

    private static RuntimeEntityMetadata BuildEntity(ushort modelEntityId)
    {
        var model =
            GeneratedMetadata.GetModel(modelEntityId);

        var fields =
            new Dictionary<ushort, RuntimeFieldMetadata>();

        foreach (var field in model.Fields)
        {
            if (field.Column is null)
                continue;

            fields[field.Id.Value] =
                new RuntimeFieldMetadata(
                    field.Id.Value,
                    field.Name,
                    field.Column.ColumnId,
                    field.Column.Entity.EntityId.Value);
        }

        var navigations =
            new List<RuntimeNavigationMetadata>();

        if (model.Navigations != null)
        {
            foreach (var nav in model.Navigations)
            {
                navigations.Add(
                    new RuntimeNavigationMetadata(
                        nav.Name,
                        nav.TargetModel.Value,
                        joinInformation: null));
            }
        }

        return new RuntimeEntityMetadata(
            modelEntityId,
            model.Name,
            fields,
            navigations);
    }
}

