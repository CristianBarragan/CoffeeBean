using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using FoundationMutation = CoffeeBeanery.GraphQL.Core.Foundation.MutationPlan;
using FoundationMeta = CoffeeBeanery.GraphQL.Core.Foundation.Metadata;

namespace CoffeeBeanery.GraphQL.Core.Runtime;

/// <summary>
/// Lowers Foundation's MutationOperation list onto the existing, working
/// MutationPlanBuilder/MutationPlan. Same seam as QueryPlanTranslator:
/// SqlMutationCompiler etc. are untouched and still only ever see a
/// MutationPlan.
///
/// FIRST PASS / DIRECTIONAL:
/// - Foundation's MutationColumn describes the SHAPE of a mutation column
///   (which column, what kind of value it holds) but not the actual
///   literal value -- that only exists at request time. valueResolver
///   supplies it here; wire it up to wherever mutation input values
///   currently get resolved (MutationRuntimePlanner, most likely).
/// - GraphMutation/RelationshipMutation are not translated yet (no
///   AddGraphMerge/dependency-wiring translation attempted) -- stubbed
///   below rather than guessed.
/// - Same EntityId/StorageEntityId conflation caveat as QueryPlanTranslator.
/// </summary>
public static class MutationPlanTranslator
{
    /// <param name="operations">Foundation mutation operations, in write order.</param>
    /// <param name="valueResolver">Given a source field id, returns the raw literal string to write, or null if not supplied.</param>
    public static MutationPlan FromMutationOperations(
        IReadOnlyList<FoundationMutation.MutationOperation> operations,
        Func<ushort, string?> valueResolver)
    {
        var builder = new MutationPlanBuilder();
        var aliasCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var operation in operations)
        {
            switch (operation)
            {
                case FoundationMutation.EntityMutation entityMutation:
                    AddEntityMutation(entityMutation, ref builder, aliasCounts, valueResolver);
                    break;

                case FoundationMutation.GraphMutation:
                case FoundationMutation.RelationshipMutation:
                    // TODO: translate to builder.AddGraphMerge(...) / AddDependency(...)
                    // once the CTE/graph-merge wiring rules are confirmed.
                    break;

                default:
                    throw new NotSupportedException(
                        $"MutationPlanTranslator: unsupported MutationOperation '{operation.GetType().Name}'.");
            }
        }

        return builder.Build();
    }

    private static string NextAlias(Dictionary<string, int> counts, string entityName)
    {
        var count = counts.TryGetValue(entityName, out var c) ? c : 0;
        counts[entityName] = count + 1;
        return count == 0 ? entityName : $"{entityName}{count}";
    }

    private static void AddEntityMutation(
        FoundationMutation.EntityMutation mutation,
        ref MutationPlanBuilder builder,
        Dictionary<string, int> aliasCounts,
        Func<ushort, string?> valueResolver)
    {
        var alias = NextAlias(aliasCounts, mutation.Entity.Name);
        var storageId = mutation.Entity.EntityId.Value;

        var values = ImmutableArray.CreateBuilder<FieldValue>();
        var conflictColumns = ImmutableArray.CreateBuilder<ConflictColumn>();

        foreach (var column in mutation.Columns)
        {
            var columnName = "";

            foreach (var c in mutation.Entity.Columns)
            {
                if (c.Id.Value == column.Column.ColumnId)
                {
                    columnName = c.Name;
                    break;
                }
            }

            if (column.IsPrimaryKey)
            {
                conflictColumns.Add(new ConflictColumn(column.SourceFieldId, column.Column.ColumnId, columnName));
            }

            if (column.ValueKind != FoundationMeta.MutationValueKind.Input &&
                column.ValueKind != FoundationMeta.MutationValueKind.Constant)
            {
                // Generated/Expression columns are the SQL writer's job at
                // execution time, not something written into UpsertRow.Values.
                continue;
            }

            var raw = valueResolver(column.SourceFieldId);

            if (raw is null)
                continue;

            values.Add(new FieldValue(
                storageId,
                column.SourceFieldId,
                column.Column.ColumnId,
                raw));
        }

        // NOTE: same entityId/storageEntityId value used for both -- see
        // class remarks. lookups/schemaOverride/tableOverride are still
        // not populated -- deferred along with CTE-dependency and
        // graph-merge translation (see class remarks).
        builder.AddRow(
            storageId,
            storageId,
            alias,
            values.ToImmutable(),
            conflictColumns: conflictColumns.ToImmutable());
    }
}