using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Graphgine.SourceGenerators.Passes;

namespace Graphgine.SourceGenerators
{
    /// <summary>
    /// Runs only in the entity-defining project (Database.Entity).
    ///
    /// Emits assembly-level FK graph metadata.
    /// Attribute types live in Graphgine.Mapping.
    ///
    /// The generator assembly is shared by all mapping generators, so this
    /// generator is gated by the EnableEntityForeignKeyEmitter MSBuild property.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class EntityForeignKeyEmitterGenerator : IIncrementalGenerator
    {
        public void Initialize(
            IncrementalGeneratorInitializationContext context)
        {
            var enabled =
                context.AnalyzerConfigOptionsProvider
                    .Select(
                        static (provider, _) =>
                        {
                            provider.GlobalOptions.TryGetValue(
                                "build_property.EnableEntityForeignKeyEmitter",
                                out var value);

                            return string.Equals(
                                value,
                                "true",
                                StringComparison.OrdinalIgnoreCase);
                        });


            var derivedKeys =
                context.CompilationProvider
                    .Combine(enabled)
                    .Select(
                        static (pair, ct) =>
                        {
                            var compilation = pair.Left;
                            var isEnabled = pair.Right;

                            if (!isEnabled)
                            {
                                return new EntityResult(
                                    compilation.AssemblyName ?? "<unknown>",
                                    ImmutableArray<FluentEntityNavigationConvention.DerivedForeignKey>.Empty,
                                    false,
                                    0);
                            }


                            var keys =
                                FluentEntityNavigationConvention.CollectAll(
                                    compilation,
                                    ct);
                            
                            return new EntityResult(
                                compilation.AssemblyName ?? "<unknown>",
                                keys.ToImmutableArray(),
                                true,
                                keys.Count());
                        });



            var derivedModelKeys =
                context.CompilationProvider
                    .Combine(enabled)
                    .Select(
                        static (pair, ct) =>
                        {
                            var compilation = pair.Left;
                            var isEnabled = pair.Right;

                            if (!isEnabled)
                            {
                                return new ModelResult(
                                    compilation.AssemblyName ?? "<unknown>",
                                    ImmutableDictionary<(INamedTypeSymbol Entity, string NavigationName), string>.Empty,
                                    false);
                            }


                            var keys =
                                FluentInverseNavigationConvention.CollectAll(
                                    compilation,
                                    ct);


                            return new ModelResult(
                                compilation.AssemblyName ?? "<unknown>",
                                keys,
                                true);
                        });



            context.RegisterSourceOutput(
                derivedKeys,
                static (spc, result) =>
                {
                    try
                    {
                        if (!result.Enabled)
                            return;


                        spc.ReportDiagnostic(
                            Diagnostic.Create(
                                MappingDiagnostics.EntityGraphDebug,
                                Location.None,
                                $"Emitter assembly={result.AssemblyName}, keys={result.Keys.Length}"
                            ));


                        if (result.Keys.Length == 0)
                            return;


                        // ---------------------------------------------------------
                        // FIXED: ToDisplayString() on a nullable-annotated reference
                        // type (e.g. Account.Contract is declared `Contract? Contract`)
                        // includes a trailing '?' in its output — e.g.
                        // "Database.Entity.Contract?" instead of
                        // "Database.Entity.Contract". That '?' is not part of the CLR
                        // metadata name, so GetTypeByMetadataName(...) on the read side
                        // (EntityForeignKeyGraph.Build) silently fails to resolve it and
                        // drops the edge entirely — with no crash, just a missing FK
                        // edge for every entity whose relevant navigation property
                        // happens to be nullable-annotated. This was the actual root
                        // cause of Contract's AccountId FK never appearing in the
                        // generated column/join output: it had no non-annotated
                        // duplicate to fall back on, unlike a few other entities in this
                        // graph that happened to get serialized from both a nullable and
                        // non-nullable navigation direction.
                        //
                        // Stripping the trailing '?' here (at serialization) is the real
                        // fix; EntityForeignKeyGraph.Build also strips defensively on
                        // the read side in case any other producer of this format makes
                        // the same mistake in the future.
                        // ---------------------------------------------------------
                        static string StripNullableAnnotation(string typeName) =>
                            typeName.EndsWith("?", StringComparison.Ordinal)
                                ? typeName.Substring(0, typeName.Length - 1)
                                : typeName;

                        var edgeLines =
                            result.Keys.Select(k =>
                                $"{StripNullableAnnotation(k.DeclaringEntityType.ToDisplayString())}|" +
                                $"{k.RawForeignKeyColumn}|" +
                                $"{StripNullableAnnotation(k.RelatedEntityType.ToDisplayString())}|" +
                                $"{k.RawPrincipalKeyColumn}");


                        var serialized =
                            string.Join(";", edgeLines);


                        var escaped =
                            serialized.Replace("\"", "\"\"");
                        
                        spc.ReportDiagnostic(
                            Diagnostic.Create(
                                MappingDiagnostics.EntityGraphDebug,
                                Location.None,
                                $"Emitter assembly={result.AssemblyName}, keys={result.Keys.Length}"
                            ));


                        var source =
                            $"""
                            [assembly: global::Graphgine.Mapping.EntityForeignKeyGraph(
                                @"{escaped}")]
                            """;


                        spc.AddSource(
                            "EntityForeignKeyGraph.g.cs",
                            source);
                    }
                    catch (Exception ex)
                    {
                        ReportCrash(spc, ex);
                    }
                });



            context.RegisterSourceOutput(
                derivedModelKeys,
                static (spc, result) =>
                {
                    try
                    {
                        if (!result.Enabled)
                            return;


                        spc.ReportDiagnostic(
                            Diagnostic.Create(
                                MappingDiagnostics.EntityGraphDebug,
                                Location.None,
                                $"Model emitter assembly={result.AssemblyName}, keys={result.Keys.Count}"
                            ));


                        if (result.Keys.Count == 0)
                            return;


                        var edgeLines =
                            result.Keys.Select(k =>
                                $"{k.Key}|{k.Value}");


                        var serialized =
                            string.Join(";", edgeLines);


                        var escaped =
                            serialized.Replace("\"", "\"\"");


                        var source =
                            $"""
                            [assembly: global::Graphgine.Mapping.ModelForeignKeyGraph(
                                @"{escaped}")]
                            """;


                        spc.AddSource(
                            "ModelForeignKeyGraph.g.cs",
                            source);
                    }
                    catch (Exception ex)
                    {
                        ReportCrash(spc, ex);
                    }
                });
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



        private sealed record EntityResult(
            string AssemblyName,
            ImmutableArray<FluentEntityNavigationConvention.DerivedForeignKey> Keys,
            bool Enabled,
            int Count);



        private sealed record ModelResult(
            string AssemblyName,
            ImmutableDictionary<(INamedTypeSymbol Entity, string NavigationName), string> Keys,
            bool Enabled);
    }
}