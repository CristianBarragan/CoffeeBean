// using System.Collections.Generic;
//
// namespace Graphgine.SourceGenerators.Emit;
//
// using System.Collections.Immutable;
// using System.Linq;
// using Graphgine.SourceGenerators.Model;
//
// internal static class CodegenModelSet
// {
//     public static List<MappingClassInfo> Resolve(ImmutableArray<MappingClassInfo> allMappings)
//     {
//         return allMappings
//             .Where(x => x.ModelType != null)
//             .GroupBy(x => x.ModelType!.Name, System.StringComparer.OrdinalIgnoreCase)
//             .Select(g => g.First())
//             .OrderBy(x => x.ModelType!.Name, System.StringComparer.Ordinal)
//             .ToList();
//     }
// }