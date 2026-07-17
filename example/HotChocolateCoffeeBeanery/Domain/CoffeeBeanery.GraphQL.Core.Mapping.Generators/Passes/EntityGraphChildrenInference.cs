using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;
using Microsoft.CodeAnalysis;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes;

public class EntityGraphChildrenInference
{
    public static void Apply(
        MappingClassInfo info,
        ImmutableArray<MappingClassInfo> allMappings,
        List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> edges)
    {
        if (info.EntityType == null)
            return;

        foreach (var edge in edges)
        {
            if (!SymbolEqualityComparer.Default.Equals(
                    edge.PrincipalEntity,
                    info.EntityType))
                continue;


            var childMapping = allMappings.FirstOrDefault(m =>
                SymbolEqualityComparer.Default.Equals(
                    m.EntityType,
                    edge.DependentEntity));

            if (childMapping == null)
                continue;


            if (info.ModelChildren.Any(x =>
                    string.Equals(
                        x.To,
                        childMapping.ModelType.Name,
                        StringComparison.OrdinalIgnoreCase)))
                continue;


            info.ModelChildren.Add(new ModelChildInfo
            {
                To = childMapping.ModelType.Name
            });
        }
    }
}