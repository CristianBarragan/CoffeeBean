using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes
{
    /// <summary>
    /// Scans every IEntityTypeConfiguration&lt;T&gt;.Configure(...) method in the
    /// compilation for HasOne(...)/HasMany(...).WithOne(...)/WithMany(...) chains,
    /// extracting just the (DeclaringEntity, NavigationPropertyName) -> InverseNavigationPropertyName
    /// pairing. Deliberately does NOT read HasForeignKey(...) - this codebase's
    /// fluent config FKs reference surrogate int Id columns (AccountId, CustomerId),
    /// not the Guid business keys (AccountKey, CustomerKey) used throughout the rest
    /// of the mapping/join layer. Pulling those in would silently break the
    /// business-key join convention everywhere else.
    ///
    /// Used solely to disambiguate multiple navigations from the same entity to the
    /// same related type (e.g. Customer.OuterCustomerCustomerRelationship vs
    /// InnerCustomerCustomerRelationship), by feeding the inverse nav name into
    /// EntityNavigationConvention's "{InverseNavName}Key" sibling lookup.
    /// </summary>
    internal static class FluentInverseNavigationConvention
    {
        public static ImmutableDictionary<(INamedTypeSymbol Entity, string NavigationName), string> CollectAll(
            Compilation compilation, CancellationToken ct)
        {
            var builder = ImmutableDictionary.CreateBuilder<(INamedTypeSymbol, string), string>(
                FluentKeyComparer.Instance);

            foreach (var tree in compilation.SyntaxTrees)
            {
                ct.ThrowIfCancellationRequested();
                var semanticModel = compilation.GetSemanticModel(tree);
                var root = tree.GetRoot(ct);

                foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    var entityType = TryGetConfiguredEntityType(classDecl, semanticModel, ct);
                    if (entityType is null)
                        continue;

                    var configureMethod = classDecl.Members
                        .OfType<MethodDeclarationSyntax>()
                        .FirstOrDefault(m => m.Identifier.Text == "Configure" && m.Body is not null);

                    if (configureMethod?.Body is null)
                        continue;

                    foreach (var stmt in configureMethod.Body.Statements.OfType<ExpressionStatementSyntax>())
                    {
                        if (stmt.Expression is not InvocationExpressionSyntax outer)
                            continue;

                        var chain = CollectChain(outer);

                        var hasIdx = chain.FindIndex(c => c.Name is "HasOne" or "HasMany");
                        if (hasIdx < 0) continue;

                        var withIdx = chain.FindIndex(hasIdx + 1, c => c.Name is "WithOne" or "WithMany");
                        if (withIdx < 0) continue;

                        var navName = ExtractLambdaMemberName(chain[hasIdx].Invocation);
                        var inverseNavName = ExtractLambdaMemberName(chain[withIdx].Invocation);

                        if (navName is null || inverseNavName is null)
                            continue;

                        builder[(entityType, navName)] = inverseNavName;
                    }
                }
            }

            return builder.ToImmutable();
        }

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

        // Walks a fluent chain from its outermost invocation back to the root
        // (e.g. "builder"), returning [(CallName, Invocation), ...] in call order.
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

        private sealed class FluentKeyComparer : IEqualityComparer<(INamedTypeSymbol Entity, string NavigationName)>
        {
            public static readonly FluentKeyComparer Instance = new();

            public bool Equals((INamedTypeSymbol Entity, string NavigationName) x, (INamedTypeSymbol Entity, string NavigationName) y) =>
                SymbolEqualityComparer.Default.Equals(x.Entity, y.Entity) &&
                x.NavigationName == y.NavigationName;

            public int GetHashCode((INamedTypeSymbol Entity, string NavigationName) obj) =>
                SymbolEqualityComparer.Default.GetHashCode(obj.Entity) * 397 ^ obj.NavigationName.GetHashCode();
        }
    }
}