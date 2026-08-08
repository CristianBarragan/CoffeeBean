using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes
{
    internal static class AliasResolution
    {
        public static void Apply(MappingClassInfo info)
        {
            foreach (var fm in info.FieldMaps)
            {
                if (string.IsNullOrWhiteSpace(fm.SourceAlias))
                    fm.SourceAlias = info.Alias;

                if (string.IsNullOrWhiteSpace(fm.DestinationAlias))
                    fm.DestinationAlias = fm.DestinationEntity;
            }
        }
    }
}