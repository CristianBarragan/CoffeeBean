using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

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


        public static List<DerivedForeignKey> CollectAll(
            Compilation compilation,
            CancellationToken ct)
        {
            var results = new List<DerivedForeignKey>();

            foreach (var tree in compilation.SyntaxTrees)
            {
                ct.ThrowIfCancellationRequested();

                var semanticModel =
                    compilation.GetSemanticModel(tree);

                var root =
                    tree.GetRoot(ct);


                foreach (var classDecl in root.DescendantNodes()
                             .OfType<ClassDeclarationSyntax>())
                {
                    ct.ThrowIfCancellationRequested();


                    var entityType =
                        TryGetConfiguredEntityType(
                            classDecl,
                            semanticModel,
                            ct);


                    if (entityType == null)
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
                                x => x.Name is "WithOne" or "WithMany");


                        if (withIndex < 0)
                            continue;


                        var fkIndex =
                            chain.FindIndex(
                                withIndex + 1,
                                x => x.Name == "HasForeignKey");


                        if (fkIndex < 0)
                            continue;


                        var navigationName =
                            ExtractLambdaMemberName(
                                chain[hasIndex].Invocation);


                        var inverseName =
                            ExtractLambdaMemberName(
                                chain[withIndex].Invocation);


                        var rawFkColumn =
                            ExtractLambdaMemberName(
                                chain[fkIndex].Invocation);


                        if (navigationName == null ||
                            inverseName == null ||
                            rawFkColumn == null)
                        {
                            continue;
                        }


                        var navigationProperty =
                            entityType.GetMembers()
                                .OfType<IPropertySymbol>()
                                .FirstOrDefault(x =>
                                    string.Equals(
                                        x.Name,
                                        navigationName,
                                        StringComparison.Ordinal));


                        var relatedEntity =
                            ResolveRelatedType(
                                navigationProperty);


                        if (relatedEntity == null)
                            continue;


                        // ---------------------------------------------------
                        // Determine which side is dependent (owns the FK
                        // column) and which is principal (owns the PK the FK
                        // points at). This is NOT always "entityType" — the
                        // class whose Configure() this statement lives in.
                        //
                        //   HasMany(x => x.Children).WithOne(...).HasForeignKey(...)
                        //     -> entityType is principal ("one" side);
                        //        relatedEntity (the "many"/child side) owns
                        //        the FK column.
                        //
                        //   HasOne(x => x.Other).WithMany(...).HasForeignKey(...)
                        //     -> entityType is dependent (it's configuring its
                        //        own single FK-holding navigation);
                        //        relatedEntity is principal.
                        //
                        //   HasOne(x => x.Other).WithOne(...).HasForeignKey<T>(...)
                        //     -> ambiguous from HasOne/WithOne alone; the
                        //        generic argument on HasForeignKey<T> is the
                        //        actual dependent side and must be checked
                        //        explicitly.
                        //
                        // Getting this backwards (as a previous version of
                        // this method did, always treating entityType as
                        // DeclaringEntityType/dependent) causes every derived
                        // FK edge to record the wrong entity as owning the FK
                        // column, and to default RawPrincipalKeyColumn from
                        // the wrong entity's primary key.
                        // ---------------------------------------------------

                        INamedTypeSymbol dependentEntityType;
                        INamedTypeSymbol principalEntityType;

                        var relationshipName = chain[hasIndex].Name;

                        if (relationshipName == "HasMany")
                        {
                            // entityType.HasMany(x => x.Children) — entityType
                            // is principal; relatedEntity is dependent.
                            principalEntityType = entityType;
                            dependentEntityType = relatedEntity;
                        }
                        else // "HasOne"
                        {
                            var fkGenericTarget =
                                ExtractHasForeignKeyGenericArgument(
                                    chain[fkIndex].Invocation,
                                    semanticModel,
                                    ct);

                            if (fkGenericTarget != null &&
                                SymbolEqualityComparer.Default.Equals(
                                    fkGenericTarget,
                                    entityType))
                            {
                                // HasOne(...).WithOne(...).HasForeignKey<TSelf>(...)
                                // — the FK actually lives on entityType itself.
                                dependentEntityType = entityType;
                                principalEntityType = relatedEntity;
                            }
                            else if (fkGenericTarget != null &&
                                     SymbolEqualityComparer.Default.Equals(
                                         fkGenericTarget,
                                         relatedEntity))
                            {
                                // HasOne(...).WithOne(...).HasForeignKey<TRelated>(...)
                                // — the FK lives on the related entity.
                                dependentEntityType = relatedEntity;
                                principalEntityType = entityType;
                            }
                            else
                            {
                                // No generic HasForeignKey<T> (plain
                                // HasOne(...).WithMany(...).HasForeignKey(...)
                                // form) — entityType is configuring its own
                                // FK navigation, so entityType is dependent.
                                dependentEntityType = entityType;
                                principalEntityType = relatedEntity;
                            }
                        }


                        //
                        // Real EF principal key.
                        //
                        // Example:
                        //
                        // HasPrincipalKey(x => x.AccountId)
                        //
                        // If not explicitly configured, EF uses the PK of
                        // the PRINCIPAL entity — not whichever entity
                        // happens to be "relatedEntity" from entityType's
                        // point of view.
                        //
                        var principalKeyIndex =
                            chain.FindIndex(
                                fkIndex + 1,
                                x => x.Name == "HasPrincipalKey");


                        var rawPrincipalKeyColumn =
                            principalKeyIndex >= 0
                                ? ExtractLambdaMemberName(
                                    chain[principalKeyIndex].Invocation)
                                : null;


                        if (string.IsNullOrWhiteSpace(rawPrincipalKeyColumn))
                        {
                            rawPrincipalKeyColumn =
                                FindPrimaryKeyPropertyName(
                                    principalEntityType);
                        }


                        // ---------------------------------------------------
                        // No more silent "?? Id" fallback here. Every other
                        // convention in this codebase keys off "{Entity}Key",
                        // never a literal "Id" — defaulting to "Id" when
                        // FindPrimaryKeyPropertyName can't resolve anything
                        // was a hardcoded guess that could silently point a
                        // join at a column that doesn't exist. If neither
                        // HasPrincipalKey(...) nor any of the naming
                        // conventions in FindPrimaryKeyPropertyName resolve a
                        // column, that's a real configuration gap and must
                        // fail loudly at generation time, not produce a
                        // plausible-looking wrong join at runtime.
                        // ---------------------------------------------------
                        if (string.IsNullOrWhiteSpace(rawPrincipalKeyColumn))
                        {
                            throw new InvalidOperationException(
                                $"Unable to determine a principal key column for " +
                                $"'{principalEntityType.Name}' while deriving the " +
                                $"foreign key from '{dependentEntityType.Name}." +
                                $"{navigationName}'. Configure HasPrincipalKey(...) " +
                                $"explicitly in the fluent mapping for " +
                                $"'{principalEntityType.Name}'.");
                        }

                        results.Add(
                            new DerivedForeignKey(
                                DeclaringEntityType:
                                    dependentEntityType,

                                RelatedEntityType:
                                    principalEntityType,

                                ModelForeignKeyProperty:
                                    rawFkColumn,

                                ModelPrincipalKeyProperty:
                                    rawPrincipalKeyColumn,

                                RawForeignKeyColumn:
                                    rawFkColumn,

                                RawPrincipalKeyColumn:
                                    rawPrincipalKeyColumn,

                                NavigationName:
                                    navigationName,

                                IsAmbiguous:
                                    false));
                    }
                }
            }

            return results;
        }


        public static void AddNavigationKeyFields(
            MappingClassInfo info,
            IEnumerable<FluentEntityNavigationConvention.DerivedForeignKey> foreignKeys)
        {
            if (info.EntityType == null)
                return;


            foreach (var fk in foreignKeys)
            {
                if (!SymbolEqualityComparer.Default.Equals(
                        fk.DeclaringEntityType,
                        info.EntityType))
                {
                    continue;
                }


                if (string.IsNullOrWhiteSpace(
                        fk.NavigationName))
                {
                    continue;
                }


                var sourceName =
                    fk.NavigationName + "Key";


                if (info.FieldMaps.Any(x =>
                        string.Equals(
                            x.SourceName,
                            sourceName,
                            StringComparison.Ordinal)))
                {
                    continue;
                }


                info.FieldMaps.Add(
                    new FieldInfo
                    {
                        SourceName =
                            sourceName,

                        DestinationEntity =
                            fk.DeclaringEntityType.Name,

                        // IMPORTANT:
                        // This is the actual EF model FK property.
                        // Never build NavigationName + "Id".
                        DestinationName =
                            fk.ModelForeignKeyProperty,

                        IsNavigationKey =
                            true
                    });
            }
        }


        // ---------------------------------------------------------------
        // REMOVED: ResolveActualForeignKeyProperty(...)
        // REMOVED: NormalizeForeignKeyColumn(...)
        //
        // Both were confirmed dead by repo-wide search — never called from
        // CollectAll, AddNavigationKeyFields, or anywhere outside this file.
        // Their logic (strip "Id" suffix, try "{prefix}Key") is already
        // covered by EntityForeignKeyGraph.ResolveColumnName below, which is
        // the version that's actually wired into the live FK graph. Keeping
        // two independent, silently-diverging implementations of the same
        // guess was itself a source of the hardcoded-key smell — deleted
        // rather than kept "just in case".
        // ---------------------------------------------------------------


        /// <summary>
        /// Fallback convention chain used ONLY when a fluent mapping doesn't
        /// explicitly declare HasPrincipalKey(...). This still guesses by
        /// name — that can't be fully eliminated without requiring every
        /// entity config to be 100% explicit — but every guess here inspects
        /// the entity's REAL declared properties (never fabricates a literal
        /// like "Id" that might not exist). Order matters: exact "Id" is
        /// EF's own default convention, "{Entity}Key" is this codebase's
        /// dominant business-key convention, and "any *Key property" is a
        /// last-resort, single-property-only fallback.
        /// </summary>
        private static string? FindPrimaryKeyPropertyName(
            INamedTypeSymbol entityType)
        {
            var properties =
                entityType.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Select(x => x.Name)
                    .ToList();


            // 1. Exact Id
            var id =
                properties.FirstOrDefault(x =>
                    string.Equals(
                        x,
                        "Id",
                        StringComparison.OrdinalIgnoreCase));

            if (id != null)
                return id;


            // 2. EntityName + Key
            var entityKey =
                entityType.Name + "Key";


            var key =
                properties.FirstOrDefault(x =>
                    string.Equals(
                        x,
                        entityKey,
                        StringComparison.OrdinalIgnoreCase));

            if (key != null)
                return key;


            // 3. Any single *Key property — only safe when there's exactly
            // one candidate; multiple *Key properties means this guess is
            // ambiguous and callers should treat a null return here as a
            // hard failure, not silently pick the first one.
            var keyCandidates =
                properties
                    .Where(x =>
                        x.EndsWith(
                            "Key",
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

            return keyCandidates.Count == 1
                ? keyCandidates[0]
                : null;
        }

        internal static class EntityGraphPathfinder
        {
            public static List<EntityForeignKeyGraph.Edge>? FindPath(
                List<EntityForeignKeyGraph.Edge> edges,
                INamedTypeSymbol from,
                INamedTypeSymbol to)
            {
                var adjacency =
                    new Dictionary<
                        INamedTypeSymbol,
                        List<(EntityForeignKeyGraph.Edge Edge, bool Forward)>>(
                        SymbolEqualityComparer.Default);


                void AddAdj(
                    INamedTypeSymbol node,
                    EntityForeignKeyGraph.Edge edge,
                    bool forward)
                {
                    if (!adjacency.TryGetValue(node, out var list))
                    {
                        adjacency[node] = list = new();
                    }

                    list.Add((edge, forward));
                }


                foreach (var edge in edges)
                {
                    AddAdj(
                        edge.DependentEntity,
                        edge,
                        true);

                    AddAdj(
                        edge.PrincipalEntity,
                        edge,
                        false);
                }


                var visited =
                    new HashSet<INamedTypeSymbol>(
                        SymbolEqualityComparer.Default)
                    {
                        from
                    };


                var queue =
                    new Queue<(
                        INamedTypeSymbol Node,
                        List<EntityForeignKeyGraph.Edge> Path)>();


                queue.Enqueue(
                    (
                        from,
                        new List<EntityForeignKeyGraph.Edge>()
                    ));


                while (queue.Count > 0)
                {
                    var current =
                        queue.Dequeue();


                    if (SymbolEqualityComparer.Default.Equals(
                            current.Node,
                            to))
                    {
                        return current.Path;
                    }


                    if (!adjacency.TryGetValue(
                            current.Node,
                            out var neighbors))
                    {
                        continue;
                    }


                    foreach (var (edge, forward) in neighbors)
                    {
                        var next =
                            forward
                                ? edge.PrincipalEntity
                                : edge.DependentEntity;


                        if (visited.Contains(next))
                            continue;


                        visited.Add(next);


                        queue.Enqueue(
                            (
                                next,
                                new List<EntityForeignKeyGraph.Edge>(
                                    current.Path)
                                {
                                    edge
                                }
                            ));
                    }
                }


                return null;
            }
        }


        public static class EntityForeignKeyGraph
        {
            public sealed record Edge(
                INamedTypeSymbol DependentEntity,
                string DependentColumn,
                INamedTypeSymbol PrincipalEntity,
                string PrincipalColumn);



            public sealed record EntityGraphBuildResult(
                List<Edge> Edges,
                List<string> Diagnostics);

            private static string ResolveColumnName(
                INamedTypeSymbol entity,
                string propertyName)
            {
                var direct =
                    entity.GetMembers()
                        .OfType<IPropertySymbol>()
                        .FirstOrDefault(p =>
                            string.Equals(
                                p.Name,
                                propertyName,
                                StringComparison.OrdinalIgnoreCase));

                if (direct != null)
                    return direct.Name;


                if (propertyName.EndsWith(
                        "Id",
                        StringComparison.OrdinalIgnoreCase))
                {
                    var prefix =
                        propertyName.Substring(
                            0,
                            propertyName.Length - 2);

                    var keyProperty =
                        entity.GetMembers()
                            .OfType<IPropertySymbol>()
                            .FirstOrDefault(p =>
                                string.Equals(
                                    p.Name,
                                    prefix + "Key",
                                    StringComparison.OrdinalIgnoreCase));

                    if (keyProperty != null)
                        return keyProperty.Name;
                }


                return propertyName;
            }

            public static EntityGraphBuildResult Build(
                Compilation compilation,
                CancellationToken ct)
            {
                var edges =
                    new List<Edge>();

                var diagnostics =
                    new List<string>();


                var attributeType =
                    compilation.GetTypeByMetadataName(
                        "CoffeeBeanery.GraphQL.Core.Mapping.EntityForeignKeyGraphAttribute");


                if (attributeType == null)
                {
                    return new EntityGraphBuildResult(
                        edges,
                        diagnostics);
                }


                AttributeData? found = null;


                found =
                    compilation.Assembly
                        .GetAttributes()
                        .FirstOrDefault(a =>
                            SymbolEqualityComparer.Default.Equals(
                                a.AttributeClass,
                                attributeType));


                if (found == null)
                {
                    var assemblies =
                        new Queue<IAssemblySymbol>();

                    var visited =
                        new HashSet<string>(
                            StringComparer.Ordinal);


                    foreach (var reference in compilation.References)
                    {
                        if (compilation.GetAssemblyOrModuleSymbol(reference)
                            is IAssemblySymbol assembly)
                        {
                            assemblies.Enqueue(assembly);
                        }
                    }


                    while (assemblies.Count > 0)
                    {
                        ct.ThrowIfCancellationRequested();


                        var assembly =
                            assemblies.Dequeue();


                        if (!visited.Add(
                                assembly.Name))
                        {
                            continue;
                        }


                        found =
                            assembly.GetAttributes()
                                .FirstOrDefault(a =>
                                    SymbolEqualityComparer.Default.Equals(
                                        a.AttributeClass,
                                        attributeType));


                        if (found != null)
                        {
                            break;
                        }


                        foreach (var reference in assembly.Modules
                                     .SelectMany(x =>
                                         x.ReferencedAssemblySymbols))
                        {
                            assemblies.Enqueue(reference);
                        }
                    }
                }


                if (found == null ||
                    found.ConstructorArguments.Length == 0)
                {
                    return new EntityGraphBuildResult(
                        edges,
                        diagnostics);
                }


                var serialized =
                    found.ConstructorArguments[0].Value
                    as string;


                if (string.IsNullOrWhiteSpace(serialized))
                {
                    return new EntityGraphBuildResult(
                        edges,
                        diagnostics);
                }


                foreach (var line in serialized.Split(';'))
                {
                    ct.ThrowIfCancellationRequested();


                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }


                    var parts =
                        line.Split('|');


                    if (parts.Length != 4)
                    {
                        diagnostics.Add(
                            $"Invalid FK graph entry: {line}");

                        continue;
                    }


                    // ---------------------------------------------------------------
                    // FIXED (defense in depth): a metadata name never legitimately
                    // contains '?' — that's a nullable-reference-type source/display
                    // annotation, not part of the CLR type name. If a producer of
                    // this serialized format calls ToDisplayString() on an
                    // INamedTypeSymbol without suppressing nullable annotations (the
                    // bug fixed at the source in EntityForeignKeyEmitterGenerator),
                    // GetTypeByMetadataName silently returns null for a name like
                    // "Database.Entity.Contract?" and the edge is dropped with only
                    // a diagnostic — no crash, just a missing FK edge downstream.
                    // Stripping defensively here means a similar mistake anywhere
                    // else that produces this format degrades gracefully instead of
                    // silently losing edges again.
                    // ---------------------------------------------------------------
                    static string StripNullableAnnotation(string typeName) =>
                        typeName.EndsWith("?", StringComparison.Ordinal)
                            ? typeName.Substring(0, typeName.Length - 1)
                            : typeName;

                    var dependentType =
                        compilation.GetTypeByMetadataName(
                            StripNullableAnnotation(parts[0]));


                    var principalType =
                        compilation.GetTypeByMetadataName(
                            StripNullableAnnotation(parts[2]));


                    if (dependentType == null ||
                        principalType == null)
                    {
                        diagnostics.Add(
                            $"Unable to resolve FK graph types: {parts[0]} -> {parts[2]}");

                        continue;
                    }


                    var dependentColumn =
                        ResolveColumnName(
                            dependentType,
                            parts[1]);


                    var principalColumn =
                        ResolveColumnName(
                            principalType,
                            parts[3]);


                    edges.Add(
                        new Edge(
                            dependentType,
                            dependentColumn,
                            principalType,
                            principalColumn));
                }


                return new EntityGraphBuildResult(
                    edges,
                    diagnostics);
            }
        }



        private static INamedTypeSymbol? ResolveRelatedType(
            IPropertySymbol? property)
        {
            if (property == null)
                return null;


            var type =
                property.Type;


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
                        StringComparison.OrdinalIgnoreCase));
        }



        private static INamedTypeSymbol? TryGetConfiguredEntityType(
            ClassDeclarationSyntax classDecl,
            SemanticModel semanticModel,
            CancellationToken ct)
        {
            if (semanticModel.GetDeclaredSymbol(
                    classDecl,
                    ct)
                is not INamedTypeSymbol symbol)
            {
                return null;
            }


            foreach (var iface in symbol.AllInterfaces)
            {
                if (iface.Name ==
                    "IEntityTypeConfiguration" &&
                    iface.TypeArguments.Length == 1 &&
                    iface.TypeArguments[0]
                        is INamedTypeSymbol entity)
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


            var current =
                outer;


            while (current.Expression
                   is MemberAccessExpressionSyntax member)
            {
                var name =
                    member.Name is GenericNameSyntax generic
                        ? generic.Identifier.Text
                        : member.Name.Identifier.Text;


                result.Insert(
                    0,
                    (
                        name,
                        current
                    ));


                if (member.Expression
                    is InvocationExpressionSyntax inner)
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
            var argument =
                invocation.ArgumentList.Arguments
                    .FirstOrDefault()
                    ?.Expression;


            if (argument is not SimpleLambdaExpressionSyntax lambda)
                return null;


            if (lambda.Body is MemberAccessExpressionSyntax member)
            {
                return member.Name.Identifier.Text;
            }


            return null;
        }


        /// <summary>
        /// Extracts the type argument T from a HasForeignKey&lt;T&gt;(...)
        /// call, e.g. HasForeignKey&lt;Contract&gt;(c => c.AccountId) returns
        /// the symbol for Contract. Returns null if HasForeignKey is not the
        /// generic form (plain HasForeignKey(...) with no type argument).
        /// </summary>
        private static INamedTypeSymbol? ExtractHasForeignKeyGenericArgument(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            CancellationToken ct)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax
                {
                    Name: GenericNameSyntax generic
                })
            {
                return null;
            }

            if (generic.TypeArgumentList.Arguments.Count != 1)
                return null;

            var typeArg = generic.TypeArgumentList.Arguments[0];

            var symbolInfo = semanticModel.GetSymbolInfo(typeArg, ct);

            return symbolInfo.Symbol as INamedTypeSymbol;
        }
    }
}