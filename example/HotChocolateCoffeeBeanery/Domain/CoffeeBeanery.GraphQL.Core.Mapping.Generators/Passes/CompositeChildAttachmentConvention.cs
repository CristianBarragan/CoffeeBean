using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes
{
    /// <summary>
    /// For every object-typed or List&lt;object&gt;-typed property on a Model
    /// (mirroring ModelChildrenInference's own unwrap rules), find the other
    /// mapping class in the compilation whose ModelType matches the property's
    /// element type, and register a ChildAttachment so it can be nested as a
    /// GraphQL field at runtime - even when the related model is composite
    /// (spans multiple unrelated EF entities, e.g. Product) and therefore has
    /// no single EF navigation property for EntityNavigationConvention to find.
    ///
    /// Skips:
    ///   - scalar/enum properties (same rules as ModelChildrenInference)
    ///   - self-references
    ///   - the Wrapper model (root payload container - not a real nesting target)
    ///   - fields already claimed by a hand-written ChildAttachment in BuildMap()
    ///
    /// Join column resolution: convention is "{ParentModel.Name}Key" must
    /// exist on the parent's own primary entity, and some entity in the
    /// related model's ModelToEntity composition must carry that same column
    /// name (e.g. Product's CustomerBankingRelationship link, which has its
    /// own "CustomerKey" column). If neither side matches, the attachment is
    /// still registered as unresolved (ParentJoinColumn left blank);
    /// NodeBuilder.BuildEdges skips unresolved attachments, so they're a
    /// silent no-op rather than a build error - the convention simply
    /// doesn't apply and a hand-written ChildAttachment is required instead.
    /// </summary>
    internal static class CompositeChildAttachmentConvention
    {
        private static readonly HashSet<string> ScalarTypeNames = new()
        {
            "String", "Guid", "DateTime", "DateTimeOffset", "Decimal",
            "Boolean", "Byte", "SByte", "Int16", "UInt16", "Int32", "UInt32",
            "Int64", "UInt64", "Single", "Double", "Char"
        };

        public static void Apply(
            MappingClassInfo info,
            System.Collections.Immutable.ImmutableArray<MappingClassInfo> allMappings)
        {
            var modelIndex = allMappings
                .Where(x => x.ModelType != null)
                .ToDictionary(
                    x => x.ModelType,
                    SymbolEqualityComparer.Default);

            var existingFieldNames = new HashSet<string>(
                info.AutoChildAttachments.Select(a => a.FieldName)
                    .Concat(info.ModelChildren.Select(c => ToGraphQlFieldNameLiteral(c.To))),
                StringComparer.OrdinalIgnoreCase);

            var navigations = DiscoverNavigations(info.ModelType);
            
            foreach (var nav in navigations)
            {
                if (!modelIndex.TryGetValue(nav.TargetType, out var childMapping))
                    continue;

                var fieldName = ToGraphQlFieldNameLiteral(nav.PropertyName);

                if (existingFieldNames.Contains(fieldName))
                    continue;

                info.ModelChildren.Add(new ModelChildInfo
                {
                    From = info.ModelType.Name,
                    To = childMapping.ModelType.Name,
                    NavigationName = fieldName
                });

                existingFieldNames.Add(fieldName);

                var parentJoinColumn = $"{info.ModelType.Name}Key";

                var childEntity =
                    childMapping.Definition.Entities.FirstOrDefault(e =>
                        e.EntityType
                            .GetMembers()
                            .OfType<IPropertySymbol>()
                            .Any(p =>
                                string.Equals(
                                    p.Name,
                                    parentJoinColumn,
                                    StringComparison.OrdinalIgnoreCase)));

                if (childEntity == null)
                    continue;

                info.AutoChildAttachments.Add(
                    new AutoChildAttachmentInfo
                    {
                        FieldName = fieldName,
                        ToModelName = childMapping.ModelType.Name,

                        ParentEntityType =
                            info.Definition.Entities
                                .FirstOrDefault(e => e.IsPrimary)
                                ?.EntityType
                            ?? throw new InvalidOperationException(
                                $"Mapping {info.ModelType.Name} has no primary entity"),

                        ParentJoinColumn = parentJoinColumn,

                        ChildEntityType = childEntity.EntityType,

                        ChildJoinColumn = parentJoinColumn
                    });
            }



            //
            // Reverse discovery:
            //
            // CustomerCustomerEdge
            //      |
            //      +-- InnerCustomer : Customer
            //      +-- OuterCustomer : Customer
            //
            // becomes:
            //
            // Customer
            //      |
            //      +-- CustomerCustomerEdge
            //
            //
            // foreach (var candidate in allMappings)
            // {
            //     if (candidate.ModelType == info.ModelType)
            //         continue;
            //
            //     if (candidate.EntityType == null)
            //         continue;
            //
            //
            //     foreach (var prop in candidate.ModelType
            //                  .GetMembers()
            //                  .OfType<IPropertySymbol>()
            //                  .Where(p => p.GetMethod is not null &&
            //                              !p.IsStatic))
            //     {
            //         var elementType = UnwrapCollection(prop.Type);
            //         var unwrapped = UnwrapNullable(elementType);
            //
            //
            //         if (unwrapped is not INamedTypeSymbol related)
            //             continue;
            //
            //
            //         if (!SymbolEqualityComparer.Default.Equals(
            //                 related,
            //                 info.ModelType))
            //             continue;
            //
            //
            //         //
            //         // Example:
            //         // candidate = CustomerCustomerEdge
            //         // related  = Customer
            //         //
            //         var fieldName =
            //             ToGraphQlFieldNameLiteral(
            //                 candidate.ModelType.Name);
            //
            //
            //         if (existingFieldNames.Contains(fieldName))
            //             continue;
            //
            //
            //
            //         info.ModelChildren.Add(
            //             new ModelChildInfo
            //             {
            //                 To = candidate.ModelType.Name
            //             });
            //
            //
            //         existingFieldNames.Add(fieldName);
            //
            //
            //
            //         //
            //         // Try to find the entity carrying the FK.
            //         //
            //         // CustomerCustomerEdge has:
            //         //
            //         // InnerCustomerKey
            //         // OuterCustomerKey
            //         //
            //         var parentKey =
            //             $"{info.ModelType.Name}Key";
            //
            //
            //         foreach (var entityLink in candidate.Definition.Entities)
            //         {
            //             var fkProperty =
            //                 entityLink.EntityType
            //                     .GetMembers()
            //                     .OfType<IPropertySymbol>()
            //                     .FirstOrDefault(p =>
            //                         string.Equals(
            //                             p.Name,
            //                             parentKey,
            //                             StringComparison.OrdinalIgnoreCase));
            //
            //
            //             if (fkProperty == null)
            //                 continue;
            //
            //
            //
            //             info.AutoChildAttachments.Add(
            //                 new AutoChildAttachmentInfo
            //                 {
            //                     FieldName = fieldName,
            //                     ToModelName = candidate.ModelType.Name,
            //
            //                     ParentEntityType = info.EntityType,
            //                     ParentJoinColumn = parentKey,
            //
            //                     ChildEntityType = entityLink.EntityType,
            //                     ChildJoinColumn = fkProperty.Name
            //                 });
            //
            //
            //             break;
            //         }
            //     }
            // }
        }

        private sealed record NavigationInfo(
            string PropertyName,
            INamedTypeSymbol TargetType,
            bool IsCollection);

        private static IEnumerable<NavigationInfo> DiscoverNavigations(INamedTypeSymbol modelType)
        {
            foreach (var property in modelType.GetMembers()
                         .OfType<IPropertySymbol>()
                         .Where(p => p.GetMethod != null && !p.IsStatic))
            {
                var isCollection = false;

                ITypeSymbol type = property.Type;

                if (type.SpecialType == SpecialType.System_String)
                    continue;

                if (type is INamedTypeSymbol named &&
                    named.IsGenericType &&
                    named.TypeArguments.Length == 1 &&
                    named.Name is "List" or "ICollection" or "IEnumerable" or "IList")
                {
                    isCollection = true;
                    type = named.TypeArguments[0];
                }

                if (type is INamedTypeSymbol
                    {
                        Name: "Nullable",
                        TypeArguments.Length: 1
                    } nullable)
                {
                    type = nullable.TypeArguments[0];
                }

                if (type is not INamedTypeSymbol target)
                    continue;

                if (IsScalar(target))
                    continue;

                yield return new NavigationInfo(
                    property.Name,
                    target,
                    isCollection);
            }
        }

        private static ITypeSymbol UnwrapCollection(ITypeSymbol type)
        {
            if (type.SpecialType == SpecialType.System_String)
                return type;


            if (type is INamedTypeSymbol named &&
                named.IsGenericType &&
                named.TypeArguments.Length == 1 &&
                named.Name is "List" or "IEnumerable" or "ICollection" or "IList")
            {
                return named.TypeArguments[0];
            }


            return type;
        }


        private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named &&
                named.Name == "Nullable" &&
                named.TypeArguments.Length == 1)
            {
                return named.TypeArguments[0];
            }

            return type;
        }


        private static bool IsScalar(ITypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Enum)
                return true;

            return type.SpecialType switch
            {
                SpecialType.System_String => true,
                SpecialType.System_Boolean => true,
                SpecialType.System_Char => true,
                SpecialType.System_Int16 => true,
                SpecialType.System_Int32 => true,
                SpecialType.System_Int64 => true,
                SpecialType.System_Decimal => true,
                SpecialType.System_Double => true,
                SpecialType.System_Single => true,
                _ => false
            };
        }


        internal static string ToGraphQlFieldNameLiteral(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}