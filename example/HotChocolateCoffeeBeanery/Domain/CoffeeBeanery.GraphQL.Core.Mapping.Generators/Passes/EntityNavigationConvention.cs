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

        // Shared helper: try every parent entity as a source, return the first
        // path found. Used by both the model-property walk below and the
        // AliasProperty fallback loop, so a composite parent's FK can live on
        // any of its backing entities in either code path — not just whichever
        // one happened to be marked IsPrimary.
        List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge>? FindPathFromAnyParent(
            INamedTypeSymbol targetEntity)
        {
            foreach (var sourceEntity in parentEntities)
            {
                var path = FluentEntityNavigationConvention.EntityGraphPathfinder.FindPath(
                    entityGraph, sourceEntity, targetEntity);
                if (path != null) return path;
            }
            return null;
        }

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

            foreach (var targetEntity in childEntities)
            {
                var path = FindPathFromAnyParent(targetEntity);
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

        foreach (var link in info.Definition.Entities)
        {
            if (string.IsNullOrWhiteSpace(link.AliasProperty)) continue;
            if (result.Navigations.Any(x =>
                    string.Equals(x.NavigationName, link.AliasProperty, StringComparison.Ordinal)))
                continue;

            var path = FindPathFromAnyParent(link.EntityType!);

            result.Navigations.Add(new NavigationInfo
            {
                NavigationName = link.AliasProperty,
                TargetModel = link.EntityType,
                RelatedEntityType = link.EntityType,
                ForeignKeyProperty = link.AliasProperty + "Id",
                PrincipalKeyProperty = link.EntityType!.Name + "Key",
                IsCollection = false,
                TargetIsRoot = rootEntityTypes.Contains(link.EntityType),
                JoinPaths = path != null
                    ? [new NavigationJoinPath { TargetEntity = link.EntityType, Hops = path }]
                    : []
            });
        }

        // Explicit navigations declared via MappingDefinition.Navigations —
        // fully hand-specified join hops, independent of Definition.Entities
        // entirely. Unlike the AliasProperty fallback above (which requires
        // an Entities entry, and therefore also feeds CteResolutions/
        // surrogate-id upserts), this path exists specifically so a model
        // can declare a navigable child relationship WITHOUT that entry
        // also triggering CTE-based FK resolution — e.g.
        // CustomerCustomerEdge.InnerCustomer, where the FK is already the
        // natural CustomerKey and no surrogate-id lookup should happen at
        // upsert time.
        foreach (var navDef in info.Definition.Navigations)
        {
            if (string.IsNullOrWhiteSpace(navDef.NavigationName))
                continue;

            if (result.Navigations.Any(x =>
                    string.Equals(x.NavigationName, navDef.NavigationName, StringComparison.Ordinal)))
                continue;

            var joinPaths = new List<NavigationJoinPath>();

            foreach (var pathDef in navDef.Paths)
            {
                if (pathDef.TargetEntity == null) continue;

                var hops = pathDef.Hops
                    .Where(h => h.FromEntity != null && h.ToEntity != null &&
                                !string.IsNullOrWhiteSpace(h.FromColumn) &&
                                !string.IsNullOrWhiteSpace(h.ToColumn))
                    .Select(h => new FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge(
                        h.FromEntity!, h.FromColumn!, h.ToEntity!, h.ToColumn!))
                    .ToList();

                if (hops.Count == 0) continue;

                joinPaths.Add(new NavigationJoinPath
                {
                    TargetEntity = pathDef.TargetEntity,
                    Hops = hops
                });
            }

            if (joinPaths.Count == 0)
                continue;

            var childEntities = joinPaths.Select(p => p.TargetEntity).ToList();

            result.Navigations.Add(new NavigationInfo
            {
                NavigationName = navDef.NavigationName,
                TargetModel = navDef.TargetModel,
                RelatedEntityType = navDef.Paths.FirstOrDefault()?.TargetEntity,
                IsCollection = navDef.IsCollection,
                TargetIsRoot = childEntities.Any(rootEntityTypes.Contains),
                JoinPaths = joinPaths
            });
        }

        return result;
    }

    internal static INamedTypeSymbol? ResolveElementType(ITypeSymbol type)
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