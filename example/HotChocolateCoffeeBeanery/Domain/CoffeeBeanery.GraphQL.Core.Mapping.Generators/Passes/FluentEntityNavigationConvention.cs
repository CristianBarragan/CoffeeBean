using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
            string NavigationName,
            bool IsAmbiguous);


        public static List<DerivedForeignKey> CollectAll(Compilation compilation, CancellationToken ct)
        {
            var results = new List<DerivedForeignKey>();

            foreach (var tree in compilation.SyntaxTrees)
            {
                ct.ThrowIfCancellationRequested();
                var semanticModel = compilation.GetSemanticModel(tree);
                var root = tree.GetRoot(ct);

                foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    ct.ThrowIfCancellationRequested();

                    var entityType = TryGetConfiguredEntityType(classDecl, semanticModel, ct);
                    if (entityType is null) continue;

                    var configure = classDecl.Members.OfType<MethodDeclarationSyntax>()
                        .FirstOrDefault(x => x.Identifier.Text == "Configure" && x.Body != null);
                    if (configure?.Body == null) continue;

                    var entityResults = new List<DerivedForeignKey>();

                    foreach (var statement in configure.Body.Statements.OfType<ExpressionStatementSyntax>())
                    {

                        if (statement.Expression is not InvocationExpressionSyntax outer)
                            continue;


                        var chain = CollectChain(outer);


                        var hasIndex = chain.FindIndex(x =>
                            x.Name is "HasOne" or "HasMany");

                        if (hasIndex < 0)
                            continue;


                        var withIndex = chain.FindIndex(
                            hasIndex + 1,
                            x => x.Name is "WithOne" or "WithMany");


                        if (withIndex < 0)
                            continue;


                        var navName =
                            ExtractLambdaMemberName(
                                chain[hasIndex].Invocation);

                        var inverseName =
                            ExtractLambdaMemberName(
                                chain[withIndex].Invocation);


                        if (navName == null ||
                            inverseName == null)
                            continue;


                        var navProperty =
                            entityType.GetMembers()
                                .OfType<IPropertySymbol>()
                                .FirstOrDefault(x =>
                                    x.Name == navName);


                        var relatedEntity =
                            ResolveRelatedType(navProperty);


                        if (relatedEntity == null)
                            continue;


                        //
                        // Business key convention:
                        //
                        // Entity A:
                        //     CustomerRelationship.CustomerKey
                        //
                        // Related entity:
                        //     Customer.CustomerKey
                        //
                        // The inverse navigation gives us the key name.
                        //
                        var foreignKey =
                            FindProperty(
                                relatedEntity,
                                inverseName + "Key");


                        var principalKey =
                            FindProperty(
                                entityType,
                                entityType.Name + "Key");


                        if (foreignKey == null ||
                            principalKey == null)
                            continue;


                        entityResults.Add(
                            new DerivedForeignKey(
                                entityType,
                                relatedEntity,
                                foreignKey.Name,
                                principalKey.Name,
                                navName,
                                false));
                    }


                    var ambiguous = new HashSet<INamedTypeSymbol>(
                        SymbolEqualityComparer.Default);

                    foreach (var group in entityResults
                                 .GroupBy(
                                     x => x.RelatedEntityType))
                    {
                        if (group.Count() > 1)
                        {
                            ambiguous.Add(group.Key);
                        }
                    }
                    
                    foreach (var r in entityResults)
                    {
                        results.Add(ambiguous.Contains(r.RelatedEntityType)
                            ? r with { IsAmbiguous = true }
                            : r);
                    }
                }
            }


            return results;
        }
        
        internal static class EntityGraphPathfinder
        {
            public static List<EntityForeignKeyGraph.Edge>? FindPath(
                List<EntityForeignKeyGraph.Edge> edges,
                INamedTypeSymbol from,
                INamedTypeSymbol to)
            {
                var adjacency = new Dictionary<INamedTypeSymbol, List<(EntityForeignKeyGraph.Edge Edge, bool Forward)>>(
                    SymbolEqualityComparer.Default);

                void AddAdj(INamedTypeSymbol node, EntityForeignKeyGraph.Edge edge, bool forward)
                {
                    if (!adjacency.TryGetValue(node, out var list))
                        adjacency[node] = list = new();
                    list.Add((edge, forward));
                }

                foreach (var e in edges)
                {
                    AddAdj(e.DependentEntity, e, true);   // dependent -> principal
                    AddAdj(e.PrincipalEntity, e, false);  // principal -> dependent
                }

                var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { from };
                var queue = new Queue<(INamedTypeSymbol Node, List<EntityForeignKeyGraph.Edge> Path)>();
                queue.Enqueue((from, new()));

                while (queue.Count > 0)
                {
                    var (node, path) = queue.Dequeue();
                    if (SymbolEqualityComparer.Default.Equals(node, to))
                        return path;

                    if (!adjacency.TryGetValue(node, out var neighbors)) continue;

                    foreach (var (edge, forward) in neighbors)
                    {
                        var next = forward ? edge.PrincipalEntity : edge.DependentEntity;
                        if (visited.Contains(next)) continue;
                        visited.Add(next);
                        queue.Enqueue((next, new List<EntityForeignKeyGraph.Edge>(path) { edge }));
                    }
                }

                return null; // no path found
            }
        }
        
        public static class EntityForeignKeyGraph
        {
            public sealed record Edge(
                INamedTypeSymbol DependentEntity,
                string DependentColumn,
                INamedTypeSymbol PrincipalEntity,
                string PrincipalColumn);

            public static List<Edge> Build(Compilation compilation, CancellationToken ct)
            {
                var edges = new List<Edge>();

                foreach (var tree in compilation.SyntaxTrees)
                {
                    ct.ThrowIfCancellationRequested();
                    var semanticModel = compilation.GetSemanticModel(tree);
                    var root = tree.GetRoot(ct);

                    foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                    {
                        var principalEntity = TryGetConfiguredEntityType(classDecl, semanticModel, ct);
                        if (principalEntity == null) continue;

                        var configure = classDecl.Members.OfType<MethodDeclarationSyntax>()
                            .FirstOrDefault(x => x.Identifier.Text == "Configure" && x.Body != null);
                        if (configure?.Body == null) continue;

                        foreach (var statement in configure.Body.Statements.OfType<ExpressionStatementSyntax>())
                        {
                            if (statement.Expression is not InvocationExpressionSyntax outer) continue;

                            var chain = CollectChain(outer);

                            var hasIndex = chain.FindIndex(x => x.Name is "HasOne" or "HasMany");
                            if (hasIndex < 0) continue;

                            var withIndex = chain.FindIndex(hasIndex + 1, x => x.Name is "WithOne" or "WithMany");
                            if (withIndex < 0) continue;

                            var fkIndex = chain.FindIndex(withIndex + 1, x => x.Name == "HasForeignKey");
                            if (fkIndex < 0) continue;

                            // navName: property on principalEntity, e.g. Customer.ContactPoint
                            var navName = ExtractLambdaMemberName(chain[hasIndex].Invocation);
                            if (navName == null) continue;

                            var navProperty = principalEntity.GetMembers().OfType<IPropertySymbol>()
                                .FirstOrDefault(p => p.Name == navName);

                            var dependentEntity = ResolveRelatedType(navProperty);
                            if (dependentEntity == null) continue;

                            // FK column name, e.g. "CustomerId" -- lambda param is the dependent side
                            // regardless of whether it came via WithOne/WithMany or an explicit
                            // HasForeignKey<TDependent> generic arg; in every config in this codebase
                            // the nav target of HasOne/HasMany IS the dependent/FK-owning side.
                            var fkColumnName = ExtractLambdaMemberName(chain[fkIndex].Invocation);
                            if (fkColumnName == null) continue;

                            var principalPk = dependentEntity.Name == principalEntity.Name
                                ? "Id"
                                : "Id"; // convention: every entity's PK property is "Id" per Process base

                            edges.Add(new Edge(dependentEntity, fkColumnName, principalEntity, principalPk));
                        }
                    }
                }

                return edges;
            }

            private static INamedTypeSymbol? ResolveRelatedType(IPropertySymbol? property)
            {
                if (property == null) return null;
                var type = property.Type;
                if (type is INamedTypeSymbol generic && generic.IsGenericType && generic.TypeArguments.Length == 1)
                    return generic.TypeArguments[0] as INamedTypeSymbol;
                return type as INamedTypeSymbol;
            }

            // CollectChain / ExtractLambdaMemberName / TryGetConfiguredEntityType:
            // identical to FluentEntityNavigationConvention's private helpers —
            // either factor them into a shared internal class both passes call,
            // or duplicate; your call on which.
        }


        private static INamedTypeSymbol? ResolveRelatedType(
            IPropertySymbol? property)
        {
            if (property == null)
                return null;


            var type = property.Type;


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
                        System.StringComparison.OrdinalIgnoreCase));
        }


        private static INamedTypeSymbol? TryGetConfiguredEntityType(
            ClassDeclarationSyntax classDecl,
            SemanticModel semanticModel,
            CancellationToken ct)
        {
            var symbol =
                semanticModel.GetDeclaredSymbol(
                    classDecl,
                    ct) as INamedTypeSymbol;


            if (symbol == null)
                return null;


            foreach (var iface in symbol.AllInterfaces)
            {
                if (iface.OriginalDefinition.Name ==
                    "IEntityTypeConfiguration" &&
                    iface.TypeArguments.Length == 1 &&
                    iface.TypeArguments[0] is INamedTypeSymbol entity)
                {
                    return entity;
                }
            }


            return null;
        }


        private static List<(string Name, InvocationExpressionSyntax Invocation)>
            CollectChain(InvocationExpressionSyntax outer)
        {
            var result =
                new List<(string, InvocationExpressionSyntax)>();

            var current = outer;


            while (current.Expression is MemberAccessExpressionSyntax ma)
            {
                var name =
                    ma.Name is GenericNameSyntax generic
                        ? generic.Identifier.Text
                        : ma.Name.Identifier.Text;


                result.Insert(
                    0,
                    (name, current));


                if (ma.Expression is InvocationExpressionSyntax inner)
                    current = inner;
                else
                    break;
            }


            return result;
        }


        private static string? ExtractLambdaMemberName(
            InvocationExpressionSyntax invocation)
        {
            var arg =
                invocation.ArgumentList.Arguments
                    .FirstOrDefault()
                    ?.Expression;


            if (arg is not SimpleLambdaExpressionSyntax
                {
                    Body: MemberAccessExpressionSyntax member
                })
                return null;


            return member.Name.Identifier.Text;
        }
    }
}