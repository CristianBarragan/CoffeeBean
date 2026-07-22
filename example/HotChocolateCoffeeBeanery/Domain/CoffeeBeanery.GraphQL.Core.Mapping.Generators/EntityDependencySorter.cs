using System;
using System.Collections.Generic;
using System.Linq;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;
using Microsoft.CodeAnalysis;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators;

internal static class EntityDependencySorter
{
    public static IReadOnlyList<INamedTypeSymbol> Sort(
        IEnumerable<INamedTypeSymbol> entities,
        IEnumerable<ForeignKeyDefinitionInfo> foreignKeys)
    {
        var graph =
            new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(
                SymbolEqualityComparer.Default);

        foreach (var entity in entities)
        {
            graph[entity] =
                new HashSet<INamedTypeSymbol>(
                    SymbolEqualityComparer.Default);
        }

        foreach (var fk in foreignKeys)
        {
            graph[fk.Entity].Add(fk.DependsOn);

            if (!graph.ContainsKey(fk.DependsOn))
                graph[fk.DependsOn] = new HashSet<INamedTypeSymbol>();
        }

        var result = new List<INamedTypeSymbol>();

        while (graph.Count > 0)
        {
            var ready = graph
                .Where(x => x.Value.Count == 0)
                .Select(x => x.Key)
                .ToList();

            if (ready.Count == 0)
            {
                throw new InvalidOperationException(
                    "Circular foreign key dependency detected.");
            }

            foreach (var entity in ready)
            {
                result.Add(entity);
                graph.Remove(entity);
            }

            foreach (var dependencies in graph.Values)
            {
                foreach (var entity in ready)
                {
                    dependencies.Remove(entity);
                }
            }
        }

        return result;
    }
}