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
    // FIXED: the expected FK column for disambiguating parallel edges
    // (e.g. InnerCustomerId vs OuterCustomerId, both -> Customer) must
    // be derived from the entity link's AliasProperty using the
    // "{Alias}Id" convention (matching MutationMetadataEmitter.EmitFactory's
    // expectedFkColumn logic), NOT from the link's ToColumn/EntityKey.
    // ToColumn holds the model-level NATURAL key column name (e.g.
    // "CustomerKey"), which is frequently identical across multiple
    // parallel entries pointing at the same related entity — exactly the
    // case here, where both InnerCustomer and OuterCustomer entries set
    // EntityKey = "CustomerKey". Passing that into FindDirectEdgeForColumn
    // never matches any real entityGraph edge (whose DependentColumn is
    // the PHYSICAL FK column, e.g. "InnerCustomerId"), so it silently fell
    // through to FindPathFromAnyParent for both navigations, which is
    // itself ambiguous for parallel edges and returns the same one every
    // time -- collapsing InnerCustomer and OuterCustomer onto a single
    // join.
    // ---------------------------------------------------------------
    string? ExpectedFkColumn(EntityDefinitionInfo? entity)
    {
        if (entity == null)
            return null;

        return !string.IsNullOrWhiteSpace(entity.AliasProperty)
            ? entity.AliasProperty + "Id"
            : entity.ToColumn;
    }

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
            if (!string.Equals(prop.Name, relatedModelType.Name, StringComparison.Ordinal))
                continue;

            childEntities = ResolveNavigationTargetEntities(childModel);
        }

        if (childEntities.Count == 0)
            continue;

        var joinPaths = new List<NavigationJoinPath>();

        foreach (var targetEntity in childEntities)
        {
            var path =
                FindDirectEdgeForColumn(targetEntity, ExpectedFkColumn(matchingEntity))
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

        var path =
            FindDirectEdgeForColumn(link.EntityType!, ExpectedFkColumn(link))
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