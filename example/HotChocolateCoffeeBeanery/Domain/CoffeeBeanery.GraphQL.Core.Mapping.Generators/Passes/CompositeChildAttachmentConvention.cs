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
    /// Join column resolution:
    ///
    /// FIXED — previously this used a pure naming convention: assume a
    /// property literally named "{ParentModel.Name}Key" exists on the
    /// parent's IsPrimary anchor entity, then take the FIRST entity in the
    /// child composite's Definition.Entities with a same-named property.
    /// That's a silent guess with no correctness check against real FK
    /// metadata — a coincidental same-named column on the wrong entity, or
    /// entity-declaration ordering, could silently produce the wrong join
    /// (or none at all), exactly the "AutoChildAttachments only linked one
    /// entity" bug this pass exists to prevent.
    ///
    /// This now walks the real FK graph (entityGraph, derived from fluent EF
    /// config by FluentEntityNavigationConvention.CollectAll) via the same
    /// EntityGraphPathfinder BFS that EntityNavigationConvention already
    /// uses, trying every entity in the parent's composite as a possible
    /// source and every entity in the child's composite as a possible
    /// target, and keeping the shortest real path found. Name-matching is
    /// kept only as an explicit, diagnostic-logged last resort when no FK
    /// graph edge connects the two composites at all — it no longer runs
    /// silently as the primary mechanism.
    ///
    /// If neither the graph nor the naming fallback resolves anything, the
    /// attachment is still registered as unresolved (ParentJoinColumn left
    /// blank); NodeBuilder.BuildEdges skips unresolved attachments, so
    /// they're a silent no-op rather than a build error - the convention
    /// simply doesn't apply and a hand-written ChildAttachment is required
    /// instead. A CBM900 diagnostic is emitted in that case so it isn't a
    /// SILENT no-op during generation, even though the runtime behavior is
    /// unchanged.
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
            ImmutableArray<MappingClassInfo> allMappings,
            List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph)
        {
            if (info.Graph != null)
                return;

            if (info.ModelType == null)
                return;


            var modelIndex =
                allMappings
                    .Where(m => m.ModelType != null)
                    .ToDictionary(
                        m => m.ModelType!.Name,
                        m => m,
                        StringComparer.Ordinal);


            var existingFieldNames =
                new HashSet<string>(
                    info.AutoChildAttachments.Select(a => a.FieldName),
                    StringComparer.OrdinalIgnoreCase);


            var anchor =
                info.Definition.Entities
                    .FirstOrDefault(e => e.IsPrimary);


            // All entities that can act as a join SOURCE on the parent side —
            // not just the IsPrimary anchor. A composite parent's FK to a
            // given child may live on any one of its backing entities.
            var parentEntities =
                info.Definition.Entities
                    .Where(e => e.EntityType != null)
                    .Select(e => e.EntityType!)
                    .Distinct(SymbolEqualityComparer.Default)
                    .Cast<INamedTypeSymbol>()
                    .ToList();

            if (parentEntities.Count == 0 && info.EntityType != null)
                parentEntities.Add(info.EntityType);


            foreach (var nav in DiscoverNavigations(info.ModelType))
            {
                // self reference
                if (SymbolEqualityComparer.Default.Equals(
                        nav.TargetType,
                        info.ModelType))
                {
                    continue;
                }


                // Wrapper is not a real child
                if (string.Equals(
                        nav.TargetType.Name,
                        "Wrapper",
                        StringComparison.Ordinal))
                {
                    continue;
                }


                // already mapped manually
                if (existingFieldNames.Contains(nav.PropertyName))
                {
                    continue;
                }


                if (!modelIndex.TryGetValue(
                        nav.TargetType.Name,
                        out var childMapping))
                {
                    continue;
                }


                if (childMapping.ModelType == null)
                    continue;


                // Only composite models need this convention
                if (!childMapping.IsComposite)
                    continue;


                var childEntities =
                    childMapping.Definition.Entities
                        .Where(e => e.EntityType != null)
                        .Select(e => e.EntityType!)
                        .Distinct(SymbolEqualityComparer.Default)
                        .Cast<INamedTypeSymbol>()
                        .ToList();

                if (childEntities.Count == 0)
                    continue;


                INamedTypeSymbol? parentEntityType = null;
                INamedTypeSymbol? childEntityType = null;
                string? parentJoinColumn = null;
                string? childJoinColumn = null;

                // -----------------------------------------------------
                // PRIMARY: real FK-graph pathfinding, any parent entity
                // to any child entity, shortest path wins.
                // -----------------------------------------------------
                FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge? bestEdge = null;

                foreach (var candidateParent in parentEntities)
                {
                    foreach (var candidateChild in childEntities)
                    {
                        var path =
                            FluentEntityNavigationConvention.EntityGraphPathfinder.FindPath(
                                entityGraph,
                                candidateParent,
                                candidateChild);

                        if (path == null || path.Count == 0)
                            continue;

                        // Only a direct (single-hop) edge can be expressed by
                        // this convention's AutoChildAttachmentInfo shape
                        // (one parent column, one child column). Multi-hop
                        // paths are exactly what BuildCompositeStorageJoinInfo
                        // already threads through the child's own composite
                        // chain once the first hop lands it inside that
                        // composite — so prefer the first edge of the
                        // shortest path.
                        var candidateEdge = path[0];

                        if (bestEdge != null)
                            continue; // keep the first (shortest, since BFS) match

                        parentEntityType = candidateParent;
                        childEntityType =
                            SymbolEqualityComparer.Default.Equals(
                                candidateEdge.PrincipalEntity, candidateParent)
                                ? candidateEdge.DependentEntity
                                : candidateEdge.PrincipalEntity;

                        parentJoinColumn =
                            SymbolEqualityComparer.Default.Equals(
                                candidateEdge.PrincipalEntity, candidateParent)
                                ? candidateEdge.PrincipalColumn
                                : candidateEdge.DependentColumn;

                        childJoinColumn =
                            SymbolEqualityComparer.Default.Equals(
                                candidateEdge.PrincipalEntity, candidateParent)
                                ? candidateEdge.DependentColumn
                                : candidateEdge.PrincipalColumn;

                        bestEdge = candidateEdge;
                    }
                }

                // -----------------------------------------------------
                // FALLBACK: naming convention, ONLY when no FK graph edge
                // connects the two composites at all. Logged loudly via
                // a CBM901 diagnostic so a maintainer can see the guess
                // was used, rather than it silently determining the join.
                // -----------------------------------------------------
                if (bestEdge == null)
                {
                    var parentKeyPropertyName =
                        info.ModelType.Name + "Key";

                    var fallbackParentEntity =
                        anchor?.EntityType;

                    if (fallbackParentEntity != null)
                    {
                        var parentProp =
                            fallbackParentEntity.GetMembers()
                                .OfType<IPropertySymbol>()
                                .FirstOrDefault(p =>
                                    string.Equals(
                                        p.Name,
                                        parentKeyPropertyName,
                                        StringComparison.OrdinalIgnoreCase));

                        if (parentProp != null)
                        {
                            foreach (var childEntity in childEntities)
                            {
                                var childProp =
                                    childEntity.GetMembers()
                                        .OfType<IPropertySymbol>()
                                        .FirstOrDefault(p =>
                                            string.Equals(
                                                p.Name,
                                                parentProp.Name,
                                                StringComparison.OrdinalIgnoreCase));

                                if (childProp == null)
                                    continue;

                                parentEntityType = fallbackParentEntity;
                                childEntityType = childEntity;
                                parentJoinColumn = parentProp.Name;
                                childJoinColumn = childProp.Name;

                                ReportFallbackUsed(
                                    info,
                                    nav.PropertyName,
                                    fallbackParentEntity.Name,
                                    parentProp.Name,
                                    childEntity.Name,
                                    childProp.Name);

                                break;
                            }
                        }
                    }

                    if (parentEntityType == null)
                    {
                        ReportUnresolved(
                            info,
                            nav.PropertyName,
                            childMapping.ModelType.Name);
                    }
                }


                info.AutoChildAttachments.Add(
                    new AutoChildAttachmentInfo
                    {
                        FieldName = nav.PropertyName,

                        ToModelName = childMapping.ModelType.Name,

                        ChildModelType = childMapping.ModelType,

                        ParentEntityType =
                            parentEntityType ?? anchor?.EntityType!,

                        ChildEntityType =
                            childEntityType ?? parentEntityType ?? anchor?.EntityType!,

                        ParentJoinColumn =
                            parentJoinColumn ?? string.Empty,

                        ChildJoinColumn =
                            childJoinColumn ?? string.Empty
                    });


                existingFieldNames.Add(nav.PropertyName);
            }

            BuildCompositeStorageJoinInfo(
                info,
                allMappings,
                entityGraph);
        }

        internal static List<CompositeStorageJoinInfo> ComputeCompositeJoinChain(
            INamedTypeSymbol startEntity,
            List<INamedTypeSymbol> compositeTypes,
            List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph)
        {
            var result =
                new List<CompositeStorageJoinInfo>();


            var visited =
                new HashSet<INamedTypeSymbol>(
                    SymbolEqualityComparer.Default);


            var queue =
                new Queue<INamedTypeSymbol>();


            visited.Add(startEntity);

            queue.Enqueue(startEntity);



            while (queue.Count > 0)
            {
                var current =
                    queue.Dequeue();


                foreach (var edge in entityGraph)
                {
                    INamedTypeSymbol? next = null;

                    string? currentColumn = null;

                    string? nextColumn = null;



                    if (SymbolEqualityComparer.Default.Equals(
                            edge.PrincipalEntity,
                            current))
                    {
                        next =
                            edge.DependentEntity;

                        currentColumn =
                            edge.PrincipalColumn;

                        nextColumn =
                            edge.DependentColumn;
                    }
                    else if (SymbolEqualityComparer.Default.Equals(
                                 edge.DependentEntity,
                                 current))
                    {
                        next =
                            edge.PrincipalEntity;

                        currentColumn =
                            edge.DependentColumn;

                        nextColumn =
                            edge.PrincipalColumn;
                    }


                    if (next == null)
                        continue;


                    if (!compositeTypes.Any(t =>
                            SymbolEqualityComparer.Default.Equals(t, next)))
                    {
                        continue;
                    }


                    if (visited.Contains(next))
                        continue;


                    if (string.IsNullOrWhiteSpace(currentColumn) ||
                        string.IsNullOrWhiteSpace(nextColumn))
                    {
                        continue;
                    }



                    visited.Add(next);

                    queue.Enqueue(next);



                    result.Add(
                        new CompositeStorageJoinInfo
                        {
                            ParentEntityType = current,

                            ChildEntityType = next,

                            ParentJoinColumn = currentColumn,

                            ChildJoinColumn = nextColumn
                        });
                }
            }


            return result;
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
            ImmutableArray<MappingClassInfo> allMappings,
            List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph)
        {
            info.CompositeStorageJoinInfo.Clear();


            Report(
                info,
                $"Building composite joins. Model={info.ModelType.Name}, Composite={info.IsComposite}");



            if (!info.IsComposite)
            {
                Report(
                    info,
                    "Skipped. IsComposite=false");

                return;
            }



            var anchor =
                info.Definition.Entities
                    .FirstOrDefault(e => e.IsPrimary);



            if (anchor?.EntityType == null)
            {
                Report(
                    info,
                    "Skipped. No primary entity found");

                return;
            }



            Report(
                info,
                $"Anchor entity={anchor.EntityType.Name}");



            var compositeTypes =
                info.Definition.Entities
                    .Where(e => e.EntityType != null)
                    .Select(e => e.EntityType!)
                    .ToList();



            Report(
                info,
                "Composite entities=" +
                string.Join(
                    ",",
                    compositeTypes.Select(x => x.Name)));



            var joins =
                ComputeCompositeJoinChain(
                    anchor.EntityType,
                    compositeTypes,
                    entityGraph);



            Report(
                info,
                $"Computed joins={joins.Count}");



            foreach (var join in joins)
            {
                Report(
                    info,
                    $"JOIN {join.ParentEntityType.Name}" +
                    $"({join.ParentJoinColumn}) -> " +
                    $"{join.ChildEntityType.Name}" +
                    $"({join.ChildJoinColumn})");
            }



            info.CompositeStorageJoinInfo.AddRange(
                joins);
        }
        
        private static void Report(
            MappingClassInfo info,
            string message)
        {
            info.Diagnostics.Add(
                Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "CBM900",
                        "Composite join debug",
                        message,
                        "Mapping",
                        DiagnosticSeverity.Warning,
                        true),
                    Location.None));
        }

        private static void ReportFallbackUsed(
            MappingClassInfo info,
            string navigationPropertyName,
            string parentEntityName,
            string parentColumn,
            string childEntityName,
            string childColumn)
        {
            info.Diagnostics.Add(
                Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "CBM901",
                        "Composite child join resolved by naming convention, not FK graph",
                        "Navigation '{0}' on model '{1}' has no FK-graph edge between its " +
                        "composite entities and the child's; fell back to name-matching " +
                        "'{2}.{3}' -> '{4}.{5}'. Verify this is the intended relationship, " +
                        "or add an explicit EntityForeignKeyGraph edge / HasForeignKey config.",
                        "Mapping",
                        DiagnosticSeverity.Warning,
                        true),
                    Location.None,
                    navigationPropertyName,
                    info.ModelType!.Name,
                    parentEntityName,
                    parentColumn,
                    childEntityName,
                    childColumn));
        }

        private static void ReportUnresolved(
            MappingClassInfo info,
            string navigationPropertyName,
            string childModelName)
        {
            info.Diagnostics.Add(
                Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "CBM902",
                        "Composite child join unresolved",
                        "Navigation '{0}' on model '{1}' -> '{2}' could not be resolved via " +
                        "the FK graph or the naming-convention fallback. This attachment will " +
                        "be registered with an empty join column and silently skipped at " +
                        "runtime by NodeBuilder.BuildEdges. Add a hand-written ChildAttachment " +
                        "or an EntityForeignKeyGraph edge to fix.",
                        "Mapping",
                        DiagnosticSeverity.Warning,
                        true),
                    Location.None,
                    navigationPropertyName,
                    info.ModelType!.Name,
                    childModelName));
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