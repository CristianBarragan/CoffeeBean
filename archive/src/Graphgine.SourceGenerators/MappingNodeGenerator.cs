using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Graphgine.SourceGenerators.Model;
using Graphgine.SourceGenerators.Passes;
using Graphgine.SourceGenerators.Emit;
using Graphgine.SourceGenerators.Parsing;
using Microsoft.CodeAnalysis.Text;

namespace Graphgine.SourceGenerators
{
    [Generator(LanguageNames.CSharp)]
    public sealed class MappingNodeGenerator : IIncrementalGenerator
    {
        public void Initialize(
            IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(
                context.CompilationProvider,
                static (spc, compilation) =>
                {
                    var assembly = compilation.AssemblyName;

                    spc.ReportDiagnostic(
                        Diagnostic.Create(
                            MappingDiagnostics.EntityGraphDebug,
                            Location.None,
                            $"Generator running for {assembly}"
                        ));
                });
            
            
            var mappingClasses =
                context.SyntaxProvider
                    .CreateSyntaxProvider(
                        predicate: static (node, _) =>
                            node is ClassDeclarationSyntax,

                        transform: static (ctx, ct) =>
                            TryGetMappingClass(ctx, ct))

                    .Where(static info => info is not null)
                    .Select(static (info, _) => info!);

            var rawAllMappings =
                mappingClasses.Collect();


            var rootModelTypes =
                context.CompilationProvider
                    .Select(static (compilation, ct) =>
                        WrapperRootModelResolver.Resolve(
                            compilation,
                            ct));


            var entityGraphResults =
                context.CompilationProvider
                    .Select(static (compilation, ct) =>
                        FluentEntityNavigationConvention
                            .EntityForeignKeyGraph
                            .Build(compilation, ct));

            var entityGraphs =
                entityGraphResults
                    .Select(static (result, _) => result.Edges);

            context.RegisterSourceOutput(
                entityGraphResults,
                static (spc, result) =>
                {
                    foreach (var message in result.Diagnostics)
                    {
                        spc.ReportDiagnostic(
                            Diagnostic.Create(
                                MappingDiagnostics.EntityGraphDebug,
                                Location.None,
                                message));
                    }
                });


            var allMappings =
                rawAllMappings
                    .Combine(entityGraphs)
                    .Select(static (pair, ct) =>
                    {
                        var (mappings, entityGraph) = pair;

                        foreach (var info in mappings)
                        {
                            ModelChildrenInference.Apply(
                                info,
                                mappings);

                            CompositeChildAttachmentConvention.Apply(
                                info,
                                mappings,
                                entityGraph);

                            EntityGraphChildrenInference.Apply(
                                info,
                                mappings,
                                entityGraph);
                        }

                        return mappings;
                    });


            var perClassInput =
                mappingClasses
                    .Combine(allMappings)
                    .Combine(rootModelTypes)
                    .Combine(entityGraphs);

            context.RegisterSourceOutput(
                perClassInput,
                static (spc, data) =>
                {
                    var (((info, all), rootModelTypes), entityGraph) =
                        data;

                    foreach (var edge in entityGraph)
                    {
                        spc.ReportDiagnostic(
                            Diagnostic.Create(
                                MappingDiagnostics.EntityGraphDebug,
                                Location.None,
                                $"{edge.PrincipalEntity.Name}.{edge.PrincipalColumn} -> {edge.DependentEntity.Name}.{edge.DependentColumn}"
                            ));
                    }
                    
                    EmitClass(
                        spc,
                        info,
                        all,
                        rootModelTypes,
                        entityGraph);
                });


            var globalInput =
                allMappings
                    .Combine(rootModelTypes)
                    .Combine(entityGraphs);

            context.RegisterPostInitializationOutput(static ctx =>
            {
                ctx.AddSource(
                    "GeneratorLoaded.g.cs",
                    """
                    // <auto-generated/>

                    namespace Graphgine.Execution;

                    public static class GeneratorLoaded
                    {
                        public const bool Value = true;
                    }
                    """);
            });

            context.RegisterSourceOutput(
                globalInput,
                static (spc, data) =>
                {
                    var ((all, rootModelTypes), entityGraph) =
                        data;


                    if (all.IsEmpty)
                        return;


                    EmitGlobal(
                        spc,
                        all,
                        rootModelTypes,
                        entityGraph);
                });
        }


        private static MappingClassInfo? TryGetMappingClass(
            GeneratorSyntaxContext ctx,
            CancellationToken ct)
        {
            var classDecl =
                (ClassDeclarationSyntax)ctx.Node;


            var symbol =
                ctx.SemanticModel.GetDeclaredSymbol(
                    classDecl,
                    ct)
                as INamedTypeSymbol;


            if (symbol is null ||
                symbol.IsAbstract)
            {
                return null;
            }


            var mappingInterface =
                ctx.SemanticModel.Compilation
                    .GetTypeByMetadataName(
                        "Graphgine.Mapping.IMappingDefinition");


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
                if (info.ModelType == null)
                    return;


                foreach (var diagnostic in info.Diagnostics)
                {
                    spc.ReportDiagnostic(diagnostic);
                }


                if (info.Diagnostics.Any(
                        x => x.Severity == DiagnosticSeverity.Error))
                {
                    return;
                }


                FieldMapGeneration.Apply(
                    info,
                    spc);


                var rootEntityTypes =
                    ResolveRootEntityTypes(
                        allMappings,
                        rootModelTypes);


                var navResult =
                    EntityNavigationConvention.Resolve(
                        info,
                        allMappings,
                        entityGraph,
                        rootEntityTypes);


                var navResults =
                    new Dictionary<string, NavigationResolutionResult?>(
                        StringComparer.Ordinal);


                foreach (var model in allMappings)
                {
                    if (model.ModelType == null)
                        continue;


                    navResults[model.ModelType.Name] =
                        EntityNavigationConvention.Resolve(
                            model,
                            allMappings,
                            entityGraph,
                            rootEntityTypes);
                }

                var childLinks =
                    PlannerEmitter.ComputeChildLinks(
                        info,
                        allMappings,
                        navResults,
                        entityGraph);

                spc.ReportDiagnostic(
                    Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "CBM001",
                            "Debug",
                            "{0}",
                            "Mapping",
                            DiagnosticSeverity.Info,
                            true),
                        Location.None,
                        $"Mapping={info.ModelType.Name}, " +
                        $"ChildLinks={string.Join(",",
                            childLinks.Select(
                                x => x.ChildModelName))}"));


                if (navResult.HasBlockingAmbiguity)
                    return;
            }
            catch (Exception ex)
            {
                ReportCrash(
                    spc,
                    ex);
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
                spc.AddSource(
                    "GeneratorHeartbeat.g.cs",
                    """
                    // <auto-generated/>

                    namespace Graphgine.Execution;

                    public static class GeneratorHeartbeat
                    {
                        public const bool Running = true;
                    }
                    """);

                if (allMappings.IsEmpty)
                    return;

                var resolvedMappings = allMappings
                    .Select(info =>
                    {
                        var copy = info.Clone();
                        FieldMapGeneration.ApplyWithoutDiagnostics(copy);
                        return copy;
                    })
                    .ToImmutableArray();

                foreach (var info in resolvedMappings)
                {
                    ModelChildrenInference.Apply(
                        info,
                        resolvedMappings);

                    CompositeChildAttachmentConvention.Apply(
                        info,
                        resolvedMappings,
                        entityGraph);

                    EntityGraphChildrenInference.Apply(
                        info,
                        resolvedMappings,
                        entityGraph);
                }

                var rootEntityTypes =
                    ResolveRootEntityTypes(resolvedMappings, rootModelTypes);

                foreach (var model in resolvedMappings)
                {
                    if (model.ModelType == null)
                        continue;

                    EntityNavigationConvention.Resolve(
                        model, resolvedMappings, entityGraph, rootEntityTypes);
                }

                var ids =
                    IdEmitter.Emit(
                        resolvedMappings, entityGraph);

                spc.AddSource(
                    "GeneratedIds.g.cs",
                    SourceText.From(ids, Encoding.UTF8));
                
                
                spc.AddSource("ColumnNameResolver.g.cs", ColumnNameResolverEmitter.Emit(resolvedMappings));

                spc.AddSource("EntityMeta.g.cs",
                    MetadataEmitter.Emit(resolvedMappings, rootEntityTypes, entityGraph));

                spc.AddSource("MutationMetadataRegistry.g.cs",
                    MutationMetadataEmitter.Emit(resolvedMappings, rootEntityTypes, entityGraph));

                // FIXED (compile break): MutationMaterializerEmitter.Emit
                // now requires entityGraph — its EmitDematerializer calls
                // ColumnIdResolver.Resolve, which needs it to compute
                // column indices via GetFullColumnOrder (same fix as
                // PlannerEmitter/MutationMetadataEmitter).
                spc.AddSource("MutationMaterializers.g.cs",
                    MutationMaterializerEmitter.Emit(resolvedMappings, entityGraph));

                // FIXED (compile break): QueryMaterializerEmitter.Emit
                // now requires entityGraph for the same reason — its
                // EmitRowMaterializer calls ColumnIdResolver.Resolve to
                // build the columnMap[columnId] lookups that read values
                // back out of a DbDataReader.
                spc.AddSource("QueryMaterializers.g.cs",
                    QueryMaterializerEmitter.Emit(resolvedMappings, entityGraph));

                spc.AddSource("Planners.g.cs",
                    PlannerEmitter.Emit(resolvedMappings, rootEntityTypes,  entityGraph));

                spc.AddSource("AdapterTables.g.cs",
                    AdapterEmitter.Emit(resolvedMappings, rootEntityTypes, entityGraph));

                spc.ReportDiagnostic(
                    Diagnostic.Create(MappingDiagnostics.EntityGraphDebug, Location.None, entityGraph.Count));
            }
            catch (Exception ex)
            {
                ReportCrash(spc, ex);
            }
        }



        private static void ReportCrash(
            SourceProductionContext spc,
            Exception ex)
        {
            spc.ReportDiagnostic(
                Diagnostic.Create(
                    MappingDiagnostics.GeneratorCrashDescriptor,
                    Location.None,
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace?
                        .Replace("\r\n", " ")
                        .Replace("\n", " ")
                    ?? ""));


            spc.AddSource(
                "GeneratorCrash.g.cs",
                $"""
                // <auto-generated/>

                #error CBM000: Source generator crashed:
                //{ex.GetType().Name}: {ex.Message}
                """);
        }
        
        private static ImmutableHashSet<INamedTypeSymbol> ResolveRootEntityTypes(
            ImmutableArray<MappingClassInfo> allMappings,
            ImmutableHashSet<INamedTypeSymbol> rootModelTypes)
        {
            if (rootModelTypes.IsEmpty)
            {
                return ImmutableHashSet.Create<INamedTypeSymbol>(
                    SymbolEqualityComparer.Default);
            }


            var builder =
                ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(
                    SymbolEqualityComparer.Default);



            foreach (var mapping in allMappings)
            {
                if (mapping.EntityType == null)
                    continue;


                if (mapping.ModelType != null &&
                    rootModelTypes.Contains(mapping.ModelType))
                {
                    builder.Add(mapping.EntityType);
                }
            }



            return builder.ToImmutable();
        }
    }
}