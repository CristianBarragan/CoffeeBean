using System;
using System.Linq;
using Graphgine.SourceGenerators.Model;

namespace Graphgine.SourceGenerators.Passes;

internal static class EntityLinkFieldMapGeneration
{
    public static void Apply(MappingClassInfo info)
    {
        foreach (var link in info.Definition.Entities)
        {
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