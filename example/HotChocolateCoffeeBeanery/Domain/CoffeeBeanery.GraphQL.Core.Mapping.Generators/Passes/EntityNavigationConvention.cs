using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes
{
    internal static class EntityNavigationConvention
    {
        public static NavigationResolutionResult Resolve(
            MappingClassInfo info,
            SourceProductionContext spc,
            ISet<INamedTypeSymbol> rootEntityTypes,
            ImmutableDictionary<(INamedTypeSymbol, string), string> fluentInverseNav)
        {
            var result = ResolveCore(info, rootEntityTypes, fluentInverseNav);
            foreach (var d in result.PendingDiagnostics)
                spc.ReportDiagnostic(d);
            return result;
        }

        /// <summary>
        /// Resolves navigations without reporting diagnostics. Used by the global
        /// emitter pipeline (PlannerEmitter) which needs nav data but must not
        /// double-report diagnostics already emitted by the per-class path.
        /// HasBlockingAmbiguity is still set; callers should skip mappings where
        /// it is true to avoid emitting planners with unresolved join columns.
        /// </summary>
        public static NavigationResolutionResult ResolveQuietly(
            MappingClassInfo info,
            ISet<INamedTypeSymbol> rootEntityTypes,
            ImmutableDictionary<(INamedTypeSymbol, string), string> fluentInverseNav)
            => ResolveCore(info, rootEntityTypes, fluentInverseNav);

        private static NavigationResolutionResult ResolveCore(
            MappingClassInfo info,
            ISet<INamedTypeSymbol> rootEntityTypes,
            ImmutableDictionary<(INamedTypeSymbol, string), string> fluentInverseNav)
        {
            var result = new NavigationResolutionResult();

            if (info.EntityType is null)
                return result;

            var explicitAttrs = GetEntityForeignKeyAttributes(info.EntityType);
            var properties = info.EntityType.GetMembers().OfType<IPropertySymbol>()
                .Where(p => p.GetMethod is not null && !p.IsStatic)
                .ToList();

            var navigationCandidates = new List<(IPropertySymbol Property, INamedTypeSymbol RelatedType, bool IsCollection)>();

            foreach (var prop in properties)
            {
                var (elementType, isCollection) = UnwrapCollection(prop.Type);
                if (elementType is not INamedTypeSymbol named)
                    continue;

                if (IsScalarLike(named))
                    continue;

                navigationCandidates.Add((prop, named, isCollection));
            }

            var groupedByRelatedType = navigationCandidates
                .GroupBy(c => c.RelatedType, SymbolEqualityComparer.Default)
                .ToList();

            foreach (var group in groupedByRelatedType)
            {
                var relatedType = (INamedTypeSymbol)group.Key!;
                var isAmbiguous = group.Count() > 1;
                var targetIsRoot = rootEntityTypes.Contains(relatedType);

                var attrsForType = explicitAttrs
                    .Where(a => SymbolEqualityComparer.Default.Equals(a.RelatedEntityType, relatedType))
                    .ToList();

                var aliasKeysForType = info.ModelToEntity
                    .Where(k => SymbolEqualityComparer.Default.Equals(k.EntityType, relatedType) &&
                                k.AliasProperty is not null)
                    .ToList();

                var relatedProperties = relatedType.GetMembers().OfType<IPropertySymbol>().ToList();

                foreach (var (property, related, isCollection) in group)
                {
                    // 1. Explicit attribute match.
                    var attrMatch = isAmbiguous
                        ? attrsForType.FirstOrDefault(a => a.NavigationName == property.Name)
                        : attrsForType.FirstOrDefault();

                    if (attrMatch is not null)
                    {
                        result.Navigations.Add(new NavigationInfo
                        {
                            NavigationName = property.Name,
                            RelatedEntityType = related,
                            ForeignKeyProperty = attrMatch.ForeignKeyProperty,
                            PrincipalKeyProperty = attrMatch.PrincipalKeyProperty,
                            IsCollection = isCollection,
                            TargetIsRoot = targetIsRoot
                        });
                        continue;
                    }

                    // 2a. Forward convention: "{NavigationName}Key" or "{RelatedType.Name}Key"
                    //     on the DECLARING side (this entity owns the FK).
                    var fkProp = FindScalarSibling(properties, property.Name + "Key")
                                 ?? FindScalarSibling(properties, related.Name + "Key");

                    string? principalKeyName = null;

                    if (fkProp is not null)
                    {
                        principalKeyName = related.GetMembers().OfType<IPropertySymbol>()
                            .FirstOrDefault(p => p.Name == related.Name + "Key")?.Name
                            ?? related.Name + "Key";
                    }
                    else
                    {
                        // 2b. Inverse convention: "{DeclaringEntity.Name}Key" on declaring side,
                        //     same name on related side (related entity owns the FK back to us).
                        var ownKeyProp = FindScalarSibling(properties, info.EntityType.Name + "Key");

                        if (ownKeyProp is not null)
                        {
                            var siblingOnRelated = FindScalarSibling(relatedProperties, ownKeyProp.Name);
                            if (siblingOnRelated is not null)
                            {
                                fkProp = ownKeyProp;
                                principalKeyName = siblingOnRelated.Name;
                            }
                        }

                        // 2c. Ambiguous + still unresolved: use fluent config's
                        //     WithOne/WithMany inverse nav name to pick the right
                        //     sibling on the related side.
                        if (fkProp is null && isAmbiguous &&
                            fluentInverseNav.TryGetValue((info.EntityType, property.Name), out var inverseNavName))
                        {
                            var ownKeyProp2 = FindScalarSibling(properties, info.EntityType.Name + "Key");
                            var siblingByInverseName = FindScalarSibling(relatedProperties, inverseNavName + "Key");

                            if (ownKeyProp2 is not null && siblingByInverseName is not null)
                            {
                                fkProp = ownKeyProp2;
                                principalKeyName = siblingByInverseName.Name;
                            }
                        }
                    }

                    if (fkProp is null)
                    {
                        result.PendingDiagnostics.Add(Diagnostic.Create(
                            MappingDiagnostics.UnresolvedForeignKey,
                            info.EntityType.Locations.FirstOrDefault() ?? Location.None,
                            info.EntityType.Name, property.Name, related.Name));
                        result.HasBlockingAmbiguity = true;
                        continue;
                    }

                    // 3. Ambiguity without disambiguation.
                    if (isAmbiguous)
                    {
                        var resolvedViaFluent = fluentInverseNav.ContainsKey((info.EntityType, property.Name));
                        var aliasMatch = aliasKeysForType.FirstOrDefault(k =>
                            string.Equals(k.AliasProperty, property.Name,
                                System.StringComparison.OrdinalIgnoreCase));

                        if (aliasMatch is null && !resolvedViaFluent)
                        {
                            result.PendingDiagnostics.Add(Diagnostic.Create(
                                MappingDiagnostics.AmbiguousNavigation,
                                info.EntityType.Locations.FirstOrDefault() ?? Location.None,
                                info.EntityType.Name,
                                related.Name,
                                string.Join(", ", group.Select(g => g.Property.Name)),
                                property.Name,
                                info.ModelType.Name));
                            result.HasBlockingAmbiguity = true;
                            continue;
                        }
                    }

                    result.Navigations.Add(new NavigationInfo
                    {
                        NavigationName = property.Name,
                        RelatedEntityType = related,
                        ForeignKeyProperty = fkProp.Name,
                        PrincipalKeyProperty = principalKeyName ?? related.Name + "Key",
                        IsCollection = isCollection,
                        TargetIsRoot = targetIsRoot
                    });
                }
            }

            return result;
        }

        private static IPropertySymbol? FindScalarSibling(List<IPropertySymbol> properties, string name) =>
            properties.FirstOrDefault(p =>
                string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase) &&
                IsScalarLike(p.Type is INamedTypeSymbol n ? n : null!));

        private static (ITypeSymbol ElementType, bool IsCollection) UnwrapCollection(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol { IsGenericType: true } named &&
                named.TypeArguments.Length == 1 &&
                named.Name is "List" or "ICollection" or "IList" or "IEnumerable")
                return (named.TypeArguments[0], true);

            return (type, false);
        }

        private static bool IsScalarLike(INamedTypeSymbol? type)
        {
            if (type is null) return false;

            var unwrapped = type is { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
                ? (INamedTypeSymbol)type.TypeArguments[0]
                : type;

            if (unwrapped.TypeKind == TypeKind.Enum) return true;
            if (unwrapped.SpecialType != SpecialType.None) return true;
            return unwrapped.Name is "String" or "Guid" or "DateTime" or "DateTimeOffset" or "Decimal";
        }

        private record ForeignKeyAttrInfo(
            INamedTypeSymbol RelatedEntityType,
            string ForeignKeyProperty,
            string PrincipalKeyProperty,
            string? NavigationName);

        private static List<ForeignKeyAttrInfo> GetEntityForeignKeyAttributes(INamedTypeSymbol entityType)
        {
            var list = new List<ForeignKeyAttrInfo>();

            foreach (var member in entityType.GetMembers().OfType<IPropertySymbol>())
            {
                foreach (var attr in member.GetAttributes())
                {
                    if (attr.AttributeClass?.Name != "EntityForeignKeyAttribute")
                        continue;

                    var args = attr.ConstructorArguments;
                    if (args.Length < 3) continue;
                    if (args[0].Value is not INamedTypeSymbol relatedType) continue;

                    var fk = args[1].Value as string;
                    var pk = args[2].Value as string;
                    if (fk is null || pk is null) continue;

                    // NavigationName comes from the property the attribute sits on,
                    // falling back to the explicit 4th arg if provided.
                    var navName = (args.Length > 3 ? args[3].Value as string : null) 
                                  ?? member.Name;

                    list.Add(new ForeignKeyAttrInfo(relatedType, fk, pk, navName));
                }
            }

            return list;
        }
    }
}