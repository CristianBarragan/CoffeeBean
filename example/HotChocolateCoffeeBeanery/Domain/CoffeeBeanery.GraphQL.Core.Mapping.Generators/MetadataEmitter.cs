using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Emit;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;
using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Passes;
using Microsoft.CodeAnalysis;

internal static class MetadataEmitter
{
    public static string Emit(
        ImmutableArray<MappingClassInfo> allMappings,
        ImmutableHashSet<INamedTypeSymbol> rootEntityTypes,
        List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph)
    {
        var models = allMappings
            .Where(m => m.IsModel)
            .GroupBy(m => m.ModelType.Name, System.StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Distinct()
            .OrderBy(m => m.ModelType.Name, System.StringComparer.Ordinal)
            .ToList();

        foreach (var m in models)
        {
            if (m.CteUpdateMeta.Count > 0)
                continue;

            if (m.Graph != null)
            {
                foreach (var resolution in MutationMetadataEmitter.BuildGraphEdgeCteResolutions(
                             m, allMappings, entityGraph))
                {
                    m.CteUpdateMeta.Add(
                        new CteUpdateMetaInfo
                        {
                            NavigationAlias =
                                resolution.NavigationAlias,

                            ForeignKeyColumn =
                                resolution.ForeignKeyColumn,

                            ForeignKeyColumnId =
                                resolution.ForeignKeyColumnId,

                            OwningPrimaryKeyColumn =
                                resolution.OwningPrimaryKeyColumn,

                            OwningPrimaryKeyColumnId =
                                resolution.OwningPrimaryKeyColumnId,

                            RelatedEntityTypeName =
                                resolution.RelatedEntityTypeName,

                            RelatedStorageEntityId =
                                resolution.RelatedStorageEntityId,

                            RelatedSurrogateIdColumn =
                                resolution.RelatedSurrogateIdColumn,

                            RelatedSurrogateIdColumnId =
                                resolution.RelatedSurrogateIdColumnId,

                            RelatedNaturalKeyColumn =
                                resolution.RelatedNaturalKeyColumn,

                            RelatedNaturalKeyColumnId =
                                resolution.RelatedNaturalKeyColumnId
                        });
                }

                continue;
            }

            var navResult = EntityNavigationConvention.Resolve(m, allMappings, entityGraph, rootEntityTypes);

            // FIXED: PopulateCteUpdateMeta now takes entityGraph (see its
            // signature below) so its column-index resolution routes
            // through ColumnIdResolver/GetFullColumnOrder instead of a raw
            // GetScalarProperties lookup that could disagree with every
            // other emitter's numbering.
            PopulateCteUpdateMeta(
                m,
                navResult,
                allMappings,
                entityGraph);
        }

        // ---------------------------------------------------------------
        // FIXED: this list determines the ARRAY ORDER for EntityColumnName,
        // EntityTable, and EntitySchema below -- arrays that are indexed at
        // runtime by StorageEntityId.* constants. Those constants (emitted
        // by IdEmitter.EmitStorageEntityIds) are assigned by taking every
        // entity's stripped name, deduplicating, and sorting ALPHABETICALLY:
        //
        //     mappings.SelectMany(...).Select(StripEntitySuffix)
        //         .Distinct(Ordinal).OrderBy(x => x, Ordinal)
        //
        // This list was instead built in FIRST-ENCOUNTERED order while
        // iterating `models` (itself sorted alphabetically by MODEL name,
        // not entity name) and each model's own Definition.Entities in
        // whatever order they were declared in BuildMap(). Those two
        // orderings have no reason to agree -- and for any composite model
        // whose Definition.Entities isn't itself alphabetical (e.g. Product
        // listing Account, Contract, Transaction, CustomerBankingRelationship
        // in FK-chain order), they didn't. The result: EntityColumnName[i]
        // (and EntityTable[i]/EntitySchema[i]) could silently hold a
        // DIFFERENT entity's data than whatever entity StorageEntityId
        // value `i` actually refers to -- exactly the class of bug behind
        // "AppendJoin: cannot resolve TO column. ChildStorageEntityId=2,
        // ChildColumnId=5, ArrayLength=5": index 2 (Contract, by the real
        // alphabetical StorageEntityId numbering) was reading some OTHER
        // entity's shorter column array.
        //
        // Rebuilt here with the exact same ordering rule
        // EmitStorageEntityIds uses, so entityTypes[i]'s stripped name is
        // guaranteed to equal the i-th name in that same alphabetically
        // sorted distinct list -- i.e. index i here always corresponds to
        // StorageEntityId value i.
        // ---------------------------------------------------------------
        var entityTypes =
            models
                .SelectMany(m => m.Definition.Entities)
                .Where(e => e.EntityType != null)
                .Select(e => e.EntityType!)
                .GroupBy(
                    e => IdEmitter.StripEntitySuffix(e.Name),
                    StringComparer.Ordinal)
                .OrderBy(
                    g => g.Key,
                    StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

        var entitySchemaLookup =
            new Dictionary<INamedTypeSymbol,string>(
                SymbolEqualityComparer.Default);

        foreach (var mapping in models)
        {
            foreach (var entity in mapping.Definition.Entities)
            {
                if (entity.EntityType == null)
                    continue;


                // Ignore composite model schema
                if (mapping.Definition.Entities.Count > 1)
                    continue;


                if (string.IsNullOrWhiteSpace(mapping.Schema))
                    continue;


                entitySchemaLookup[entity.EntityType] =
                    mapping.Schema;
            }
        }

        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine();

        sb.AppendLine("namespace CoffeeBeanery.GraphQL.Core.Runtime");
        sb.AppendLine("{");

        sb.AppendLine("    public static class EntityMeta");
        sb.AppendLine("    {");
        
        EmitModelNameArray(sb, models);
        EmitSchemaArray(sb, models);
        EmitColumnNameArray(sb, models, allMappings, entityGraph);
        EmitFieldNameArray(sb, models);
        EmitFieldToColumnArray(sb, models, allMappings, entityGraph);
        EmitFieldMappingsArray(sb, models);
        EmitConflictColumnsArray(sb, entityTypes, allMappings);
        EmitCteResolutionsArray(sb, models);

        var storageEntities =
            GetStorageEntities(
                entityTypes,
                allMappings);

        EmitTableArray(sb, models);
        
        EmitEntityTableArray(
            sb,
            storageEntities);

        EmitEntitySchemaArray(
            sb,
            storageEntities);

        EmitEntityColumnNameArray(
            sb,
            entityTypes,
            allMappings,
            entityGraph);

        // ---------------------------------------------------------------
        // FIXED: MutationRuntimePlanner.cs calls
        // EntityMeta.TryGetEntityId(spec.RelatedEntityTypeName, out var id)
        // to resolve a graph-edge navigation's related entity at runtime
        // (replacing a hardcoded EntityId.Customer bug). TryGetEntityId
        // previously only existed as an INSTANCE method on
        // GeneratedEntityMetaProvider (which implements IEntityMetaProvider)
        // — there was no static entry point on EntityMeta itself for
        // hand-written runtime code to call without constructing a provider
        // instance (CS0117: 'EntityMeta' does not contain a definition for
        // 'TryGetEntityId'). This adds a static twin here, scanning the same
        // ModelName array GeneratedEntityMetaProvider.TryGetEntityId already
        // uses, so both paths stay backed by one source of truth.
        // ---------------------------------------------------------------
        EmitStaticTryGetEntityId(sb);

        sb.AppendLine("    }");

        EmitEnumConversions(sb, models, allMappings, entityGraph);

        sb.AppendLine("}");
        sb.AppendLine();

        EmitModelIdConstants(sb, models);

        EmitGeneratedEntityMetaProvider(sb);

        EmitGeneratedPlannerRegistry(sb);

        return sb.ToString();
    }
    
    internal static void PopulateCteUpdateMeta(
    MappingClassInfo info,
    NavigationResolutionResult navResult,
    ImmutableArray<MappingClassInfo> allMappings,
    List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph)
{
    var primaryEntry =
        info.Definition.Entities
            .FirstOrDefault(x =>
                x.IsPrimary &&
                x.EntityType != null);

    if (primaryEntry?.EntityType == null ||
        string.IsNullOrWhiteSpace(primaryEntry.ToColumn))
    {
        return;
    }

    ushort ResolveColumnId(
        INamedTypeSymbol entityType,
        string columnName)
    {
        return ColumnIdResolver.ResolveId(
            entityType,
            columnName,
            allMappings,
            entityGraph);
    }

    ushort ResolveStorageId(
        INamedTypeSymbol entityType)
    {
        var stripped =
            IdEmitter.StripEntitySuffix(entityType.Name);

        var ordered =
            allMappings
                .SelectMany(m => m.Definition.Entities)
                .Where(e => e.EntityType != null)
                .Select(e => IdEmitter.StripEntitySuffix(e.EntityType!.Name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

        var index =
            ordered.FindIndex(x =>
                string.Equals(
                    x,
                    stripped,
                    StringComparison.Ordinal));

        return index < 0
            ? (ushort)0
            : (ushort)index;
    }

    var primaryEntityType =
        primaryEntry.EntityType;

    foreach (var nav in navResult.Navigations)
    {
        if (nav.RelatedEntityType == null)
            continue;

        if (string.IsNullOrWhiteSpace(nav.ForeignKeyProperty))
            continue;

        var naturalKey =
            info.Definition.Entities
                .FirstOrDefault(x =>
                    x.EntityType != null &&
                    !string.IsNullOrWhiteSpace(x.ToColumn) &&
                    (
                        string.Equals(
                            x.AliasProperty,
                            nav.NavigationName,
                            StringComparison.OrdinalIgnoreCase)
                        ||
                        string.Equals(
                            x.EntityType.Name,
                            nav.RelatedEntityType.Name,
                            StringComparison.OrdinalIgnoreCase)
                    ));

        if (naturalKey == null)
            continue;

        if (info.CteUpdateMeta.Any(x =>
            string.Equals(
                x.NavigationAlias,
                nav.NavigationName,
                StringComparison.OrdinalIgnoreCase)))
        {
            continue;
        }

        var surrogate =
            GetPkPropertyName(
                nav.RelatedEntityType);

        info.CteUpdateMeta.Add(
            new CteUpdateMetaInfo
            {
                NavigationAlias =
                    nav.NavigationName,

                ForeignKeyColumn =
                    nav.ForeignKeyProperty,

                ForeignKeyColumnId =
                    ResolveColumnId(
                        primaryEntityType,
                        nav.ForeignKeyProperty),

                OwningPrimaryKeyColumn =
                    primaryEntry.ToColumn!,

                OwningPrimaryKeyColumnId =
                    ResolveColumnId(
                        primaryEntityType,
                        primaryEntry.ToColumn!),

                RelatedEntityTypeName =
                    nav.RelatedEntityType.Name,

                RelatedStorageEntityId =
                    ResolveStorageId(
                        nav.RelatedEntityType),

                RelatedSurrogateIdColumn =
                    surrogate,

                RelatedSurrogateIdColumnId =
                    ResolveColumnId(
                        nav.RelatedEntityType,
                        surrogate),

                RelatedNaturalKeyColumn =
                    naturalKey.ToColumn!,
                
                                RelatedNaturalKeyColumnId =
                    ResolveColumnId(
                        nav.RelatedEntityType,
                        naturalKey.ToColumn!)
            });
    }

    foreach (var link in info.Definition.Entities.Where(x =>
                 !x.IsPrimary &&
                 x.EntityType != null &&
                 !string.IsNullOrWhiteSpace(x.AliasProperty) &&
                 !string.IsNullOrWhiteSpace(x.ToColumn)))
    {
        if (info.CteUpdateMeta.Any(x =>
                string.Equals(
                    x.NavigationAlias,
                    link.AliasProperty,
                    StringComparison.OrdinalIgnoreCase)))
        {
            continue;
        }

        var fk =
            link.AliasProperty + "Id";

        var surrogate =
            GetPkPropertyName(
                link.EntityType!);

        info.CteUpdateMeta.Add(
            new CteUpdateMetaInfo
            {
                NavigationAlias =
                    link.AliasProperty!,

                ForeignKeyColumn =
                    fk,

                ForeignKeyColumnId =
                    ResolveColumnId(
                        primaryEntityType,
                        fk),

                OwningPrimaryKeyColumn =
                    primaryEntry.ToColumn!,

                OwningPrimaryKeyColumnId =
                    ResolveColumnId(
                        primaryEntityType,
                        primaryEntry.ToColumn!),

                RelatedEntityTypeName =
                    link.EntityType.Name,

                RelatedStorageEntityId =
                    ResolveStorageId(
                        link.EntityType),

                RelatedSurrogateIdColumn =
                    surrogate,

                RelatedSurrogateIdColumnId =
                    ResolveColumnId(
                        link.EntityType,
                        surrogate),

                RelatedNaturalKeyColumn =
                    link.ToColumn!,

                RelatedNaturalKeyColumnId =
                    ResolveColumnId(
                        link.EntityType,
                        link.ToColumn!)
            });
    }
}
    
    
    
    private static void EmitFieldToColumnArray(
        StringBuilder sb,
        List<MappingClassInfo> models,
        ImmutableArray<MappingClassInfo> allMappings,
        List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph)
    {
        sb.AppendLine(
            $"        public static readonly ushort[][] FieldToColumn = new ushort[{models.Count}][]");

        sb.AppendLine("        {");

        foreach (var model in models)
        {
            var mappings =
                PlannerEmitter.ComputeFieldMappingsEagerPublic(
                        model,
                        PlannerEmitter.IsCompositeInfo(model))
                    .ToList();

            if (mappings.Count == 0)
            {
                sb.AppendLine(
                    "            System.Array.Empty<ushort>(),");
                continue;
            }

            var columnIds =
                new List<ushort>();

            foreach (var mapping in mappings)
            {
                var entity =
                    model.Definition.Entities
                        .FirstOrDefault(e =>
                            e.EntityType != null &&
                            string.Equals(
                                IdEmitter.StripEntitySuffix(e.EntityType.Name),
                                IdEmitter.StripEntitySuffix(mapping.EntityTypeName),
                                StringComparison.OrdinalIgnoreCase));

                if (entity?.EntityType == null)
                {
                    columnIds.Add(ushort.MaxValue);
                    continue;
                }

                var columnId =
                    ColumnIdResolver.ResolveId(
                        entity.EntityType,
                        mapping.ColumnName,
                        allMappings,
                        entityGraph);

                columnIds.Add(columnId);
            }

            sb.AppendLine(
                $"            new ushort[] {{ {string.Join(", ", columnIds)} }},");
        }

        sb.AppendLine("        };");
        sb.AppendLine();
    }
    
    private static List<(string EntityName, string Schema)> GetStorageEntities(
        List<INamedTypeSymbol> entityTypes,
        ImmutableArray<MappingClassInfo> allMappings)
    {
        var result = new List<(string EntityName, string Schema)>();

        foreach (var entityType in entityTypes)
        {
            var entityName = entityType.Name;

            if (entityName.EndsWith(
                    "Entity",
                    StringComparison.Ordinal))
            {
                entityName = entityName.Substring(
                    0,
                    entityName.Length - "Entity".Length);
            }


            var schema = "public";


            var mapping =
                allMappings
                    .Where(m =>
                        m.Definition.Entities.Any(e =>
                            e.EntityType != null &&
                            SymbolEqualityComparer.Default.Equals(
                                e.EntityType,
                                entityType)))
                    .OrderByDescending(m =>
                        m.Definition.Entities.Any(e =>
                            e.EntityType != null &&
                            e.IsPrimary &&
                            SymbolEqualityComparer.Default.Equals(
                                e.EntityType,
                                entityType)))
                    .FirstOrDefault();


            if (mapping != null)
            {
                var entityMapping =
                    mapping.Definition.Entities.FirstOrDefault(e =>
                        e.EntityType != null &&
                        SymbolEqualityComparer.Default.Equals(
                            e.EntityType,
                            entityType));


                schema = new[]
                    {
                        entityMapping?.Schema,
                        mapping.Schema,
                        mapping.Definition.Schema,
                        "public"
                    }
                    .First(s => !string.IsNullOrWhiteSpace(s));
            }


            result.Add((
                EntityName: entityName,
                Schema: schema));
        }


        return result;
    }
    
    // ---------------------------------------------------------------
    // FIXED: added allMappings/entityGraph parameters so the
    // ColumnIdResolver.Resolve(...) call inside compiles (it now requires
    // them) and — more importantly — so this actually resolves the same
    // column index scheme GetFullColumnOrder uses, rather than whatever
    // ColumnIdResolver.Resolve's OLD signature computed (see
    // ColumnIdResolver.cs's own FIXED comment). Call site in Emit(...)
    // updated to match.
    // ---------------------------------------------------------------
    private static void EmitEnumConversions(
    StringBuilder sb,
    List<MappingClassInfo> models,
    ImmutableArray<MappingClassInfo> allMappings,
    List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph)
{
    var enumFields = models
        .SelectMany(m => m.FieldMaps
            .Where(f => f.ModelEnumType != null &&
                        f.EntityEnumType != null)
            .Select(f => (Model: m, Field: f)))
        .ToList();

    sb.AppendLine("    public static class EnumConversions");
    sb.AppendLine("    {");

    sb.AppendLine("        public static string? TryConvert(ushort storageEntityId, ushort columnId, string value)");
    sb.AppendLine("        {");
    sb.AppendLine("            var normalizedValue = value.Trim();");
    sb.AppendLine("            switch (storageEntityId)");
    sb.AppendLine("            {");

    foreach (var group in enumFields.GroupBy(
                 x => x.Field.DestinationEntity,
                 StringComparer.Ordinal))
    {
        var entityName = IdEmitter.StripEntitySuffix(group.Key);

        sb.AppendLine($"                case StorageEntityId.{entityName}:");
        sb.AppendLine("                    switch (columnId)");
        sb.AppendLine("                    {");

        foreach (var item in group)
        {
            var field = item.Field;

            var entityType =
                item.Model.Definition.Entities
                    .FirstOrDefault(e =>
                        string.Equals(
                            IdEmitter.StripEntitySuffix(
                                e.EntityType?.Name ?? ""),
                            entityName,
                            StringComparison.OrdinalIgnoreCase))
                    ?.EntityType;

            if (entityType == null)
            {
                throw new InvalidOperationException(
                    $"Cannot resolve entity type '{entityName}' for enum field '{field.DestinationName}'.");
            }

            var columnExpression =
                ColumnIdResolver.Resolve(
                    entityType,
                    field.DestinationName,
                    allMappings,
                    entityGraph);

            sb.AppendLine($"                        case (ushort){columnExpression}:");
            sb.AppendLine("                            return normalizedValue switch");
            sb.AppendLine("                            {");

            var modelMembers =
                field.ModelEnumType!
                    .GetMembers()
                    .OfType<IFieldSymbol>()
                    .Where(f => f.IsConst && f.HasConstantValue)
                    .ToDictionary(
                        x => x.Name,
                        x => Convert.ToInt32(x.ConstantValue),
                        StringComparer.Ordinal);

            var entityMembers =
                field.EntityEnumType!
                    .GetMembers()
                    .OfType<IFieldSymbol>()
                    .Where(f => f.IsConst && f.HasConstantValue)
                    .ToDictionary(
                        x => x.Name,
                        x => Convert.ToInt32(x.ConstantValue),
                        StringComparer.Ordinal);

            foreach (var pair in modelMembers)
            {
                var modelName = pair.Key;
                var modelValue = pair.Value;

                if (field.EnumIgnored.Contains(modelName))
                    continue;

                var destinationName =
                    field.EnumOverrides.TryGetValue(
                        modelName,
                        out var overrideName)
                        ? overrideName
                        : modelName;

                if (!entityMembers.TryGetValue(
                        destinationName,
                        out var entityValue))
                {
                    throw new InvalidOperationException(
                        $"Enum mapping '{field.ModelEnumType.Name}.{modelName}' -> '{field.EntityEnumType.Name}.{destinationName}' does not exist.");
                }

                sb.AppendLine(
                    $"                                \"{modelValue}\" => \"{entityValue}\",");
            }

            sb.AppendLine("                                _ => null");
            sb.AppendLine("                            };");
        }

        sb.AppendLine("                    }");
        sb.AppendLine("                    break;");
    }

    sb.AppendLine("            }");

    sb.AppendLine("            return null;");
    sb.AppendLine("        }");

    sb.AppendLine("    }");
    sb.AppendLine();
}

    private static List<(string[] Tables, string[] Schemas)> GetModelStorageMappings(
        ImmutableArray<MappingClassInfo> models)
    {
        var result =
            new List<(string[] Tables, string[] Schemas)>();

        foreach (var model in models)
        {
            var tables = new List<string>();
            var schemas = new List<string>();

            foreach (var entity in model.Definition.Entities)
            {
                if (entity.EntityType == null)
                    continue;

                var name = entity.EntityType.Name;

                const string suffix = "Entity";

                if (name.EndsWith(
                        suffix,
                        StringComparison.Ordinal))
                {
                    name = name.Substring(
                        0,
                        name.Length - suffix.Length);
                }

                tables.Add(name);

                schemas.Add(
                    entity.Schema
                    ?? model.Schema
                    ?? model.Definition.Schema
                    ?? "public");
            }

            result.Add((
                Tables: tables.ToArray(),
                Schemas: schemas.ToArray()));
        }

        return result;
    }

    private static ushort ResolveStorageEntityId(
        List<MappingClassInfo> models,
        object destinationEntity)
    {
        if (destinationEntity is ushort id)
            return id;

        var name = destinationEntity.ToString();

        var entities = models
            .SelectMany(m => m.Definition.Entities)
            .Where(x => x.EntityType != null)
            .Select(x => x.EntityType!)
            .GroupBy(
                x => x.Name,
                StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(
                x => x.Name,
                StringComparer.Ordinal)
            .ToList();

        var index = entities.FindIndex(e => string.Equals(
            e.Name,
            name,
            StringComparison.Ordinal));

        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Cannot resolve storage entity id for '{name}'");
        }

        return (ushort)index;
    }

    private static void EmitFieldMappingsArray( StringBuilder sb, List<MappingClassInfo> models) { sb.AppendLine( $" public static readonly global::CoffeeBeanery.GraphQL.Core.Runtime.FieldMapSpec[][] FieldMappings = " + $"new global::CoffeeBeanery.GraphQL.Core.Runtime.FieldMapSpec[{models.Count}][]"); sb.AppendLine( " {"); foreach (var m in models) { var mappings = PlannerEmitter.ComputeFieldMappingsEagerPublic( m, PlannerEmitter.IsCompositeInfo(m)); if (mappings.Count == 0) { sb.AppendLine( " System.Array.Empty<global::CoffeeBeanery.GraphQL.Core.Runtime.FieldMapSpec>(),"); continue; } sb.AppendLine( " new global::CoffeeBeanery.GraphQL.Core.Runtime.FieldMapSpec[]"); sb.AppendLine( " {"); foreach (var fm in mappings) { sb.AppendLine( " new global::CoffeeBeanery.GraphQL.Core.Runtime.FieldMapSpec("); sb.AppendLine( $" \"{fm.FieldName}\","); sb.AppendLine( $" StorageEntityId.{fm.EntityTypeName},"); sb.AppendLine( $" \"{fm.ColumnName}\","); sb.AppendLine( " \"\","); sb.AppendLine( " \"\""); sb.AppendLine( " ),"); } sb.AppendLine( " },"); } sb.AppendLine( " };"); sb.AppendLine(); }

    // ---------------------------------------------------------------
    // REMOVED: the private ResolveColumnId(MappingClassInfo, string)
    // that used to live here. It was a raw GetScalarProperties lookup —
    // the same disagreeing-ordering problem as everywhere else in this
    // file — AND, per a repo-wide search, appears to have no remaining
    // callers once EmitFieldToColumnArray and PopulateCteUpdateMeta were
    // switched to ColumnIdResolver.ResolveId above. Confirm with:
    //
    //   Get-ChildItem -Recurse -Filter *.cs | Select-String -Pattern "(?<!Mutation)MetadataEmitter\.ResolveColumnId|(?<![.\w])ResolveColumnId\(info,\s*columnName\)"
    //
    // before deleting from your actual copy — in this cleaned version
    // it's gone. If something outside this file DOES still call it,
    // restore it but route it through ColumnIdResolver.ResolveId the
    // same way PopulateCteUpdateMeta's local ResolveColumnId now does,
    // rather than reinstating the raw GetScalarProperties version.
    // ---------------------------------------------------------------
    
    // Made internal (was private) so MutationMetadataEmitter.BuildGraphEdgeCteResolutions
    // can resolve a related entity's real surrogate/primary key the same way
    // PopulateCteUpdateMeta already does, instead of reusing the natural-key
    // lookup for both purposes (see the FIXED comment in
    // MutationMetadataEmitter.cs's BuildGraphEdgeCteResolutions).
    internal static string GetPkPropertyName(
        INamedTypeSymbol entityType)
    {
        var pk = IdEmitter.GetScalarProperties(entityType)
            .FirstOrDefault(p =>
                string.Equals(
                    p.Name,
                    "Id",
                    StringComparison.OrdinalIgnoreCase));

        if (pk != null)
            return pk.Name;


        var first = IdEmitter.GetScalarProperties(entityType)
            .FirstOrDefault();

        if (first != null)
            return first.Name;


        throw new InvalidOperationException(
            $"Cannot find primary key property for entity '{entityType.Name}'.");
    }

private static void EmitStaticTryGetEntityId(
        StringBuilder sb)
    {
        sb.AppendLine();

        sb.AppendLine(
            "        public static bool TryGetEntityId(string modelName, out ushort entityId)");

        sb.AppendLine(
            "        {");

        sb.AppendLine(
            "            for (ushort i = 0; i < ModelName.Length; i++)");

        sb.AppendLine(
            "            {");

        sb.AppendLine(
            "                if (global::System.StringComparer.OrdinalIgnoreCase.Equals(ModelName[i][0], modelName))");

        sb.AppendLine(
            "                {");

        sb.AppendLine(
            "                    entityId = i;");

        sb.AppendLine(
            "                    return true;");

        sb.AppendLine(
            "                }");

        sb.AppendLine(
            "            }");

        sb.AppendLine(
            "            entityId = 0;");

        sb.AppendLine(
            "            return false;");

        sb.AppendLine(
            "        }");
    }


private static void EmitModelIdConstants(
        StringBuilder sb,
        List<MappingClassInfo> models)
    {
        sb.AppendLine(
            "        public static class ModelId");

        sb.AppendLine("        {");

        for (int i = 0; i < models.Count; i++)
        {
            sb.AppendLine(
                $"            public const ushort {models[i].ModelType.Name} = {i};");
        }

        sb.AppendLine();

        sb.AppendLine(
            $"            public const ushort Count = {models.Count};");

        sb.AppendLine("        }");

        sb.AppendLine();
    }


    private static void EmitSchemaArray(
    StringBuilder sb,
    List<MappingClassInfo> models)
{
    sb.AppendLine(
        $"        public static readonly string[][] Schema = new string[{models.Count}][]");

    sb.AppendLine("        {");

    foreach (var model in models)
    {
        var schemas =
            model.Definition.Entities
                .Where(e => e.EntityType != null)
                .Select(e =>
                {
                    var entityName = e.EntityType.Name;

                    if (entityName.EndsWith(
                            "Entity",
                            StringComparison.Ordinal))
                    {
                        entityName = entityName.Substring(
                            0,
                            entityName.Length - "Entity".Length);
                    }

                    return FindSchemaByEntityName(
                        entityName,
                        models);
                })
                .Select(x => $"\"{Escape(x ?? string.Empty)}\"")
                .ToList();

        sb.AppendLine(
            $"            new string[] {{ {string.Join(", ", schemas)} }},");
    }

    sb.AppendLine("        };");

    sb.AppendLine();
}

private static string? FindSchemaByEntityName(
    string entityName,
    List<MappingClassInfo> models)
{
    foreach (var model in models)
    {
        foreach (var entity in model.Definition.Entities)
        {
            if (entity.EntityType == null)
            {
                continue;
            }

            var name = entity.EntityType.Name;

            if (name.EndsWith(
                    "Entity",
                    StringComparison.Ordinal))
            {
                name = name.Substring(
                    0,
                    name.Length - "Entity".Length);
            }

            if (!string.Equals(
                    name,
                    entityName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(model.Definition.Schema))
            {
                return model.Definition.Schema;
            }

            if (!string.IsNullOrWhiteSpace(model.Schema))
            {
                return model.Schema;
            }
        }
    }

    return null;
}

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private static void EmitModelNameArray(
        StringBuilder sb,
        List<MappingClassInfo> models)
    {
        sb.AppendLine(
            $"        public static readonly string[][] ModelName = new string[{models.Count}][]");

        sb.AppendLine("        {");

        foreach (var m in models)
        {
            var names = new List<string>
            {
                m.ModelType.Name
            };

            names.AddRange(
                m.CteUpdateMeta
                    .Select(x => x.NavigationAlias)
                    .Distinct());

            sb.AppendLine(
                $"            new string[] {{ {string.Join(", ", names.Select(x => $"\"{x}\""))} }},");
        }

        sb.AppendLine("        };");

        sb.AppendLine();
    }


    private static string ToCamel(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return char.ToLowerInvariant(name[0])
               + name.Substring(1);
    }

    private static void EmitTableArray(
        StringBuilder sb,
        List<MappingClassInfo> models)
    {
        sb.AppendLine(
            $"        public static readonly string[][] Table = new string[{models.Count}][]");

        sb.AppendLine("        {");

        foreach (var model in models)
        {
            var tables =
                model.Definition.Entities
                    .Where(e => e.EntityType != null)
                    .Select(e =>
                    {
                        var name = e.EntityType!.Name;

                        if (name.EndsWith(
                                "Entity",
                                StringComparison.Ordinal))
                        {
                            name =
                                name.Substring(
                                    0,
                                    name.Length - "Entity".Length);
                        }

                        return name;
                    })
                    .ToList();

            sb.AppendLine(
                $"            new string[] {{ {string.Join(", ", tables.Select(x => $"\"{Escape(x)}\""))} }},");
        }

        sb.AppendLine("        };");
        sb.AppendLine();
    }


    // ---------------------------------------------------------------
    // FIXED: this used to build ColumnName purely from
    // IdEmitter.GetScalarProperties — raw declaration order, PK not
    // guaranteed first, no entityGraph-derived FK columns appended.
    // EmitFieldToColumnArray's indices (now ColumnIdResolver.ResolveId,
    // i.e. GetFullColumnOrder-backed) are meant to index INTO this same
    // ColumnName array per model. If ColumnName kept using
    // GetScalarProperties ordering while FieldToColumn used
    // GetFullColumnOrder ordering, the two arrays would disagree — the
    // exact "hardcoded/duplicated column scheme" problem this whole pass
    // of fixes exists to close. Now both use GetFullColumnOrder for the
    // SAME entity, so a FieldToColumn index always lands on the matching
    // name in ColumnName.
    // ---------------------------------------------------------------
    private static void EmitColumnNameArray(
        StringBuilder sb,
        List<MappingClassInfo> models,
        ImmutableArray<MappingClassInfo> allMappings,
        List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph)
    {
        sb.AppendLine(
            $"        public static readonly string[][] ColumnName = new string[{models.Count}][]");

        sb.AppendLine("        {");

        foreach (var m in models)
        {
            INamedTypeSymbol typeForColumns;

            if (IsComposite(m))
            {
                var primaryEntity =
                    m.Definition.Entities.FirstOrDefault(k => k.IsPrimary);

                typeForColumns =
                    primaryEntity?.EntityType
                    ?? throw new InvalidOperationException(
                        $"Composite model '{m.ModelType.Name}' has no primary EntityType.");
            }
            else
            {
                typeForColumns =
                    m.EntityType
                    ?? throw new InvalidOperationException(
                        $"Simple model '{m.ModelType.Name}' has no EntityType.");
            }

            var strippedName =
                IdEmitter.StripEntitySuffix(typeForColumns.Name);

            var cols =
                IdEmitter.GetFullColumnOrder(
                    strippedName,
                    allMappings,
                    typeForColumns,
                    entityGraph);

            sb.AppendLine(
                $"            new string[{cols.Count}]");

            sb.AppendLine("            {");

            foreach (var c in cols)
            {
                sb.AppendLine(
                    $"                \"{c}\",");
            }

            sb.AppendLine("            },");
        }

        sb.AppendLine("        };");
        sb.AppendLine();
    }


    private static bool IsComposite(
        MappingClassInfo info)
    {
        return info.Definition.Entities
            .Where(x => x.EntityType != null)
            .Select(x => x.EntityType!.Name)
            .Distinct(StringComparer.Ordinal)
            .Count() > 1;
    }


    private static void EmitFieldNameArray(
        StringBuilder sb,
        List<MappingClassInfo> models)
    {
        sb.AppendLine(
            $"        public static readonly string[][] FieldName = new string[{models.Count}][]");

        sb.AppendLine("        {");


        foreach (var m in models)
        {
            var mappings =
                PlannerEmitter.ComputeFieldMappingsEagerPublic(
                        m,
                        PlannerEmitter.IsCompositeInfo(m))
                    .ToList();


            if (mappings.Count == 0)
            {
                sb.AppendLine(
                    "            System.Array.Empty<string>(),");
                continue;
            }


            sb.AppendLine(
                "            new string[]");

            sb.AppendLine(
                "            {");


            foreach (var mapping in mappings)
            {
                // This is the same name used by FieldMappings.
                sb.AppendLine(
                    $"                \"{ToCamel(mapping.FieldName)}\",");
            }


            sb.AppendLine(
                "            },");
        }


        sb.AppendLine("        };");
        sb.AppendLine();
    }
    
private static void EmitConflictColumnsArray(
    StringBuilder sb,
    List<INamedTypeSymbol> entityTypes,
    ImmutableArray<MappingClassInfo> allMappings)
{
    sb.AppendLine(
        $"        public static readonly ConflictColumn[][] EntityConflictColumns = new ConflictColumn[{entityTypes.Count}][]");

    sb.AppendLine("        {");

    foreach (var entityType in entityTypes)
    {
        var entityName =
            IdEmitter.StripEntitySuffix(entityType.Name);

        var mappings =
            allMappings
                .Where(m =>
                    m.Definition.Entities.Any(e =>
                        e.EntityType != null &&
                        SymbolEqualityComparer.Default.Equals(
                            e.EntityType,
                            entityType)))
                .ToList();

        var emitted =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        sb.AppendLine("            new ConflictColumn[]");
        sb.AppendLine("            {");

        //
        // Explicit Upsert keys that actually belong to THIS entity.
        //
        foreach (var mapping in mappings)
        {
            foreach (var upsertKey in mapping.UpsertKeys)
            {
                if (string.IsNullOrWhiteSpace(upsertKey.Key))
                    continue;

                var entityEntry =
                    mapping.Definition.Entities
                        .FirstOrDefault(e =>
                            e.IsPrimary &&
                            e.EntityType != null &&
                            SymbolEqualityComparer.Default.Equals(
                                e.EntityType,
                                entityType) &&
                            string.Equals(
                                e.ToColumn,
                                upsertKey.Key,
                                StringComparison.OrdinalIgnoreCase));

                if (entityEntry == null)
                    continue;

                if (!emitted.Add(upsertKey.Key))
                    continue;

                sb.AppendLine(
                    $"                new ConflictColumn(" +
                    $"FieldId.{mapping.ModelType!.Name}.{upsertKey.Key}, " +
                    $"ColumnId.{entityName}.{upsertKey.Key}, " +
                    $"\"{upsertKey.Key}\"),");
            }
        }

        //
        // Primary key fallback.
        //
        if (emitted.Count == 0)
        {
            foreach (var mapping in mappings)
            {
                var entity =
                    mapping.Definition.Entities.FirstOrDefault(e =>
                        e.IsPrimary &&
                        e.EntityType != null &&
                        SymbolEqualityComparer.Default.Equals(
                            e.EntityType,
                            entityType));

                if (entity == null ||
                    string.IsNullOrWhiteSpace(entity.ToColumn))
                {
                    continue;
                }

                var column = entity.ToColumn!;

                if (!emitted.Add(column))
                    continue;

                sb.AppendLine(
                    $"                new ConflictColumn(" +
                    $"FieldId.{mapping.ModelType!.Name}.{column}, " +
                    $"ColumnId.{entityName}.{column}, " +
                    $"\"{column}\"),");

                break;
            }
        }

        //
        // Convention fallback (*Key on this entity only)
        //
        if (emitted.Count == 0)
        {
            var scalar =
                IdEmitter.GetScalarProperties(entityType)
                    .FirstOrDefault(p =>
                        p.Name.EndsWith(
                            "Key",
                            StringComparison.OrdinalIgnoreCase));

            if (scalar != null)
            {
                var mapping =
                    mappings.First();

                sb.AppendLine(
                    $"                new ConflictColumn(" +
                    $"FieldId.{mapping.ModelType!.Name}.{scalar.Name}, " +
                    $"ColumnId.{entityName}.{scalar.Name}, " +
                    $"\"{scalar.Name}\"),");
            }
        }

        sb.AppendLine("            },");
    }

    sb.AppendLine("        };");
    sb.AppendLine();
}

private static void EmitCteResolutionsArray(
    StringBuilder sb,
    List<MappingClassInfo> models)
{
    sb.AppendLine(
        $"        public static readonly global::CoffeeBeanery.GraphQL.Core.Runtime.CteResolutionSpec[][] CteResolutions = new global::CoffeeBeanery.GraphQL.Core.Runtime.CteResolutionSpec[{models.Count}][]");

    sb.AppendLine("        {");

    foreach (var m in models)
    {
        if (m.CteUpdateMeta.Count == 0)
        {
            sb.AppendLine(
                "            System.Array.Empty<global::CoffeeBeanery.GraphQL.Core.Runtime.CteResolutionSpec>(),");
            continue;
        }

        sb.AppendLine(
            "            new global::CoffeeBeanery.GraphQL.Core.Runtime.CteResolutionSpec[]");

        sb.AppendLine("            {");

        foreach (var cte in m.CteUpdateMeta)
        {
            var tableAlias =
                cte.NavigationAlias +
                cte.RelatedEntityTypeName;

            sb.AppendLine(
                "                new global::CoffeeBeanery.GraphQL.Core.Runtime.CteResolutionSpec(");

            sb.AppendLine(
                $"                    \"{cte.NavigationAlias}\",");

            sb.AppendLine(
                $"                    \"{cte.ForeignKeyColumn}\",");

            sb.AppendLine(
                $"                    \"{cte.OwningPrimaryKeyColumn}\",");

            // Use the ID already resolved when CteUpdateMeta was built.
            sb.AppendLine(
                $"                    {cte.OwningPrimaryKeyColumnId},");

            sb.AppendLine(
                $"                    \"{tableAlias}\",");

            sb.AppendLine(
                $"                    \"{cte.RelatedSurrogateIdColumn}\",");

            sb.AppendLine(
                $"                    \"{cte.RelatedNaturalKeyColumn}\"");

            sb.AppendLine(
                "                ),");
        }

        sb.AppendLine("            },");
    }

    sb.AppendLine("        };");
    sb.AppendLine();
}

// ---------------------------------------------------------------
// Storage-entity-keyed arrays
// ---------------------------------------------------------------

    private static void EmitEntitySchemaArray(
        StringBuilder sb,
        List<(string EntityName, string Schema)> entities)
    {
        sb.AppendLine(
            "        public static readonly string[] EntitySchema = new string[" +
            entities.Count +
            "]");

        sb.AppendLine("        {");

        foreach (var entity in entities)
        {
            sb.AppendLine(
                $"            \"{Escape(entity.Schema)}\",");
        }

        sb.AppendLine("        };");
        sb.AppendLine();
    }
    
    private static void EmitEntityTableArray(
        StringBuilder sb,
        List<(string EntityName, string Schema)> entities)
    {
        sb.AppendLine(
            $"        public static readonly string[] EntityTable = new string[{entities.Count}]");

        sb.AppendLine("        {");

        foreach (var entity in entities)
        {
            sb.AppendLine(
                $"            \"{Escape(entity.EntityName)}\",");
        }

        sb.AppendLine("        };");
        sb.AppendLine();
    }


    private static void EmitEntityColumnNameArray(
        StringBuilder sb,
        List<INamedTypeSymbol> entityTypes,
        ImmutableArray<MappingClassInfo> allMappings,
        List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge> entityGraph)
    {
        sb.AppendLine(
            "        /// <summary>Indexed by StorageEntityId.* then ColumnId.{EntityName}.*</summary>");

        sb.AppendLine(
            $"        public static readonly string[][] EntityColumnName = new string[{entityTypes.Count}][]");

        sb.AppendLine("        {");

        foreach (var entity in entityTypes)
        {
            var entityName =
                IdEmitter.StripEntitySuffix(entity.Name);

            var columns =
                IdEmitter.GetFullColumnOrder(
                        entityName,
                        allMappings,
                        entity,
                        entityGraph)
                    .ToList();

            sb.AppendLine(
                $"            new string[{columns.Count}]");

            sb.AppendLine("            {");

            foreach (var column in columns)
            {
                sb.AppendLine(
                    $"                \"{column}\",");
            }

            sb.AppendLine("            },");
        }

        sb.AppendLine("        };");
        sb.AppendLine();
    }


// ---------------------------------------------------------------
// GeneratedEntityMetaProvider
// ---------------------------------------------------------------

    private static void EmitGeneratedEntityMetaProvider(
        StringBuilder sb)
    {
        sb.AppendLine(
            "namespace CoffeeBeanery.GraphQL.Core.Runtime");

        sb.AppendLine("{");

        sb.AppendLine(
            "    public sealed class " +
            "GeneratedEntityMetaProvider : global::CoffeeBeanery.GraphQL.Core.Runtime.IEntityMetaProvider");

        sb.AppendLine("    {");

        sb.AppendLine(
            "        public int Count => EntityMeta.Table.Length;");

        sb.AppendLine(
            "        public string[][] ModelName => EntityMeta.ModelName;");

        sb.AppendLine(
            "        public string[][] Table => EntityMeta.Table;");

        sb.AppendLine(
            "        public string[][] Schema => EntityMeta.Schema;");

        sb.AppendLine(
            "        public FieldMapSpec[][] FieldMappings => EntityMeta.FieldMappings;");

        sb.AppendLine(
            "        public string[][] ColumnName => EntityMeta.ColumnName;");

        sb.AppendLine(
            "        public string[][] FieldName => EntityMeta.FieldName;");

        sb.AppendLine(
            "        public ushort[][] FieldToColumn => EntityMeta.FieldToColumn;");

        sb.AppendLine(
            "        public ConflictColumn[][] EntityConflictColumns => EntityMeta.EntityConflictColumns;");;

        sb.AppendLine(
            "        public CteResolutionSpec[][] CteResolutions => EntityMeta.CteResolutions;");

        sb.AppendLine(
            "        public int StorageEntityCount => EntityMeta.EntityTable.Length;");

        sb.AppendLine(
            "        public string[] EntitySchema => EntityMeta.EntitySchema;");

        sb.AppendLine(
            "        public string[] EntityTable => EntityMeta.EntityTable;");

        sb.AppendLine(
            "        public string[][] EntityColumnName => EntityMeta.EntityColumnName;");

        sb.AppendLine();

        sb.AppendLine(
            "        public bool TryGetEntityId(string modelName, out ushort entityId)");

        sb.AppendLine("        {");

        sb.AppendLine(
            "            for (ushort i = 0; i < EntityMeta.ModelName.Length; i++)");

        sb.AppendLine("            {");

        sb.AppendLine(
            "                if (global::System.StringComparer.OrdinalIgnoreCase.Equals(EntityMeta.ModelName[i][0], modelName))");

        sb.AppendLine("                {");

        sb.AppendLine(
            "                    entityId = i;");

        sb.AppendLine(
            "                    return true;");

        sb.AppendLine("                }");

        sb.AppendLine("            }");

        sb.AppendLine(
            "            entityId = 0;");

        sb.AppendLine(
            "            return false;");

        sb.AppendLine("        }");

        sb.AppendLine("    }");

        sb.AppendLine("}");

        sb.AppendLine();
    }


// ---------------------------------------------------------------
// GeneratedPlannerRegistry
// ---------------------------------------------------------------

    private static void EmitGeneratedPlannerRegistry(StringBuilder sb)
    {
        sb.AppendLine("namespace CoffeeBeanery.GraphQL.Core.Runtime");
        sb.AppendLine("{");
        sb.AppendLine("    public sealed class GeneratedPlannerRegistry : global::CoffeeBeanery.GraphQL.Core.Runtime.IPlannerRegistry");
        sb.AppendLine("    {");

        sb.AppendLine(
            "        public void Build(ushort entityId, in global::CoffeeBeanery.GraphQL.Core.Runtime.SelectionIR selection, ref global::CoffeeBeanery.GraphQL.Core.Runtime.QueryPlanBuilder builder, bool isRoot)");

        sb.AppendLine(
            "            => GeneratedPlanners.PlannerRegistry.Build(entityId, selection, ref builder, isRoot);");

        sb.AppendLine();

        sb.AppendLine(
            "        public void BuildMutation(ushort entityId, in global::CoffeeBeanery.GraphQL.Core.Runtime.MutationIR mutation, ref global::CoffeeBeanery.GraphQL.Core.Runtime.MutationPlanBuilder builder)");

        sb.AppendLine(
            "            => global::CoffeeBeanery.GraphQL.Core.Runtime.MutationRuntimePlanner.Build(entityId, mutation, ref builder);");

        sb.AppendLine();

        sb.AppendLine(
            "        public bool IsValidEntity(ushort entityId)");

        sb.AppendLine(
            "            => GeneratedPlanners.PlannerRegistry.IsValidEntity(entityId);");

        sb.AppendLine();

        sb.AppendLine(
            "        public string GetEntityName(ushort entityId)");

        sb.AppendLine(
            "            => GeneratedPlanners.PlannerRegistry.GetEntityName(entityId);");

        sb.AppendLine("    }");
        sb.AppendLine("}");
    }
}