using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Emit;
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
    System.Collections.Immutable.ImmutableArray<MappingClassInfo> allMappings,
    List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph)
{
    if (info.Graph != null)
        return;

    // ... existing modelIndex / navigations / AutoChildAttachments block unchanged ...

    if (info.IsComposite)
    {
        BuildCompositeStorageJoinInfo(info, entityGraph);
    }
}

private static void BuildCompositeStorageJoinInfo(
    MappingClassInfo info,
    List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph)
{
    var anchor = info.Definition.Entities.FirstOrDefault(e => e.IsPrimary);
    if (anchor == null)
        return;

    var compositeTypes = info.Definition.Entities
        .Select(e => e.EntityType)
        .ToList();

    var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { anchor.EntityType };
    var queue = new Queue<INamedTypeSymbol>();
    queue.Enqueue(anchor.EntityType);

    while (queue.Count > 0)
    {
        var current = queue.Dequeue();

        foreach (var edge in entityGraph)
        {
            INamedTypeSymbol? other = null;
            string? parentColumn = null;
            string? childColumn = null;

            if (SymbolEqualityComparer.Default.Equals(edge.PrincipalEntity, current) &&
                compositeTypes.Any(t => SymbolEqualityComparer.Default.Equals(t, edge.DependentEntity)))
            {
                other = edge.DependentEntity;
                parentColumn = edge.PrincipalColumn;
                childColumn = edge.DependentColumn;
            }
            else if (SymbolEqualityComparer.Default.Equals(edge.DependentEntity, current) &&
                     compositeTypes.Any(t => SymbolEqualityComparer.Default.Equals(t, edge.PrincipalEntity)))
            {
                other = edge.PrincipalEntity;
                parentColumn = edge.DependentColumn;
                childColumn = edge.PrincipalColumn;
            }

            if (other == null || visited.Contains(other))
                continue;

            visited.Add(other);
            queue.Enqueue(other);

            info.CompositeStorageJoinInfo.Add(new CompositeStorageJoinInfo
            {
                ParentEntityType = current,
                ChildEntityType = other,
                ParentJoinColumn = parentColumn!,
                ChildJoinColumn = childColumn!
            });
        }
    }
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
        
        private static void BuildCompositeStorageJoinInfo(
            MappingClassInfo info,
            ImmutableArray<MappingClassInfo> allMappings)
        {
            var anchor = info.Definition.Entities.FirstOrDefault(e => e.IsPrimary);
            if (anchor == null)
                return;

            foreach (var entity in info.Definition.Entities)
            {
                if (SymbolEqualityComparer.Default.Equals(entity.EntityType, anchor.EntityType))
                    continue;

                if (string.IsNullOrWhiteSpace(entity.FromColumn) || string.IsNullOrWhiteSpace(entity.ToColumn))
                    continue; // no declared join — nothing to emit for this entity

                info.CompositeStorageJoinInfo.Add(new CompositeStorageJoinInfo
                {
                    ParentEntityType = anchor.EntityType,
                    ChildEntityType = entity.EntityType,
                    ParentJoinColumn = entity.FromColumn!,
                    ChildJoinColumn = entity.ToColumn!
                });
            }
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