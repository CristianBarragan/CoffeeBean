namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Emit;

using System.Collections.Immutable;
using System.Linq;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

internal static class CodegenModelSet
{
    /// <summary>
    /// The single source of truth for which mappings participate in
    /// codegen, and in what order. EntityId assignment (IdEmitter) and
    /// every other emitter that needs to align with it (AdapterEmitter,
    /// PlannerEmitter, MaterializerEmitter) must all call this instead
    /// of independently filtering ImmutableArray&lt;MappingClassInfo&gt;.
    /// </summary>
    public static System.Collections.Generic.List<MappingClassInfo> Resolve(
        ImmutableArray<MappingClassInfo> allMappings)
    {
        return allMappings
            .Where(x => x.ModelType != null)
            .OrderBy(x => x.ModelType!.Name, System.StringComparer.Ordinal)
            .ToList();
    }
}