using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes;
using Microsoft.CodeAnalysis;

internal static class EntityNavigationConvention
{
    public static NavigationResolutionResult Resolve(
    MappingClassInfo info,
    ImmutableArray<MappingClassInfo> allMappings,
    List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph,
    ISet<INamedTypeSymbol> rootEntityTypes)
{
    var result = new NavigationResolutionResult();

    // CHANGED: don't collapse the parent to just its primary entity.
    // A composite parent can have its FK-bearing side on any of its
    // backing entities, not necessarily the one marked IsPrimary.
    var parentEntities = info.Definition.Entities
        .Where(e => e.EntityType != null)
        .Select(e => e.EntityType!)
        .Distinct(SymbolEqualityComparer.Default)
        .Cast<INamedTypeSymbol>()
        .ToList();

    if (parentEntities.Count == 0 && info.EntityType != null)
        parentEntities.Add(info.EntityType);

    if (parentEntities.Count == 0)
        return result;

    var modelProperties = info.ModelType.GetMembers().OfType<IPropertySymbol>().ToList();

    foreach (var prop in modelProperties)
    {
        if (prop.IsStatic) continue;

        var relatedModelType = ResolveElementType(prop.Type);
        if (relatedModelType == null) continue;

        var childModel = allMappings.FirstOrDefault(m =>
            m.IsModel && SymbolEqualityComparer.Default.Equals(m.ModelType, relatedModelType));
        if (childModel == null) continue;

        if (!string.Equals(prop.Name, relatedModelType.Name, StringComparison.Ordinal))
            continue;

        var isCollection = IsCollection(prop.Type);
        var childEntities = childModel.Definition.Entities
            .Where(e => e.EntityType != null)
            .Select(e => e.EntityType!)
            .Distinct(SymbolEqualityComparer.Default)
            .Cast<INamedTypeSymbol>()
            .ToList();

        if (childEntities.Count == 0)
            continue;

        var joinPaths = new List<NavigationJoinPath>();

        // CHANGED: try every (parentEntity, childEntity) pair, not just
        // (primaryParentEntity, childEntity). Accept the first path found
        // per target entity — a composite child may legitimately connect
        // via more than one of its backing entities in different queries,
        // so we don't stop at the first successful target, only at the
        // first successful *source* for a given target.
        foreach (var targetEntity in childEntities)
        {
            List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge>? path = null;

            foreach (var sourceEntity in parentEntities)
            {
                path = FluentEntityNavigationConvention.EntityGraphPathfinder.FindPath(
                    entityGraph, sourceEntity, targetEntity);
                if (path != null) break;
            }

            if (path == null) continue;

            joinPaths.Add(new NavigationJoinPath
            {
                TargetEntity = targetEntity,
                Hops = path
            });
        }

        if (joinPaths.Count == 0)
            continue;

        result.Navigations.Add(new NavigationInfo
        {
            NavigationName = prop.Name,
            TargetModel = relatedModelType,
            RelatedEntityType = childModel.EntityType,
            IsCollection = isCollection,
            TargetIsRoot = childEntities.Any(rootEntityTypes.Contains),
            JoinPaths = joinPaths
        });
    }

    // ... AliasProperty fallback loop unchanged (still uses parentPrimaryEntity there,
    // should probably get the same treatment — see note below) ...

    return result;
}

    private static INamedTypeSymbol? ResolveElementType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1 &&
            named.Name is "List" or "ICollection" or "IEnumerable" or "IList")
        {
            return named.TypeArguments[0] as INamedTypeSymbol;
        }
        return type as INamedTypeSymbol;
    }

    private static bool IsCollection(ITypeSymbol type) =>
        type is INamedTypeSymbol named && named.IsGenericType &&
        named.Name is "List" or "ICollection" or "IEnumerable" or "IList";
}