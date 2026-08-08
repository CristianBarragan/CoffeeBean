using System.Collections.Generic;
using Foundgine;
using Foundgine.Metadata;
using FoundationMutation = Foundgine.Planning;
using FoundationMeta = Foundgine.Metadata;

namespace Graphgine.Execution;

/// <summary>
/// Builds Foundation.MutationPlan.MutationOperation values from a
/// MutationIR tree -- the mutation-side counterpart to how
/// PlannerEmitter.BuildQueryNode builds a QueryNode. This is a parallel,
/// independent walker of the same MutationIR + MutationEntityMetadata
/// shapes that MutationRuntimePlanner.BuildCore (Graphgine, the
/// actual live dispatcher ProcessService.MutationProcessAsync calls) uses
/// -- not a port of MutationRuntimePlanner itself, since that method also
/// handles CTE-dependency, graph-merge, and the Materializer/Interceptor/
/// Dematerializer registries, which are explicitly out of scope here (see
/// SCOPE below). MutationRuntimePlanner is untouched by this file.
///
/// (src/Graphgine/Execution/MutationPlannerRuntime.cs is
/// a different, unrelated class with a confusingly similar name -- it is
/// dead code, called from nowhere in this project. Do not confuse it with
/// MutationRuntimePlanner above.)
///
/// SCOPE (matches the deliberate split settled on for CTE-dependency and
/// graph-merge mutations): this only covers simple, single-row,
/// non-interceptor mutations -- the same boundary queries ended up with
/// for composite/graph joins vs. the SQL scan/join case. CTE-dependent
/// writes (a child mutation referencing a parent's generated surrogate id
/// via NavigationAlias + "Key"), graph merges (MutationKind.GraphEdge),
/// and the Materializer/Interceptor/Dematerializer registries are NOT
/// built here -- MutationPlannerRuntime.Build remains the only path for
/// those.
///
/// FIRST PASS / DIRECTIONAL, same spirit as QueryPlanTranslator's remarks:
/// - Kind defaults to Upsert for every EntityMutation -- nothing in
///   MutationIR/MutationEntityMetadata currently distinguishes Create vs
///   Update vs Delete. Confirm/wire that up before relying on this for
///   anything that needs to strictly create-only or delete.
/// - Rows are grouped by (EntityId, StorageEntityId) only, not also by
///   alias the way MutationPlannerRuntime.AddRow does -- Foundation's
///   EntityMutation has no row-identity/alias field, so two distinct rows
///   of the same entity type within one mutation currently collapse into
///   a single EntityMutation with the union of their columns.
/// - Navigation-key fields are skipped (same as MutationPlannerRuntime):
///   they become FK lookups resolved at execution time, not a direct
///   column write -- and for a non-root child entity, the FK value itself
///   would need the CTE-dependency machinery this builder doesn't cover.
/// </summary>
public static class MutationOperationBuilder
{
    public static (
        List<FoundationMutation.MutationOperation> Operations,
        Dictionary<ushort, string?> Values)
        Build(
            in MutationIR node,
            MutationEntityMetadata metadata,
            IMetadataProvider metadataProvider)
    {
        var rows =
            new Dictionary<(ushort EntityId, ushort StorageEntityId), List<FoundationMeta.MutationColumn>>();

        var values = new Dictionary<ushort, string?>();

        Walk(node, metadata, rows, values, metadataProvider);

        var operations = new List<FoundationMutation.MutationOperation>();

        foreach (var row in rows)
        {
            operations.Add(
                new FoundationMutation.EntityMutation(
                    metadataProvider.GetEntity(row.Key.StorageEntityId),
                    FoundationMutation.MutationKind.Upsert,
                    row.Value));
        }

        return (operations, values);
    }

    private static void Walk(
        in MutationIR node,
        MutationEntityMetadata metadata,
        Dictionary<(ushort EntityId, ushort StorageEntityId), List<FoundationMeta.MutationColumn>> rows,
        Dictionary<ushort, string?> values,
        IMetadataProvider metadataProvider)
    {
        foreach (var value in node.Values)
        {
            if (!metadata.TryResolveField(value.FieldId, out var field))
                continue;

            if (field.IsNavigationKey)
                continue;

            values[value.FieldId] = value.RawValue;

            var key = (field.EntityId, field.StorageEntityId);

            if (!rows.TryGetValue(key, out var columns))
            {
                columns = new List<FoundationMeta.MutationColumn>();
                rows[key] = columns;
            }

            if (columns.Exists(c => c.SourceFieldId == field.FieldId))
                continue;

            columns.Add(
                new FoundationMeta.MutationColumn(
                    new FoundationMeta.ColumnReference(
                        metadataProvider.GetEntity(field.StorageEntityId),
                        field.ColumnId),
                    field.FieldId,
                    FoundationMeta.MutationValueKind.Input,
                    field.IsPrimaryKey));
        }

        foreach (var child in node.Children)
        {
            Walk(child, metadata, rows, values, metadataProvider);
        }
    }
}
