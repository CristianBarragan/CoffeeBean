using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Graphgine.SourceGenerators.Model;

namespace Graphgine.SourceGenerators.Passes
{
    /// <summary>
    /// Direct symbol-walking port of NodeBuilder.InferModelChildren: any
    /// object-typed (non-scalar) property on the Model becomes a ModelKey
    /// child automatically, unless already manually declared. Collection
    /// properties are unwrapped to their element type T.
    /// </summary>
    internal static class ModelChildrenInference
    {
        private static readonly HashSet<string> ScalarTypeNames = new()
        {
            "String", "Guid", "DateTime", "DateTimeOffset", "Decimal",
            "Boolean", "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32",
            "Int64", "UInt64", "Single", "Double", "Char"
        };

        public static void Apply(MappingClassInfo info,
            ImmutableArray<MappingClassInfo> allMappings)
        {
            var existing = new HashSet<string>(
                info.ModelChildren.Select(c => c.To),
                System.StringComparer.OrdinalIgnoreCase);

            var properties = info.ModelType.GetMembers().OfType<IPropertySymbol>()
                .Where(p => p.GetMethod is not null && !p.IsStatic);

            foreach (var prop in properties)
            {
                var elementType = UnwrapCollection(prop.Type);
                var unwrapped = UnwrapNullable(elementType);

                if (IsScalar(unwrapped))
                    continue;

                if (!existing.Contains(unwrapped.Name))
                {
                    info.ModelChildren.Add(new ModelChildInfo
                    {
                        From = info.ModelType.Name,
                        To = unwrapped.Name,
                        NavigationName = char.ToLowerInvariant(prop.Name[0]) + prop.Name.Substring(1)
                    });
                    existing.Add(unwrapped.Name);
                }
            }
            
            foreach (var candidate in allMappings)
            {
                if (candidate.EntityType == null)
                    continue;

                var entityName = candidate.EntityType.Name;

                if (!entityName.EndsWith("Edge", StringComparison.Ordinal))
                    continue;


                var navs = candidate.EntityType
                    .GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(p =>
                        p.Type is INamedTypeSymbol n &&
                        SymbolEqualityComparer.Default.Equals(
                            n,
                            info.EntityType));


                if (navs.Count() < 2)
                    continue;


                var navigationName =
                    char.ToLowerInvariant(candidate.ModelType.Name[0])
                    + candidate.ModelType.Name.Substring(1);

                if (info.ModelChildren.Any(x =>
                        string.Equals(
                            x.NavigationName,
                            navigationName,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                info.ModelChildren.Add(new ModelChildInfo
                {
                    From = info.ModelType.Name,
                    To = candidate.ModelType.Name,
                    NavigationName = navigationName
                });
            }
        }
        
        private static ITypeSymbol UnwrapCollection(ITypeSymbol type)
        {
            if (type.SpecialType == SpecialType.System_String)
                return type;

            if (type is INamedTypeSymbol { IsGenericType: true } named &&
                named.TypeArguments.Length == 1 &&
                named.Name is "List" or "IEnumerable" or "ICollection" or "IList")
            {
                return named.TypeArguments[0];
            }

            return type;
        }

        private static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
            type is INamedTypeSymbol { Name: "Nullable", TypeArguments.Length: 1 } nullable
                ? nullable.TypeArguments[0]
                : type;

        private static bool IsScalar(ITypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Enum) return true;
            if (type.IsValueType && type.SpecialType != SpecialType.None) return true;
            return ScalarTypeNames.Contains(type.Name);
        }
    }
}
