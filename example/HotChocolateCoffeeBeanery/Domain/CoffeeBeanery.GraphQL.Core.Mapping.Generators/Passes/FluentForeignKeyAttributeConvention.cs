using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes
{
    /// <summary>
    /// Scans every IEntityTypeConfiguration&lt;T&gt;.Configure(...) method for
    /// HasOne/HasMany(...).WithOne/WithMany(...).HasForeignKey(...) chains and
    /// derives the equivalent of a hand-written [EntityForeignKey] attribute for
    /// each one, using the same business-key naming convention
    /// EntityNavigationConvention already applies ("{InverseNavName}Key" on the
    /// related side, "{DeclaringEntity.Name}Key" on the principal side).
    ///
    /// Designed to run in the SAME compilation as the entity classes themselves
    /// (e.g. Database.Entity), so the fluent config syntax is actually visible -
    /// solving the cross-project syntax-tree visibility gap that
    /// FluentInverseNavigationConvention hits when run from a different project
    /// (e.g. Domain.Shared) that only sees compiled metadata, not source.
    /// </summary>
    internal static class FluentForeignKeyAttributeConvention
    {
        public record DerivedForeignKey(
            INamedTypeSymbol DeclaringEntityType,
            INamedTypeSymbol RelatedEntityType,
            string ForeignKeyProperty,
            string PrincipalKeyProperty,
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
                    var declaringEntityType = TryGetConfiguredEntityType(classDecl, semanticModel, ct);
                    if (declaringEntityType is null)
                        continue;

                    var configureMethod = classDecl.Members
                        .OfType<MethodDeclarationSyntax>()
                        .FirstOrDefault(m => m.Identifier.Text == "Configure" && m.Body is not null);

                    if (configureMethod?.Body is null)
                        continue;

                    var chainResults = new List<DerivedForeignKey>();

                    foreach (var stmt in configureMethod.Body.Statements.OfType<ExpressionStatementSyntax>())
                    {
                        if (stmt.Expression is not InvocationExpressionSyntax outer)
                            continue;

                        var chain = CollectChain(outer);

                        var hasIdx = chain.FindIndex(c => c.Name is "HasOne" or "HasMany");
                        if (hasIdx < 0) continue;

                        var withIdx = chain.FindIndex(hasIdx + 1, c => c.Name is "WithOne" or "WithMany");
                        if (withIdx < 0) continue;

                        var fkIdx = chain.FindIndex(withIdx + 1, c => c.Name == "HasForeignKey");
                        if (fkIdx < 0) continue; // only chains with an actual FK wiring count

                        var navName = ExtractLambdaMemberName(chain[hasIdx].Invocation);
                        var inverseNavName = ExtractLambdaMemberName(chain[withIdx].Invocation);

                        if (navName is null || inverseNavName is null)
                            continue;

                        var navProp = declaringEntityType.GetMembers()
                            .OfType<IPropertySymbol>()
                            .FirstOrDefault(p => p.Name == navName);

                        var relatedEntityType = ResolveRelatedType(navProp);
                        if (relatedEntityType is null)
                            continue;

                        var foreignKeyProperty = FindScalarSibling(relatedEntityType, inverseNavName + "Key");
                        var principalKeyProperty = FindScalarSibling(declaringEntityType, declaringEntityType.Name + "Key");

                        if (foreignKeyProperty is null || principalKeyProperty is null)
                            continue; // convention didn't resolve - leave for explicit attribute/manual fix

                        chainResults.Add(new DerivedForeignKey(
                            declaringEntityType,
                            relatedEntityType,
                            foreignKeyProperty.Name,
                            principalKeyProperty.Name,
                            navName,
                            IsAmbiguous: false));
                    }

                    var ambiguousGroups = chainResults
                        .GroupBy(r => r.RelatedEntityType, SymbolEqualityComparer.Default)
                        .Where(g => g.Count() > 1)
                        .Select(g => (INamedTypeSymbol)g.Key!);

                    var ambiguousGroupsSet = new HashSet<INamedTypeSymbol>(ambiguousGroups, SymbolEqualityComparer.Default);

                    foreach (var r in chainResults)
                    {
                        results.Add(r with { IsAmbiguous = ambiguousGroupsSet.Contains(r.RelatedEntityType) });
                    }
                }
            }

            return results;
        }

        private static INamedTypeSymbol? ResolveRelatedType(IPropertySymbol? navProp)
        {
            if (navProp is null) return null;

            var type = navProp.Type;

            if (type is INamedTypeSymbol { IsGenericType: true } generic &&
                generic.TypeArguments.Length == 1 &&
                generic.Name is "List" or "ICollection" or "IList" or "IEnumerable")
            {
                return generic.TypeArguments[0] as INamedTypeSymbol;
            }

            return type as INamedTypeSymbol;
        }

        private static IPropertySymbol? FindScalarSibling(INamedTypeSymbol type, string name) =>
            type.GetMembers().OfType<IPropertySymbol>()
                .FirstOrDefault(p => string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase));

        private static INamedTypeSymbol? TryGetConfiguredEntityType(
            ClassDeclarationSyntax classDecl, SemanticModel semanticModel, CancellationToken ct)
        {
            if (classDecl.BaseList is null)
                return null;

            if (semanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol classSymbol)
                return null;

            foreach (var iface in classSymbol.AllInterfaces)
            {
                if (iface.OriginalDefinition.Name == "IEntityTypeConfiguration" &&
                    iface.TypeArguments.Length == 1 &&
                    iface.TypeArguments[0] is INamedTypeSymbol entityType)
                {
                    return entityType;
                }
            }

            return null;
        }

        private static List<(string Name, InvocationExpressionSyntax Invocation)> CollectChain(
            InvocationExpressionSyntax outer)
        {
            var chain = new List<(string, InvocationExpressionSyntax)>();
            var current = outer;

            while (current.Expression is MemberAccessExpressionSyntax ma)
            {
                var name = ma.Name is GenericNameSyntax g ? g.Identifier.Text : ma.Name.Identifier.Text;
                chain.Insert(0, (name, current));

                if (ma.Expression is InvocationExpressionSyntax inner)
                    current = inner;
                else
                    break;
            }

            return chain;
        }

        private static string? ExtractLambdaMemberName(InvocationExpressionSyntax invocation)
        {
            var arg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (arg is not SimpleLambdaExpressionSyntax { Body: MemberAccessExpressionSyntax memberAccess })
                return null;

            return memberAccess.Name.Identifier.Text;
        }
    }
}