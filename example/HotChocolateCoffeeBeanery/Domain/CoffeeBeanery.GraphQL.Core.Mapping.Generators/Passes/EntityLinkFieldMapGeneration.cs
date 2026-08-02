using System;
using System.Linq;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes;

internal static class EntityLinkFieldMapGeneration
{
    public static void Apply(MappingClassInfo info)
    {
        foreach (var link in info.Definition.Entities)
        {
            // ---------------------------------------------------------------
            // FIXED: this used to run for EVERY entry in Definition.Entities,
            // including the model's own IsPrimary entry -- the single entity
            // synthesized by MappingClassParser from the `Entity`/`Key`
            // shorthand (e.g. Customer's Entity = typeof(Customer),
            // Key = nameof(Customer.CustomerKey)). That entry represents the
            // model's OWN backing row and its OWN natural/business key, not a
            // relationship to some OTHER entity -- there is nothing to
            // "navigate" to. Marking it IsNavigationKey = true made
            // MutationRuntimePlanner's filteredValues step (which strips
            // IsNavigationKey fields before building INSERT statements)
            // silently drop the model's own key from every upsert -- e.g.
            // Customer's CustomerKey vanishing from mut_0/mut_1's VALUES list
            // while still (correctly, via the separately-synthesized
            // PrimaryKey/UpsertKeys collections) appearing in ON CONFLICT.
            // It also blocked FieldMapGeneration's own correct name-matching
            // convention pass for that same source name, since its
            // early-return sees this spurious entry and skips.
            //
            // Only a genuine SECONDARY link -- another entity a composite
            // model reaches beyond its own primary entity (e.g. Product
            // reaching CustomerBankingRelationship/Contract/Transaction) --
            // is actually a navigation/FK-lookup key that should be excluded
            // from literal INSERT values and resolved via CTE join instead.
            // The primary entity's own entry should flow through as a normal
            // scalar column, picked up by FieldMapGeneration's convention
            // matching (hence "continue" here rather than adding a FieldMap
            // for it at all -- this pass's whole job is composite LINKS,
            // not primary-entity columns).
            // ---------------------------------------------------------------
            if (link.IsPrimary)
                continue;

            if (link.ModelKey == null || link.EntityKey == null)
                continue;

            if (HasAnyFieldMap(
                    info,
                    link.ModelKey,
                    link.EntityType.Name))
            {
                continue;
            }

            info.FieldMaps.Add(new FieldInfo
            {
                SourceName = link.ModelKey,
                DestinationEntity = link.EntityType.Name,
                DestinationName = link.EntityKey,

                // IMPORTANT:
                // This is a relationship FK, not a normal scalar.
                IsGenerated = true,
                IsNavigationKey = true,

                PropertyType = link.EntityType
            });
        }
    }


    private static bool HasAnyFieldMap(
        MappingClassInfo info,
        string sourceName,
        string destEntity)
    {
        return info.FieldMaps.Any(f =>
            string.Equals(
                f.SourceName,
                sourceName,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                f.DestinationEntity,
                destEntity,
                StringComparison.OrdinalIgnoreCase));
    }
}