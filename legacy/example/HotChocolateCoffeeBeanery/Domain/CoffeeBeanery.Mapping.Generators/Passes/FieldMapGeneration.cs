using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes
{
    internal static class FieldMapGeneration
    {
        private static readonly HashSet<string> ScalarTypeNames = new()
        {
            "String", "Guid", "DateTime", "DateTimeOffset", "Decimal",
            "Boolean", "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32",
            "Int64", "UInt64", "Single", "Double", "Char"
        };

        public static void Apply(MappingClassInfo info, SourceProductionContext spc)
            => ApplyCore(info, spc, reportDiagnostics: true);

        /// <summary>
        /// Runs the same field-map generation logic without reporting diagnostics
        /// to a SourceProductionContext. Used by the global emitter pipeline, which
        /// must not mutate shared MappingClassInfo state and instead recomputes
        /// field maps independently. This overload is therefore not called on the
        /// shared info objects — it is provided for callers that create a local
        /// MappingClassInfo copy.
        /// </summary>
        public static void ApplyWithoutDiagnostics(MappingClassInfo info)
            => ApplyCore(info, default, reportDiagnostics: false);

        private static void ApplyCore(
            MappingClassInfo info,
            SourceProductionContext spc,
            bool reportDiagnostics)
        {
            if (info.Definition.Entities.Count == 0)
                return;

            var modelProperties = info.ModelType.GetMembers().OfType<IPropertySymbol>()
                .Where(p => p.GetMethod is not null && !p.IsStatic)
                .ToList();

            var candidateEntities = info.Definition.Entities
                .Select(k => k.EntityType)
                .Distinct(SymbolEqualityComparer.Default)
                .Cast<INamedTypeSymbol>()
                .ToList();

            foreach (var modelProp in modelProperties)
{
    var unwrapped = UnwrapCollection(modelProp.Type);
    if (!IsScalar(UnwrapNullable(unwrapped)))
        continue;

    var matchedAny = false;

    // If an explicit (or already-generated) map exists for this source
    // property against ANY entity, that fully resolves it — convention
    // matching must not also pick up same-named columns on other entities.
    if (info.FieldMaps.Any(f =>
            string.Equals(f.SourceName, modelProp.Name, System.StringComparison.OrdinalIgnoreCase)))
    {
        continue;
    }

    foreach (var entityType in candidateEntities)
    {
        if (IsExcluded(info, modelProp.Name, entityType.Name))
            continue;

        var entityProp = entityType.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.SetMethod is not null)
            .FirstOrDefault(p =>
                string.Equals(p.Name, modelProp.Name, System.StringComparison.OrdinalIgnoreCase));

        if (entityProp is null)
            continue;

        if (!AreTypesCompatible(modelProp.Type, entityProp.Type))
        {
            if (reportDiagnostics)
                spc.ReportDiagnostic(Diagnostic.Create(
                    MappingDiagnostics.TypeIncompatible,
                    modelProp.Locations.FirstOrDefault() ?? Location.None,
                    info.ModelType.Name, modelProp.Name, modelProp.Type.Name,
                    entityType.Name, entityProp.Name, entityProp.Type.Name));
            continue;
        }

        matchedAny = true;

        info.FieldMaps.Add(new FieldInfo
        {
            SourceName = modelProp.Name,
            DestinationEntity = entityType.Name,
            DestinationName = entityProp.Name,
            IsGenerated = true,
            PropertyType = modelProp.Type
        });

        // Only take the first matching entity for this property —
        // stop convention-matching once resolved.
        break;
    }

    if (!matchedAny && reportDiagnostics)
    {
        spc.ReportDiagnostic(Diagnostic.Create(
            MappingDiagnostics.NoMatchingProperty,
            modelProp.Locations.FirstOrDefault() ?? Location.None,
            info.ModelType.Name, modelProp.Name,
            string.Join(", ", candidateEntities.Select(e => e.Name))));
    }
}
        }

        private static bool IsExcluded(MappingClassInfo info, string sourceName, string destEntity) =>
            info.ExcludedFieldMappings.Any(x =>
                string.Equals(x.SourceName, sourceName, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.DestinationEntity, destEntity, System.StringComparison.OrdinalIgnoreCase));

        private static bool HasAnyFieldMap(MappingClassInfo info, string sourceName, string destEntity) =>
            info.FieldMaps.Any(f =>
                string.Equals(f.SourceName, sourceName, System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(f.DestinationEntity, destEntity, System.StringComparison.OrdinalIgnoreCase));

        public static bool AreTypesCompatible(ITypeSymbol modelType, ITypeSymbol entityType)
        {
            var a = UnwrapNullable(modelType);
            var b = UnwrapNullable(entityType);

            if (SymbolEqualityComparer.Default.Equals(a, b)) return true;
            if (a.Name == "Guid" && b.SpecialType == SpecialType.System_String) return true;
            if (a.SpecialType == SpecialType.System_String && b.Name == "Guid") return true;
            if (a.TypeKind == TypeKind.Enum && IsNumeric(b)) return true;
            if (b.TypeKind == TypeKind.Enum && IsNumeric(a)) return true;
            if (a.TypeKind == TypeKind.Enum && b.TypeKind == TypeKind.Enum) return true;
            if (IsNumeric(a) && IsNumeric(b)) return true;

            return false;
        }

        internal static ITypeSymbol UnwrapCollection(ITypeSymbol type)
        {
            if (type.SpecialType == SpecialType.System_String)
                return type;

            if (type is INamedTypeSymbol { IsGenericType: true } named &&
                named.TypeArguments.Length == 1 &&
                named.Name is "List" or "IEnumerable" or "ICollection" or "IList")
                return named.TypeArguments[0];

            return type;
        }

        internal static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
            type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named
                ? named.TypeArguments[0]
                : type;

        internal static bool IsScalar(ITypeSymbol type)
        {
            var u = UnwrapNullable(type);
            if (u.TypeKind == TypeKind.Enum) return true;
            if (u.IsValueType && u.SpecialType != SpecialType.None) return true;
            return ScalarTypeNames.Contains(u.Name);
        }

        private static bool IsNumeric(ITypeSymbol t) => t.Name is
            "Byte" or "SByte" or "Int16" or "UInt16" or "Int32" or "UInt32" or
            "Int64" or "UInt64" or "Single" or "Double" or "Decimal";
    }
}