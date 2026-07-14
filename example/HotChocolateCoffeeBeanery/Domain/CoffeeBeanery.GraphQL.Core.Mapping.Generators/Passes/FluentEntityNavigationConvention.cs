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
            string RawForeignKeyColumn,
            string RawPrincipalKeyColumn,
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


                        var withIndex = chain.FindIndex(hasIndex + 1, x => x.Name is "WithOne" or "WithMany");
                        if (withIndex < 0) continue;

                        var fkIndex = chain.FindIndex(withIndex + 1, x => x.Name == "HasForeignKey");
                        if (fkIndex < 0) continue;

                        var navName = ExtractLambdaMemberName(chain[hasIndex].Invocation);
                        var inverseName = ExtractLambdaMemberName(chain[withIndex].Invocation);
                        var rawFkColumn = ExtractLambdaMemberName(chain[fkIndex].Invocation);

                        if (navName == null || inverseName == null || rawFkColumn == null)
                            continue;

                        var navProperty = entityType.GetMembers().OfType<IPropertySymbol>()
                            .FirstOrDefault(x => x.Name == navName);
                        var relatedEntity = ResolveRelatedType(navProperty);
                        if (relatedEntity == null) continue;
                        
                        var foreignKey = FindProperty(relatedEntity, inverseName + "Key");
                        var principalKey = FindProperty(entityType, entityType.Name + "Key");

                        entityResults.Add(new DerivedForeignKey(
                            entityType,
                            relatedEntity,
                            foreignKey?.Name ?? rawFkColumn,
                            principalKey?.Name ?? "Id",
                            rawFkColumn,
                            "Id",
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

        var attributeType = compilation.GetTypeByMetadataName(
            "CoffeeBeanery.GraphQL.Core.Mapping.EntityForeignKeyAttribute");

        if (attributeType == null)
            return edges; // attribute type not visible yet — nothing to read

        // The [EntityForeignKey] attributes are emitted onto partial declarations
        // of the DEPENDENT entity (the one that owns the FK column), one attribute
        // per relationship it participates in. Walk every named type visible in
        // this compilation (including referenced assemblies) looking for them.
        foreach (var candidate in GetAllNamedTypes(compilation.GlobalNamespace))
        {
            ct.ThrowIfCancellationRequested();

            foreach (var attr in candidate.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType))
                    continue;

                if (attr.ConstructorArguments.Length < 5)
                    continue;

                if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol relatedEntity)
                    continue;

                var rawFkColumn = attr.ConstructorArguments[3].Value as string;
                var rawPkColumn = attr.ConstructorArguments[4].Value as string;

                if (string.IsNullOrWhiteSpace(rawFkColumn) || string.IsNullOrWhiteSpace(rawPkColumn))
                    continue;
                
                edges.Add(new Edge(relatedEntity, rawFkColumn!, candidate, rawPkColumn!));
            }
        }

        return edges;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol root)
    {
        foreach (var member in root.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol ns:
                    foreach (var t in GetAllNamedTypes(ns))
                        yield return t;
                    break;
                case INamedTypeSymbol type:
                    yield return type;
                    foreach (var nested in type.GetTypeMembers())
                        yield return nested;
                    break;
            }
        }
    }
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