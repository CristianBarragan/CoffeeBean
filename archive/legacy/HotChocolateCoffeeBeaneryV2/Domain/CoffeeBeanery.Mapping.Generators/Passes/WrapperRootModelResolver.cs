using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes
{
    internal static class WrapperRootModelResolver
    {
        public static ImmutableHashSet<INamedTypeSymbol> Resolve(Compilation compilation, CancellationToken ct)
        {
            var wrapperType = FindWrapperType(compilation, ct);
            if (wrapperType is null)
                return ImmutableHashSet.Create<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var prop in wrapperType.GetMembers().OfType<IPropertySymbol>())
            {
                if (prop.IsStatic || prop.GetMethod is null)
                    continue;

                var elementType = UnwrapCollection(prop.Type);

                if (elementType is not INamedTypeSymbol named)
                    continue;

                if (named.SpecialType == SpecialType.System_String)
                    continue;

                if (named.TypeKind == TypeKind.Enum)
                    continue;

                builder.Add(named);
            }

            return builder.ToImmutable();
        }

        private static INamedTypeSymbol? FindWrapperType(Compilation compilation, CancellationToken ct)
        {
            foreach (var tree in compilation.SyntaxTrees)
            {
                ct.ThrowIfCancellationRequested();

                var semanticModel = compilation.GetSemanticModel(tree);
                var root = tree.GetRoot(ct);

                foreach (var classDecl in root.DescendantNodes()
                             .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>())
                {
                    if (classDecl.Identifier.Text != "Wrapper")
                        continue;

                    if (semanticModel.GetDeclaredSymbol(classDecl, ct) is INamedTypeSymbol symbol)
                        return symbol;
                }
            }

            return null;
        }

        private static ITypeSymbol UnwrapCollection(ITypeSymbol type)
        {
            if (type.SpecialType == SpecialType.System_String)
                return type;

            if (type is INamedTypeSymbol { IsGenericType: true } named)
            {
                var defName = named.OriginalDefinition.ToDisplayString();

                if (defName is "System.Collections.Generic.List<T>"
                    or "System.Collections.Generic.IEnumerable<T>"
                    or "System.Collections.Generic.ICollection<T>"
                    or "System.Collections.Generic.IList<T>")
                {
                    return named.TypeArguments[0];
                }
            }

            return type;
        }
    }
}