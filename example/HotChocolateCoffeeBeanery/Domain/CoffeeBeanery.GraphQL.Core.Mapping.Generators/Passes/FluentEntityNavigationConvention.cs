using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes
{
    /// <summary>
    /// Roslyn-only convention pass.
    ///
    /// Reads IEntityTypeConfiguration&lt;TEntity&gt; fluent configuration and extracts
    /// navigation inverse information and derived business-key foreign key metadata.
    ///
    /// No reflection.
    /// No EF runtime model access.
    /// </summary>
    public static class FluentEntityNavigationConvention
    {
        public record DerivedForeignKey(
            INamedTypeSymbol DeclaringEntityType,
            INamedTypeSymbol RelatedEntityType,
            string ModelForeignKeyProperty,
            string ModelPrincipalKeyProperty,
            string RawForeignKeyColumn,
            string RawPrincipalKeyColumn,
            string NavigationName,
            bool IsAmbiguous);


        public static List<DerivedForeignKey> CollectAll(
            Compilation compilation,
            CancellationToken ct)
        {
            var results = new List<DerivedForeignKey>();

            foreach (var tree in compilation.SyntaxTrees)
            {
                ct.ThrowIfCancellationRequested();

                var semanticModel =
                    compilation.GetSemanticModel(tree);

                var root =
                    tree.GetRoot(ct);


                foreach (var classDecl in root.DescendantNodes()
                             .OfType<ClassDeclarationSyntax>())
                {
                    ct.ThrowIfCancellationRequested();


                    var entityType =
                        TryGetConfiguredEntityType(
                            classDecl,
                            semanticModel,
                            ct);


                    if (entityType == null)
                        continue;


                    var configure =
                        classDecl.Members
                            .OfType<MethodDeclarationSyntax>()
                            .FirstOrDefault(x =>
                                x.Identifier.Text == "Configure" &&
                                x.Body != null);


                    if (configure?.Body == null)
                        continue;


                    foreach (var statement in configure.Body.Statements
                                 .OfType<ExpressionStatementSyntax>())
                    {
                        if (statement.Expression is not InvocationExpressionSyntax outer)
                            continue;


                        var chain =
                            CollectChain(outer);


                        var hasIndex =
                            chain.FindIndex(x =>
                                x.Name is "HasOne" or "HasMany");


                        if (hasIndex < 0)
                            continue;


                        var withIndex =
                            chain.FindIndex(
                                hasIndex + 1,
                                x => x.Name is "WithOne" or "WithMany");


                        if (withIndex < 0)
                            continue;


                        var fkIndex =
                            chain.FindIndex(
                                withIndex + 1,
                                x => x.Name == "HasForeignKey");


                        if (fkIndex < 0)
                            continue;


                        var navigationName =
                            ExtractLambdaMemberName(
                                chain[hasIndex].Invocation);


                        var inverseName =
                            ExtractLambdaMemberName(
                                chain[withIndex].Invocation);


                        var rawFkColumn =
                            ExtractLambdaMemberName(
                                chain[fkIndex].Invocation);


                        if (navigationName == null ||
                            inverseName == null ||
                            rawFkColumn == null)
                        {
                            continue;
                        }


                        var navigationProperty =
                            entityType.GetMembers()
                                .OfType<IPropertySymbol>()
                                .FirstOrDefault(x =>
                                    string.Equals(
                                        x.Name,
                                        navigationName,
                                        StringComparison.Ordinal));


                        var relatedEntity =
                            ResolveRelatedType(
                                navigationProperty);


                        if (relatedEntity == null)
                            continue;
                            
                                                    //
                        // Real EF principal key.
                        //
                        // Example:
                        //
                        // HasPrincipalKey(x => x.AccountId)
                        //
                        // If not explicitly configured, EF uses the PK.
                        //
                        var principalKeyIndex =
                            chain.FindIndex(
                                fkIndex + 1,
                                x => x.Name == "HasPrincipalKey");


                        var rawPrincipalKeyColumn =
                            principalKeyIndex >= 0
                                ? ExtractLambdaMemberName(
                                    chain[principalKeyIndex].Invocation)
                                : null;


                        if (string.IsNullOrWhiteSpace(rawPrincipalKeyColumn))
                        {
                            rawPrincipalKeyColumn =
                                FindPrimaryKeyPropertyName(
                                    relatedEntity);
                        }


                        results.Add(
                            new DerivedForeignKey(
                                DeclaringEntityType:
                                    entityType,

                                RelatedEntityType:
                                    relatedEntity,

                                ModelForeignKeyProperty:
                                    rawFkColumn,

                                ModelPrincipalKeyProperty:
                                    rawPrincipalKeyColumn ?? "Id",

                                RawForeignKeyColumn:
                                    rawFkColumn,

                                RawPrincipalKeyColumn:
                                    rawPrincipalKeyColumn ?? "Id",

                                NavigationName:
                                    navigationName,

                                IsAmbiguous:
                                    false));
                    }
                }
            }

            return results;
        }


        public static void AddNavigationKeyFields(
            MappingClassInfo info,
            IEnumerable<DerivedForeignKey> foreignKeys)
        {
            if (info.EntityType == null)
                return;


            foreach (var fk in foreignKeys)
            {
                if (!SymbolEqualityComparer.Default.Equals(
                        fk.DeclaringEntityType,
                        info.EntityType))
                {
                    continue;
                }


                if (string.IsNullOrWhiteSpace(
                        fk.NavigationName))
                {
                    continue;
                }


                var sourceName =
                    fk.NavigationName + "Key";


                var exists =
                    info.FieldMaps.Any(x =>
                        string.Equals(
                            x.SourceName,
                            sourceName,
                            StringComparison.Ordinal));


                if (exists)
                    continue;


                info.FieldMaps.Add(
                    new FieldInfo
                    {
                        SourceName = sourceName,

                        DestinationEntity =
                            fk.DeclaringEntityType.Name,

                        DestinationName =
                            fk.RawForeignKeyColumn,

                        IsNavigationKey = true
                    });
            }
        }


        private static string? FindPrimaryKeyPropertyName(
            INamedTypeSymbol entityType)
        {
            var id =
                entityType.GetMembers()
                    .OfType<IPropertySymbol>()
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.Name,
                            "Id",
                            StringComparison.OrdinalIgnoreCase));


            if (id != null)
                return id.Name;


            return entityType.GetMembers()
                .OfType<IPropertySymbol>()
                .FirstOrDefault(x =>
                    x.Name.EndsWith(
                        "Id",
                        StringComparison.OrdinalIgnoreCase))
                ?.Name;
        }
        
                internal static class EntityGraphPathfinder
        {
            public static List<EntityForeignKeyGraph.Edge>? FindPath(
                List<EntityForeignKeyGraph.Edge> edges,
                INamedTypeSymbol from,
                INamedTypeSymbol to)
            {
                var adjacency =
                    new Dictionary<
                        INamedTypeSymbol,
                        List<(EntityForeignKeyGraph.Edge Edge, bool Forward)>>(
                        SymbolEqualityComparer.Default);


                void AddAdj(
                    INamedTypeSymbol node,
                    EntityForeignKeyGraph.Edge edge,
                    bool forward)
                {
                    if (!adjacency.TryGetValue(node, out var list))
                    {
                        adjacency[node] = list = new();
                    }

                    list.Add((edge, forward));
                }


                foreach (var edge in edges)
                {
                    AddAdj(
                        edge.DependentEntity,
                        edge,
                        true);

                    AddAdj(
                        edge.PrincipalEntity,
                        edge,
                        false);
                }


                var visited =
                    new HashSet<INamedTypeSymbol>(
                        SymbolEqualityComparer.Default)
                    {
                        from
                    };


                var queue =
                    new Queue<(
                        INamedTypeSymbol Node,
                        List<EntityForeignKeyGraph.Edge> Path)>();


                queue.Enqueue(
                    (
                        from,
                        new List<EntityForeignKeyGraph.Edge>()
                    ));


                while (queue.Count > 0)
                {
                    var current =
                        queue.Dequeue();


                    if (SymbolEqualityComparer.Default.Equals(
                            current.Node,
                            to))
                    {
                        return current.Path;
                    }


                    if (!adjacency.TryGetValue(
                            current.Node,
                            out var neighbors))
                    {
                        continue;
                    }


                    foreach (var (edge, forward) in neighbors)
                    {
                        var next =
                            forward
                                ? edge.PrincipalEntity
                                : edge.DependentEntity;


                        if (visited.Contains(next))
                            continue;


                        visited.Add(next);


                        queue.Enqueue(
                            (
                                next,
                                new List<EntityForeignKeyGraph.Edge>(
                                    current.Path)
                                {
                                    edge
                                }
                            ));
                    }
                }


                return null;
            }
        }


        public static class EntityForeignKeyGraph
        {
            public sealed record Edge(
                INamedTypeSymbol DependentEntity,
                string DependentColumn,
                INamedTypeSymbol PrincipalEntity,
                string PrincipalColumn);



            public sealed record EntityGraphBuildResult(
                List<Edge> Edges,
                List<string> Diagnostics);



            public static EntityGraphBuildResult Build(
                Compilation compilation,
                CancellationToken ct)
            {
                var edges =
                    new List<Edge>();

                var diagnostics =
                    new List<string>();


                var attributeType =
                    compilation.GetTypeByMetadataName(
                        "CoffeeBeanery.GraphQL.Core.Mapping.EntityForeignKeyGraphAttribute");


                if (attributeType == null)
                {
                    return new EntityGraphBuildResult(
                        edges,
                        diagnostics);
                }


                AttributeData? found = null;


                found =
                    compilation.Assembly
                        .GetAttributes()
                        .FirstOrDefault(a =>
                            SymbolEqualityComparer.Default.Equals(
                                a.AttributeClass,
                                attributeType));


                if (found == null)
                {
                    var assemblies =
                        new Queue<IAssemblySymbol>();


                    foreach (var reference in compilation.References)
                    {
                        if (compilation.GetAssemblyOrModuleSymbol(reference)
                            is IAssemblySymbol assembly)
                        {
                            assemblies.Enqueue(assembly);
                        }
                    }


                    var visited =
                        new HashSet<string>();


                    while (assemblies.Count > 0)
                    {
                        var assembly =
                            assemblies.Dequeue();


                        if (!visited.Add(
                                assembly.Name))
                        {
                            continue;
                        }


                        found =
                            assembly.GetAttributes()
                                .FirstOrDefault(a =>
                                    a.AttributeClass?.ToDisplayString()
                                    ==
                                    "CoffeeBeanery.GraphQL.Core.Mapping.EntityForeignKeyGraphAttribute");


                        if (found != null)
                            break;


                        foreach (var reference in assembly.Modules
                                     .SelectMany(x =>
                                         x.ReferencedAssemblySymbols))
                        {
                            assemblies.Enqueue(reference);
                        }
                    }
                }


                if (found == null ||
                    found.ConstructorArguments.Length == 0)
                {
                    return new EntityGraphBuildResult(
                        edges,
                        diagnostics);
                }
            
                            var serialized =
                    found.ConstructorArguments[0].Value
                    as string;


                if (string.IsNullOrWhiteSpace(serialized))
                {
                    return new EntityGraphBuildResult(
                        edges,
                        diagnostics);
                }


                foreach (var line in serialized.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;


                    var parts =
                        line.Split('|');


                    if (parts.Length != 4)
                        continue;


                    var dependentType =
                        compilation.GetTypeByMetadataName(
                            parts[0]);


                    var principalType =
                        compilation.GetTypeByMetadataName(
                            parts[2]);


                    if (dependentType == null ||
                        principalType == null)
                    {
                        diagnostics.Add(
                            $"Unable to resolve FK graph types: {parts[0]} -> {parts[2]}");

                        continue;
                    }


                    edges.Add(
                        new Edge(
                            dependentType,
                            parts[1],
                            principalType,
                            parts[3]));
                }


                return new EntityGraphBuildResult(
                    edges,
                    diagnostics);
            }
        }



        private static INamedTypeSymbol? ResolveRelatedType(
            IPropertySymbol? property)
        {
            if (property == null)
                return null;


            var type =
                property.Type;


            if (type is INamedTypeSymbol generic &&
                generic.IsGenericType &&
                generic.TypeArguments.Length == 1)
            {
                return generic.TypeArguments[0]
                    as INamedTypeSymbol;
            }


            return type as INamedTypeSymbol;
        }



        private static IPropertySymbol? FindProperty(
            INamedTypeSymbol type,
            string name)
        {
            return type.GetMembers()
                .OfType<IPropertySymbol>()
                .FirstOrDefault(x =>
                    string.Equals(
                        x.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase));
        }



        private static INamedTypeSymbol? TryGetConfiguredEntityType(
            ClassDeclarationSyntax classDecl,
            SemanticModel semanticModel,
            CancellationToken ct)
        {
            if (semanticModel.GetDeclaredSymbol(
                    classDecl,
                    ct)
                is not INamedTypeSymbol symbol)
            {
                return null;
            }


            foreach (var iface in symbol.AllInterfaces)
            {
                if (iface.Name ==
                    "IEntityTypeConfiguration" &&
                    iface.TypeArguments.Length == 1 &&
                    iface.TypeArguments[0]
                        is INamedTypeSymbol entity)
                {
                    return entity;
                }
            }


            return null;
        }



        private static List<(string Name, InvocationExpressionSyntax Invocation)>
            CollectChain(
                InvocationExpressionSyntax outer)
        {
            var result =
                new List<(string, InvocationExpressionSyntax)>();


            var current =
                outer;


            while (current.Expression
                   is MemberAccessExpressionSyntax member)
            {
                var name =
                    member.Name is GenericNameSyntax generic
                        ? generic.Identifier.Text
                        : member.Name.Identifier.Text;


                result.Insert(
                    0,
                    (
                        name,
                        current
                    ));


                if (member.Expression
                    is InvocationExpressionSyntax inner)
                {
                    current = inner;
                }
                else
                {
                    break;
                }
            }


            return result;
        }
        
        private static string? ExtractLambdaMemberName(
            InvocationExpressionSyntax invocation)
        {
            var argument =
                invocation.ArgumentList.Arguments
                    .FirstOrDefault()
                    ?.Expression;


            if (argument is not SimpleLambdaExpressionSyntax lambda)
                return null;


            if (lambda.Body is MemberAccessExpressionSyntax member)
            {
                return member.Name.Identifier.Text;
            }


            return null;
        }
    }
}
                
                