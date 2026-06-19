using Microsoft.CodeAnalysis;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators
{
    public static class MappingDiagnostics
    {
        private const string Category = "CoffeeBeanery.MappingGenerator";

        public static readonly DiagnosticDescriptor TypeIncompatible = new(
            id: "CBMAP001",
            title: "Model/Entity property type mismatch",
            messageFormat: "'{0}.{1}' ({2}) is type-incompatible with '{3}.{4}' ({5}). " +
                           "Add a manual FieldMap (e.g. with FromEnum/ToEnum) to handle this conversion.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor NoMatchingProperty = new(
            id: "CBMAP002",
            title: "Model property has no matching entity property",
            messageFormat: "'{0}.{1}' has no matching property on any candidate entity ({2}) and no manual FieldMap was provided. It will be skipped.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor AmbiguousNavigation = new(
            id: "CBMAP003",
            title: "Ambiguous entity navigation requires disambiguation",
            messageFormat: "Entity '{0}' has multiple navigations of type '{1}' ({2}). Navigation '{3}' could not be disambiguated. " +
                           "Fix: add a map.AddModelToEntity<{4},{1},...>(...) entry with its alias lambda set to the " +
                           "Model property corresponding to '{3}', or annotate '{0}' with " +
                           "[EntityForeignKey(nameof({1}), foreignKeyProperty: \"...\", principalKeyProperty: \"...\", navigationName: \"{3}\")].",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnresolvedForeignKey = new(
            id: "CBMAP004",
            title: "Unable to resolve foreign key for navigation property",
            messageFormat: "Entity '{0}' has a navigation-shaped property '{1}' of type '{2}', but no '{1}Key' or '{2}Key' " +
                           "scalar property was found by convention. Annotate '{0}' with " +
                           "[EntityForeignKey(nameof({2}), foreignKeyProperty: \"...\", principalKeyProperty: \"...\")] to resolve explicitly.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidBuildMapShape = new(
            id: "CBMAP005",
            title: "Unsupported BuildMap() shape",
            messageFormat: "BuildMap() in '{0}' contains a statement the source generator cannot statically interpret: {1}. " +
                           "Only NodeMap initializer, AddModelToEntity<,>(...), map.FieldMaps.Add(new FieldMap{{...}}), " +
                           "and map.ExcludedFieldMappings.Add(...) statements are supported.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ClassNotPartial = new(
            id: "CBMAP006",
            title: "Mapping class must be declared partial to receive generated mappings",
            messageFormat: "Mapping class '{0}' has generated FieldMaps/ModelChildren available but isn't declared 'partial', " +
                           "so they can't be emitted. NodeBuilder's reflection fallback will still cover this class at " +
                           "runtime, but it won't be AOT-safe.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}