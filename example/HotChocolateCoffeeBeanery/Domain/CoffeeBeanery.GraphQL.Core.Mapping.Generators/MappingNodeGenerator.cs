using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Emit;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Parsing;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators
{
    [Generator(LanguageNames.CSharp)]
    public sealed class MappingNodeGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static ctx =>
            {
                ctx.AddSource("EntityForeignKeyAttribute.g.cs", EntityForeignKeyAttributeSourceText.Value);
            });

            // Set <IsMappingRoot>true</IsMappingRoot> in the ONE .csproj that owns
            // all mapping classes (e.g. Domain.Shared). That project emits the global
            // files (GeneratedIds, EntityMeta, AdapterTables, Planners). Every other
            // project that references the generator as an Analyzer only gets per-class
            // MappingRegistration files.
            // Also add to that same .csproj:
            //   <ItemGroup>
            //     <CompilerVisibleProperty Include="IsMappingRoot" />
            //   </ItemGroup>
            var isMappingRoot = context.AnalyzerConfigOptionsProvider
                .Select(static (opts, _) =>
                    opts.GlobalOptions.TryGetValue("build_property.IsMappingRoot", out var v)
                    && v?.Trim().ToLowerInvariant() == "true");

            var mappingClasses = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                    transform: static (ctx, ct) => TryGetMappingClass(ctx, ct))
                .Where(static info => info is not null)
                .Select(static (info, _) => info!);

            var allMappings = mappingClasses.Collect();

            var rootModelTypes = context.CompilationProvider
                .Select(static (compilation, ct) => WrapperRootModelResolver.Resolve(compilation, ct));

            var fluentInverseNavigations = context.CompilationProvider
                .Select(static (compilation, ct) => FluentInverseNavigationConvention.CollectAll(compilation, ct));

            // ----------------------------------------------------------------
            // Per-class registration — one file per mapping class, runs in every project.
            // ----------------------------------------------------------------
            var perClassInput = mappingClasses
                .Combine(allMappings)
                .Combine(rootModelTypes)
                .Combine(fluentInverseNavigations);

            context.RegisterSourceOutput(perClassInput, static (spc, data) =>
            {
                var (((info, all), rootModelTypes), fluentInverseNav) = data;
                Emit(spc, info, all, rootModelTypes, fluentInverseNav);
            });

            // ----------------------------------------------------------------
            // Global emitters — only the IsMappingRoot project emits these.
            // Combining everything into one RegisterSourceOutput avoids duplicate
            // file conflicts when the generator runs across multiple projects.
            // ----------------------------------------------------------------
            var globalInput = allMappings
                .Combine(rootModelTypes)
                .Combine(fluentInverseNavigations)
                .Combine(isMappingRoot);

            context.RegisterSourceOutput(globalInput, static (spc, data) =>
            {
                var (((all, rootModelTypes), fluentInverseNav), isRoot) = data;

                if (!isRoot || all.IsEmpty)
                    return;

                // GeneratedIds.g.cs — EntityId.*, ColumnId.*, FieldId.*
                spc.AddSource("GeneratedIds.g.cs", IdEmitter.Emit(all));

                // EntityMeta.g.cs — Schema[], Table[], ColumnName[][], FieldName[][]
                spc.AddSource("EntityMeta.g.cs", MetadataEmitter.Emit(all));

                // AdapterTables.g.cs
                var rootEntityTypes = ResolveRootEntityTypes(all, rootModelTypes);
                var source = AdapterEmitter.Emit(all, rootEntityTypes, fluentInverseNav);
                spc.AddSource("AdapterTables.g.cs", source);

                // Planners.g.cs — *Planner classes + PlannerRegistry
                spc.AddSource("Planners.g.cs",
                    PlannerEmitter.Emit(all, rootEntityTypes, fluentInverseNav));
            });
        }

        private static MappingClassInfo? TryGetMappingClass(GeneratorSyntaxContext ctx, CancellationToken ct)
        {
            var classDecl = (ClassDeclarationSyntax)ctx.Node;
            var symbol = ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct) as INamedTypeSymbol;
            if (symbol is null || symbol.IsAbstract)
                return null;

            INamedTypeSymbol? baseType = null;
            for (var current = symbol.BaseType; current is not null; current = current.BaseType)
            {
                if (current.OriginalDefinition.Name is "BaseMappingRegistration" or "BaseModelMappingRegistration")
                {
                    baseType = current;
                    break;
                }
            }

            if (baseType is null)
                return null;

            if (baseType.TypeArguments.Length != 1 || baseType.TypeArguments[0] is not INamedTypeSymbol modelType)
                return null;

            var buildMap = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == "BuildMap");

            if (buildMap is null)
                return null;

            return MappingClassParser.Parse(symbol, modelType, buildMap, ctx.SemanticModel, ct);
        }

        private static void Emit(
            SourceProductionContext spc,
            MappingClassInfo info,
            ImmutableArray<MappingClassInfo> allMappings,
            ImmutableHashSet<INamedTypeSymbol> rootModelTypes,
            ImmutableDictionary<(INamedTypeSymbol, string), string> fluentInverseNav)
        {
            foreach (var d in info.Diagnostics)
                spc.ReportDiagnostic(d);

            if (info.Diagnostics.Any(x => x.Severity == DiagnosticSeverity.Error))
                return;

            ModelChildrenInference.Apply(info);
            CompositeChildAttachmentConvention.Apply(info, allMappings);
            FieldMapGeneration.Apply(info, spc);

            var rootEntityTypes = ResolveRootEntityTypes(allMappings, rootModelTypes);

            var navResult = EntityNavigationConvention.Resolve(info, spc, rootEntityTypes, fluentInverseNav);

            if (navResult.HasBlockingAmbiguity)
                return;

            var source = NodeTreeEmitter.EmitRegisterOverride(info, navResult);
            spc.AddSource($"{info.ClassName}.MappingRegistration.g.cs", source);
        }

        private static ImmutableHashSet<INamedTypeSymbol> ResolveRootEntityTypes(
            ImmutableArray<MappingClassInfo> allMappings,
            ImmutableHashSet<INamedTypeSymbol> rootModelTypes)
        {
            if (rootModelTypes.IsEmpty)
                return ImmutableHashSet.Create<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var mapping in allMappings)
            {
                if (mapping.EntityType is null)
                    continue;

                if (rootModelTypes.Contains(mapping.ModelType))
                    builder.Add(mapping.EntityType);
            }

            return builder.ToImmutable();
        }
    }
}