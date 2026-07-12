using System;
using System.Collections.Generic;
using System.Linq;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Emit;
using Microsoft.CodeAnalysis;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes
{
    internal static class EntityNavigationConvention
    {
        public static NavigationResolutionResult Resolve(
            MappingClassInfo info,
            ISet<INamedTypeSymbol> rootEntityTypes,
            IReadOnlyDictionary<(INamedTypeSymbol Entity, string Navigation), string> inverseNavigation = null)
        {
            var result = new NavigationResolutionResult();


            if (info.EntityType == null)
                return result;



            var entity =
                info.EntityType;



            var properties =
                entity.GetMembers()
                    .OfType<IPropertySymbol>()
                    .ToList();



            foreach (var property in properties)
            {
                if (property.IsStatic)
                    continue;

                if (!IsNavigationCandidate(property))
                    continue;

                // Only surface navigations the GraphQL model actually exposes —
                // entity-only navs (e.g. Customer.OuterCustomerCustomerRelationship,
                // which exists solely to support fluent EF config for the edge model)
                // must not be promoted into NavigationInfo.
                var modelHasMatchingProperty = info.ModelType.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Any(p => string.Equals(p.Name, property.Name, StringComparison.Ordinal));
                if (!modelHasMatchingProperty)
                    continue;

                var related = ResolveRelatedEntity(property.Type);
                if (related == null)
                    continue;

                // Ownership follows the sibling-Id property, not collection-ness:
                // whichever side declares "{NavPropertyName}Id" alongside the nav
                // is the dependent/FK-owning side (matches HasForeignKey<T>(...)).
                var fkSiblingName = property.Name + "Id";
                var declaringOwnsFk = properties.Any(p =>
                    string.Equals(p.Name, fkSiblingName, StringComparison.Ordinal));

                var navigation = new NavigationInfo
                {
                    NavigationName = property.Name,
                    RelatedEntityType = related,
                    ForeignKeyProperty = declaringOwnsFk
                        ? fkSiblingName
                        : PlannerEmitter.GetPkPropertyName(entity),
                    PrincipalKeyProperty = related.Name + "Key",
                    IsCollection = IsCollection(property.Type),
                    TargetIsRoot = rootEntityTypes.Contains(related),
                    FkOwnedByDeclaringEntity = declaringOwnsFk 
                };

                result.Navigations.Add(navigation);
            }



            /*
             * Add explicit Model aliases:
             *
             * CustomerCustomerEdge:
             *
             * InnerCustomer
             * OuterCustomer
             *
             * These are not necessarily Entity navigations,
             * but they are valid GraphQL child links.
             */

            foreach (var link in info.Definition.Entities)
            {
                if (string.IsNullOrWhiteSpace(link.AliasProperty))
                    continue;



                if (result.Navigations.Any(x =>
                    x.NavigationName == link.AliasProperty))
                {
                    continue;
                }



                result.Navigations.Add(
                    new NavigationInfo
                    {
                        NavigationName =
                            link.AliasProperty,

                        RelatedEntityType =
                            link.EntityType,

                        ForeignKeyProperty =
                            link.AliasProperty + "Id",

                        PrincipalKeyProperty =
                            link.EntityType.Name + "Key",

                        IsCollection = false,

                        TargetIsRoot =
                            rootEntityTypes.Contains(
                                link.EntityType)
                    });
            }



            return result;
        }



        private static bool IsNavigationCandidate(
            IPropertySymbol property)
        {
            var type =
                property.Type;


            if (type.SpecialType != SpecialType.None)
                return false;


            if (type.TypeKind == TypeKind.Enum)
                return false;


            if (type is INamedTypeSymbol named)
            {
                if (named.Name == "String")
                    return false;


                return true;
            }


            return false;
        }



        private static INamedTypeSymbol? ResolveRelatedEntity(
            ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named &&
                named.IsGenericType &&
                named.TypeArguments.Length == 1)
            {
                if (named.Name is
                    "List" or
                    "ICollection" or
                    "IEnumerable" or
                    "IList")
                {
                    return named.TypeArguments[0]
                        as INamedTypeSymbol;
                }
            }


            return type as INamedTypeSymbol;
        }



        private static bool IsCollection(
            ITypeSymbol type)
        {
            return type is INamedTypeSymbol named &&
                   named.IsGenericType &&
                   named.Name is
                       "List" or
                       "ICollection" or
                       "IEnumerable" or
                       "IList";
        }
    }
}