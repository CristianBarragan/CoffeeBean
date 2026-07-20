using System.Collections.Generic;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Emit;

using System.Collections.Immutable;
using System.Linq;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

internal static class CodegenModelSet
{
    public static List<MappingClassInfo> Resolve(ImmutableArray<MappingClassInfo> allMappings)
    {
        return allMappings
            .Where(x => x.ModelType != null)
            .GroupBy(x => x.ModelType!.Name, System.StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.ModelType!.Name, System.StringComparer.Ordinal)
            .ToList();
    }
}