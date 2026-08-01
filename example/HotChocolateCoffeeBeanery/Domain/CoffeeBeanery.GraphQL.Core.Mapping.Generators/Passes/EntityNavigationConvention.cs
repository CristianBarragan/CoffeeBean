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

        // ---------------------------------------------------------------
        // FIXED: FindPathFromAnyParent's BFS is keyed purely by entity
        // TYPE, so when two edges connect the same pair of entity types
        // (e.g. CustomerCustomerRelationship -> Customer via BOTH
        // InnerCustomerId and OuterCustomerId), it can only ever return
        // one of them — every caller asking for a path between that same
        // type-pair gets back the identical edge, regardless of which
        // specific navigation/alias they meant. This silently collapsed
        // InnerCustomer and OuterCustomer onto the same join.
        //
        // When the caller already knows which specific FK column it wants
        // (an AliasProperty-matched entity link's declared ToColumn), this
        // looks for that exact edge directly — checked BEFORE falling back
        // to the generic (and inherently ambiguous, for parallel edges)
        // BFS pathfinder.
        // ---------------------------------------------------------------
        List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge>? FindDirectEdgeForColumn(
            INamedTypeSymbol targetEntity,
            string? expectedColumn)
        {
            if (string.IsNullOrWhiteSpace(expectedColumn))
                return null;

            foreach (var sourceEntity in parentEntities)
            {
                var edge = entityGraph.FirstOrDefault(e =>
                    (
                        SymbolEqualityComparer.Default.Equals(e.DependentEntity, sourceEntity) &&
                        SymbolEqualityComparer.Default.Equals(e.PrincipalEntity, targetEntity) &&
                        string.Equals(e.DependentColumn, expectedColumn, StringComparison.OrdinalIgnoreCase)
                    )
                    ||
                    (
                        SymbolEqualityComparer.Default.Equals(e.PrincipalEntity, sourceEntity) &&
                        SymbolEqualityComparer.Default.Equals(e.DependentEntity, targetEntity) &&
                        string.Equals(e.PrincipalColumn, expectedColumn, StringComparison.OrdinalIgnoreCase)
                    ));

                if (edge != null)
                {
                    return new List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> { edge };
                }
            }

            return null;
        }

        // ---------------------------------------------------------------
        // FIXED: when resolving a composite child (e.g. Product, spanning
        // Account/Transaction/Contract/CustomerBankingRelationship), this
        // used to treat EVERY one of the child's backing entities as an
        // equally valid navigation target, hand all of them to
        // PlannerEmitter as candidate JoinPaths, and let
        // `.OrderBy(p => p.Hops.Count).FirstOrDefault()` pick whichever
        // was NEAREST in the FK graph — not necessarily the composite's
        // actual anchor entity. From Customer, CustomerBankingRelationship
        // is 1 hop away while Product's real anchor (Account) is 4 hops
        // away, so the join silently resolved to CustomerBankingRelationship
        // instead of Account, and Product's own internal composite-chain
        // joins (which assume they start FROM Account) ended up referencing
        // an alias nothing had actually introduced.
        //
        // A composite child has exactly one correct entry point: whichever
        // entity its own MappingDefinition marks IsPrimary — the same
        // "anchor" concept CompositeChildAttachmentConvention already uses
        // when building a composite's OWN internal join chain. Resolving
        // the navigation FK path against every backing entity conflates
        // "nearest reachable entity" with "correct composite entry point,"
        // which are different questions. Restricting to just the anchor
        // fixes that, and falls back to the old any-entity behavior only
        // if the composite has no explicit IsPrimary entity (shouldn't
        // normally happen, but avoids silently producing zero navigations
        // for a composite whose mapping is incomplete).
        // ---------------------------------------------------------------
        List<INamedTypeSymbol> ResolveNavigationTargetEntities(MappingClassInfo childModel)
        {
            var anchor =
                childModel.Definition.Entities
                    .FirstOrDefault(e => e.IsPrimary && e.EntityType != null)
                    ?.EntityType;

            if (anchor != null)
            {
                return new List<INamedTypeSymbol> { anchor };
            }

            return childModel.Definition.Entities
                .Where(e => e.EntityType != null)
                .Select(e => e.EntityType!)
                .Distinct(SymbolEqualityComparer.Default)
                .Cast<INamedTypeSymbol>()
                .ToList();
        }

        var modelProperties = info.ModelType.GetMembers().OfType<IPropertySymbol>().ToList();

foreach (var prop in modelProperties)
{
    if (prop.IsStatic)
        continue;

    var relatedModelType = ResolveElementType(prop.Type);
    if (relatedModelType == null)
        continue;

    var childModel = allMappings.FirstOrDefault(m =>
        m.IsModel &&
        SymbolEqualityComparer.Default.Equals(m.ModelType, relatedModelType));

    if (childModel == null)
        continue;

    // ---------------------------------------------------------
    // Prefer an explicit AliasProperty match.
    // ---------------------------------------------------------

    var matchingEntity = info.Definition.Entities.FirstOrDefault(e =>
        e.EntityType != null &&
        SymbolEqualityComparer.Default.Equals(e.EntityType, childModel.EntityType) &&
        string.Equals(e.AliasProperty, prop.Name, StringComparison.Ordinal));

    List<INamedTypeSymbol> childEntities;

    if (matchingEntity != null)
    {
        childEntities = new List<INamedTypeSymbol>
        {
            matchingEntity.EntityType!
        };
    }
    else
    {
        // Fall back to the original convention:
        // Customer Customer
        // Account Account
        if (!string.Equals(prop.Name, relatedModelType.Name, StringComparison.Ordinal))
            continue;

        childEntities = ResolveNavigationTargetEntities(childModel);
    }

    if (childEntities.Count == 0)
        continue;

    var joinPaths = new List<NavigationJoinPath>();

    foreach (var targetEntity in childEntities)
    {
        // Try the column-specific direct edge first (disambiguates
        // parallel edges to the same entity type, e.g. Inner/OuterCustomer),
        // then fall back to generic BFS pathfinding.
        var path =
            FindDirectEdgeForColumn(targetEntity, matchingEntity?.ToColumn)
            ?? FindPathFromAnyParent(targetEntity);

        if (path == null)
            continue;

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
        IsCollection = IsCollection(prop.Type),
        TargetIsRoot = childEntities.Any(rootEntityTypes.Contains),
        JoinPaths = joinPaths
    });
}

foreach (var child in info.ModelChildren)
{
    if (result.Navigations.Any(x =>
            string.Equals(x.NavigationName, child.NavigationName, StringComparison.Ordinal)))
        continue;

    var childModel = allMappings.FirstOrDefault(m =>
        m.IsModel &&
        string.Equals(m.ModelType.Name, child.To, StringComparison.Ordinal));

    if (childModel == null)
        continue;

    var childEntities = ResolveNavigationTargetEntities(childModel);

    if (childEntities.Count == 0)
        continue;

    var joinPaths = new List<NavigationJoinPath>();

    foreach (var targetEntity in childEntities)
    {
        var path = FindPathFromAnyParent(targetEntity);

        if (path == null)
            continue;

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
        NavigationName = child.NavigationName,
        TargetModel = childModel.ModelType,
        RelatedEntityType = childModel.EntityType,
        IsCollection = false,
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

            // Column-specific direct edge first, same disambiguation as above.
            var path =
                FindDirectEdgeForColumn(link.EntityType!, link.ToColumn)
                ?? FindPathFromAnyParent(link.EntityType!);

            result.Navigations.Add(new NavigationInfo
            {
                NavigationName = link.AliasProperty,
                TargetModel = link.EntityType,
                RelatedEntityType = link.EntityType,
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