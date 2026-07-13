using System;
using System.Collections.Generic;
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

            var isMappingRoot = context.AnalyzerConfigOptionsProvider
                .Select(static (opts, _) =>
                    opts.GlobalOptions.TryGetValue("build_property.IsMappingRoot", out var v)
                    && v?.Trim().ToLowerInvariant() == "true");

            var mappingClasses = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, ct) => TryGetMappingClass(ctx, ct))
                .Where(static info => info is not null)
                .Select(static (info, _) => info!);

            var rawAllMappings = mappingClasses.Collect();

            var rootModelTypes = context.CompilationProvider
                .Select(static (compilation, ct) => WrapperRootModelResolver.Resolve(compilation, ct));

            var entityGraphs = context.CompilationProvider
                .Select(static (compilation, ct) => FluentEntityNavigationConvention.EntityForeignKeyGraph.Build(compilation, ct));

            var allMappings = rawAllMappings.Select(static (mappings, _) =>
            {
                foreach (var info in mappings)
                {
                    ModelChildrenInference.Apply(info);
                    CompositeChildAttachmentConvention.Apply(info, mappings);
                }

                return mappings;
            });

            // ----------------------------------------------------------------
            // Per-class registration — one file per mapping class, runs in every project.
            // ----------------------------------------------------------------
            var perClassInput = mappingClasses
                .Combine(allMappings)
                .Combine(rootModelTypes)
                .Combine(entityGraphs);

            context.RegisterSourceOutput(perClassInput, static (spc, data) =>
            {
                var (((info, all), rootModelTypes), entityGraph) = data;
                EmitClass(spc, info, all, rootModelTypes, entityGraph);
            });

            // ----------------------------------------------------------------
            // Global emitters — only the IsMappingRoot project emits these,
            // and only ONCE per compilation (not once per mapping class).
            // ----------------------------------------------------------------
            var globalInput = allMappings
                .Combine(rootModelTypes)
                .Combine(entityGraphs)
                .Combine(isMappingRoot);

            context.RegisterSourceOutput(globalInput, static (spc, data) =>
            {
                var (((all, rootModelTypes), entityGraphs), isRoot) = data;

                if (!isRoot || all.IsEmpty)
                    return;

                EmitGlobal(spc, all, rootModelTypes, entityGraphs);
            });
        }

        private static MappingClassInfo? TryGetMappingClass(
            GeneratorSyntaxContext ctx,
            CancellationToken ct)
        {
            var classDecl = (ClassDeclarationSyntax)ctx.Node;

            var symbol =
                ctx.SemanticModel.GetDeclaredSymbol(classDecl, ct)
                    as INamedTypeSymbol;

            if (symbol is null || symbol.IsAbstract)
                return null;

            var mappingInterface =
                ctx.SemanticModel.Compilation
                    .GetTypeByMetadataName(
                        "CoffeeBeanery.GraphQL.Core.Mapping.IMappingDefinition");

            if (mappingInterface is null)
                return null;

            if (!symbol.AllInterfaces.Contains(
                    mappingInterface,
                    SymbolEqualityComparer.Default))
            {
                return null;
            }

            return MappingClassParser.Parse(
                symbol,
                ctx.SemanticModel,
                ct);
        }

        private static void EmitClass(
            SourceProductionContext spc,
            MappingClassInfo info,
            ImmutableArray<MappingClassInfo> allMappings,
            ImmutableHashSet<INamedTypeSymbol> rootModelTypes,
            List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph)
        {
            try
            {
                foreach (var d in info.Diagnostics)
                    spc.ReportDiagnostic(d);

                if (info.Diagnostics.Any(x => x.Severity == DiagnosticSeverity.Error))
                    return;

                FieldMapGeneration.Apply(info, spc);

                var rootEntityTypes = ResolveRootEntityTypes(allMappings, rootModelTypes);

                var navResult = EntityNavigationConvention.Resolve(info, allMappings, entityGraph, rootEntityTypes);

                if (navResult.HasBlockingAmbiguity)
                    return;

            }
            catch (Exception ex)
            {
                // Surface the real crash as a compiler error instead of silence
                spc.ReportDiagnostic(Diagnostic.Create(
                    MappingDiagnostics.GeneratorCrashDescriptor,
                    Location.None,
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace?.Replace("\r\n", " ").Replace("\n", " ") ?? ""));

                // Also emit a poisoned file so downstream "type not found" errors
                // don't mask the real problem
                spc.AddSource("GeneratorCrash.g.cs", $@"
                // <auto-generated/>
                // GENERATOR CRASHED — see CBM000 diagnostic for details
                #error CBM000: Source generator crashed: {ex.GetType().Name}: {ex.Message}
                ");
            }
        }

        private static void EmitGlobal(
            SourceProductionContext spc,
            ImmutableArray<MappingClassInfo> allMappings,
            ImmutableHashSet<INamedTypeSymbol> rootModelTypes,
            List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph)
        {
            try
            {
                var rootEntityTypes = ResolveRootEntityTypes(allMappings, rootModelTypes);

                spc.AddSource("Materializers.g.cs", MaterializerEmitter.Emit(allMappings, rootEntityTypes, entityGraph));

                spc.AddSource("GeneratedIds.g.cs", IdEmitter.Emit(allMappings, spc));

                spc.AddSource("EntityMeta.g.cs", MetadataEmitter.Emit(allMappings, rootEntityTypes, entityGraph));

                var source = AdapterEmitter.Emit(allMappings, rootEntityTypes, entityGraph);
                spc.AddSource("AdapterTables.g.cs", source);

                spc.AddSource("Planners.g.cs",
                    PlannerEmitter.Emit(allMappings, rootEntityTypes, entityGraph));
            }
            catch (Exception ex)
            {
                // Surface the real crash as a compiler error instead of silence
                spc.ReportDiagnostic(Diagnostic.Create(
                    MappingDiagnostics.GeneratorCrashDescriptor,
                    Location.None,
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace?.Replace("\r\n", " ").Replace("\n", " ") ?? ""));

                // Also emit a poisoned file so downstream "type not found" errors
                // don't mask the real problem
                spc.AddSource("GeneratorCrash.g.cs", $@"
                // <auto-generated/>
                // GENERATOR CRASHED — see CBM000 diagnostic for details
                #error CBM000: Source generator crashed: {ex.GetType().Name}: {ex.Message}
                ");
            }
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