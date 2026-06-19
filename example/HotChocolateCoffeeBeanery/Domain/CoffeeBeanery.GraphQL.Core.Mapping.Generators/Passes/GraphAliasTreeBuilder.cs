using System.Collections.Generic;
using System.Linq;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes
{
    internal static class GraphAliasTreeBuilder
    {
        public static void Apply(MappingClassInfo info, NavigationResolutionResult nav)
        {
            var counters = new Dictionary<string, int>();

            string Next(string parent, string name)
            {
                var key = $"{parent}:{name}";

                if (!counters.TryGetValue(key, out var i))
                    i = 0;

                counters[key] = i + 1;

                return string.IsNullOrEmpty(parent)
                    ? $"{name}_{i}"
                    : $"{parent}_{i}";
            }

            // ROOT alias
            info.Alias = info.ClassName;

            // FieldMaps
            foreach (var fm in info.FieldMaps)
            {
                fm.SourceAlias = info.Alias;
                fm.DestinationAlias = Next(info.Alias, fm.DestinationEntity);
            }

            // ModelToEntity
            foreach (var key in info.ModelToEntity)
            {
                key.AliasProperty = Next(info.Alias, key.EntityType.Name);
            }
        }
    }
}