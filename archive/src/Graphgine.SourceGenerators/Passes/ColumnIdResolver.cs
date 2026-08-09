using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Graphgine.SourceGenerators.Model;
using Graphgine.SourceGenerators.Passes;
using Microsoft.CodeAnalysis;

namespace Graphgine.SourceGenerators.Emit;

internal static class ColumnIdResolver
{
    // ---------------------------------------------------------------
    // FIXED: Resolve/ResolveId used to call IdEmitter.GetScalarProperties
    // directly — raw Roslyn member-declaration order, filtered to scalar
    // types, with NO primary-key-first insertion and NO entityGraph-derived
    // FK-column append. IdEmitter.EmitColumnIds (which generates the
    // ColumnId.* constants) computes column indices via
    // IdEmitter.GetFullColumnOrder instead — a DIFFERENT ordering
    // (FieldMaps-derived names alphabetized, PK inserted at index 0 if
    // missing, then entityGraph dependent-side FK columns appended).
    //
    // Every caller of this resolver (PlannerEmitter's join/column emission,
    // MutationMetadataEmitter's field metadata emission) was baking column
    // index literals into generated code using the WRONG ordering — one
    // that disagreed with ColumnId.* and, transitively, with anything else
    // (e.g. a runtime IEntityMetaProvider) that was built from
    // GetFullColumnOrder. This is exactly the class of bug
    // GetFullColumnOrder's own doc comment warns about: "computing the
    // ordering independently in each place is exactly what caused ColumnId
    // constants to disagree with the runtime name array previously."
    //
    // Both methods now require the same mappings/entityGraph context
    // GetFullColumnOrder needs, and route through it — making this the
    // ONLY place in the generator that assigns a numeric column index to
    // an entity's column, for every consumer (joins, columns, CTE
    // resolution). No other method should call GetScalarProperties to
    // compute a column INDEX; GetScalarProperties remains valid for other
    // uses (e.g. counting/filtering scalar properties), just not indexing.
    // ---------------------------------------------------------------

    public static ushort Resolve(
        INamedTypeSymbol entityType,
        string columnName,
        ImmutableArray<MappingClassInfo> mappings,
        List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge>? entityGraph = null)
    {
        var strippedName =
            IdEmitter.StripEntitySuffix(entityType.Name);

        var columns =
            IdEmitter.GetFullColumnOrder(
                strippedName,
                mappings,
                entityType,
                entityGraph);

        var index =
            columns.FindIndex(c =>
                string.Equals(
                    c,
                    columnName,
                    StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Column '{columnName}' was not found on entity '{entityType.Name}' " +
                $"via GetFullColumnOrder. Known columns: " +
                $"[{string.Join(", ", columns)}]. If this column is a " +
                $"navigation/FK column, confirm entityGraph was passed and " +
                $"contains an edge with this entity as the DependentEntity.");
        }

        return (ushort)index;
    }

    public static INamedTypeSymbol ResolveEntityOrThrow(
        MappingClassInfo info, string storageEntityName, string columnName, FieldInfo target, string modelName)
    {
        var matchedEntity =
            info.Definition.Entities
                .FirstOrDefault(e =>
                    e.EntityType != null &&
                    string.Equals(
                        IdEmitter.StripEntitySuffix(e.EntityType.Name),
                        storageEntityName,
                        StringComparison.OrdinalIgnoreCase));

        if (matchedEntity?.EntityType == null)
        {
            throw new InvalidOperationException(
                $"Model '{modelName}' has no registered entity matching storage entity '{storageEntityName}' " +
                $"while resolving column '{columnName}' for field '{target.SourceName}'.");
        }

        return matchedEntity.EntityType;
    }

    public static ushort ResolveId(
        INamedTypeSymbol entityType,
        string columnName,
        ImmutableArray<MappingClassInfo> mappings,
        List<FluentEntityNavigationConvention.EntityForeignKeyGraph.Edge>? entityGraph = null)
    {
        var strippedName =
            IdEmitter.StripEntitySuffix(entityType.Name);

        var columns =
            IdEmitter.GetFullColumnOrder(
                strippedName,
                mappings,
                entityType,
                entityGraph);

        var index =
            columns.FindIndex(c =>
                string.Equals(
                    c,
                    columnName,
                    StringComparison.OrdinalIgnoreCase));

        return index >= 0
            ? (ushort)index
            : ushort.MaxValue;
    }
}