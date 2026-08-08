using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Graphgine.SourceGenerators.Passes
{
    /// <summary>
    /// Reads Entity fluent configuration and extracts:
    ///
    /// Entity + NavigationProperty -> InverseNavigationProperty
    ///
    /// Used by Model navigation resolution to disambiguate
    /// multiple navigations to the same Entity type.
    ///
    /// Example:
    ///
    /// CustomerCustomerRelationship:
    ///
    /// HasOne(x => x.InnerCustomer)
    ///     .WithMany(x => x.InnerCustomerRelationships)
    ///
    /// Produces:
    ///
    /// (CustomerCustomerRelationship, InnerCustomer)
    ///     -> InnerCustomerRelationships
    ///
    /// Roslyn only.
    /// No reflection.
    /// No EF runtime model.
    /// </summary>
    internal static class FluentInverseNavigationConvention
    {
        public static ImmutableDictionary<(INamedTypeSymbol Entity, string NavigationName), string>
            CollectAll(
                Compilation compilation,
                CancellationToken ct)
        {
            var builder =
                ImmutableDictionary.CreateBuilder<
                    (INamedTypeSymbol Entity, string NavigationName),
                    string>(
                        FluentKeyComparer.Instance);


            foreach (var tree in compilation.SyntaxTrees)
            {
                ct.ThrowIfCancellationRequested();


                var semanticModel =
                    compilation.GetSemanticModel(tree);


                var root =
                    tree.GetRoot(ct);


                foreach (var classDecl in root
                             .DescendantNodes()
                             .OfType<ClassDeclarationSyntax>())
                {
                    ct.ThrowIfCancellationRequested();


                    var entityType =
                        TryGetConfiguredEntityType(
                            classDecl,
                            semanticModel,
                            ct);


                    if (entityType is null)
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
                                x =>
                                    x.Name is "WithOne" or "WithMany");


                        if (withIndex < 0)
                            continue;


                        var navigationName =
                            ExtractLambdaMemberName(
                                chain[hasIndex].Invocation);


                        var inverseName =
                            ExtractLambdaMemberName(
                                chain[withIndex].Invocation);


                        if (navigationName == null ||
                            inverseName == null)
                            continue;


                        builder[
                            (
                                entityType,
                                navigationName
                            )
                        ] = inverseName;
                    }
                }
            }


            return builder.ToImmutable();
        }


        private static INamedTypeSymbol? TryGetConfiguredEntityType(
            ClassDeclarationSyntax classDecl,
            SemanticModel semanticModel,
            CancellationToken ct)
        {
            if (semanticModel.GetDeclaredSymbol(
                    classDecl,
                    ct) is not INamedTypeSymbol symbol)
            {
                return null;
            }


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
            CollectChain(
                InvocationExpressionSyntax outer)
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
                    (
                        name,
                        current
                    ));


                if (ma.Expression is InvocationExpressionSyntax inner)
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
            var arg =
                invocation.ArgumentList.Arguments
                    .FirstOrDefault()
                    ?.Expression;


            if (arg is not SimpleLambdaExpressionSyntax
                {
                    Body: MemberAccessExpressionSyntax member
                })
            {
                return null;
            }


            return member.Name.Identifier.Text;
        }


        private sealed class FluentKeyComparer :
            IEqualityComparer<(INamedTypeSymbol Entity, string NavigationName)>
        {
            public static readonly FluentKeyComparer Instance = new();


            public bool Equals(
                (INamedTypeSymbol Entity, string NavigationName) x,
                (INamedTypeSymbol Entity, string NavigationName) y)
            {
                return SymbolEqualityComparer.Default.Equals(
                           x.Entity,
                           y.Entity)
                       &&
                       x.NavigationName == y.NavigationName;
            }


            public int GetHashCode(
                (INamedTypeSymbol Entity, string NavigationName) obj)
            {
                return
                    SymbolEqualityComparer.Default.GetHashCode(obj.Entity)
                    * 397
                    ^
                    obj.NavigationName.GetHashCode();
            }
        }
    }
}