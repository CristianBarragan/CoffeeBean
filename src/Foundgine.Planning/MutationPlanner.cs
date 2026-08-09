using Foundgine.Builders;
using Foundgine.Metadata;

namespace Foundgine.Planning;

/// <summary>
/// Turns a <see cref="MutationIntent"/> into a <see cref="MutationPlan"/> by
/// consulting <see cref="Foundgine.Metadata"/> — never by hardcoding
/// domain-specific rules. The mutation counterpart of <see cref="QueryPlanner"/>:
/// same discipline, it does not contain <c>if Customer then ...</c>, only
/// "does entity X exist" / "does entity X have column Y".
///
/// Unlike <see cref="QueryPlanner"/>, this has no <see cref="JoinGraph"/> to
/// consult — a single <see cref="MutationIntent"/> targets exactly one
/// entity's columns (plus, for Update/Delete, a <see cref="FilterExpression"/>
/// against that same entity). Composing several entities' worth of writes
/// into one atomic unit (e.g. Customer -> Account -> Transaction, all
/// created together) is a caller-level concern: plan each
/// <see cref="MutationIntent"/> separately and combine every resulting
/// <see cref="MutationPlan"/>'s <see cref="MutationPlan.Operations"/> into a
/// single provider-level mutation plan with one operation per entity — the
/// execution provider is only ever handed one plan covering everything it
/// needs to do, the same way a query's execution provider is.
/// </summary>
public sealed class MutationPlanner
{
    private readonly MetadataRegistry _metadata;

    public MutationPlanner(MetadataRegistry metadata)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public MutationPlan Plan(MutationIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var entity = GetEntityOrThrow(intent.Entity);

        if (intent.Kind == MutationKind.Delete && intent.Fields.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot plan a {MutationKind.Delete} on '{entity.Name}' with field values: " +
                $"a {MutationKind.Delete} identifies rows via {nameof(MutationIntent.Filter)} " +
                "only — it never writes columns.");
        }

        if (intent.Kind != MutationKind.Delete && intent.Fields.Count == 0)
        {
            throw new InvalidOperationException(
                $"Cannot plan a {intent.Kind} on '{entity.Name}' with no field values: " +
                $"{intent.Kind} must write at least one column.");
        }

        if (intent.Kind is MutationKind.Update or MutationKind.Delete && intent.Filter is null)
        {
            throw new InvalidOperationException(
                $"Cannot plan an unfiltered {intent.Kind} on '{entity.Name}': " +
                $"{nameof(MutationIntent.Filter)} must identify which row(s) to target. " +
                "Foundgine never mutates every row by accident.");
        }

        ValidateFilterTargetsEntity(intent.Filter, entity);

        var columns = intent.Fields
            .Select(field => BuildColumn(entity, field))
            .ToArray();

        var operation = new EntityMutation(entity, intent.Kind, columns, intent.Filter);

        return new MutationPlan(new MutationOperation[] { operation });
    }

    private static MutationColumn BuildColumn(EntityMetadata entity, MutationFieldValue field)
    {
        var column = FindColumnOrThrow(entity, field.ColumnId);
        var isPrimaryKey = IsConventionalPrimaryKey(column);

        return new MutationColumn(
            new ColumnReference(entity, field.ColumnId),
            field.ColumnId,
            MutationValueKind.Input,
            isPrimaryKey,
            field.Value);
    }

    /// <summary>
    /// Foundgine's metadata has no explicit primary-key flag yet (see
    /// docs/CURRENT-STATUS.md's "What is incomplete"); by the same "Id"
    /// convention every sample and E2E test already uses (Customer.Id,
    /// Account.Id, ...), the column named "Id" is treated as the primary
    /// key. This is informational on <see cref="MutationColumn.IsPrimaryKey"/>
    /// today — every write still requires an explicit
    /// <see cref="MutationIntent.Filter"/> for Update/Delete rather than one
    /// being inferred from it — until a real primary-key concept lands in
    /// <see cref="EntityMetadata"/>.
    /// </summary>
    private static bool IsConventionalPrimaryKey(ColumnMetadata column) =>
        string.Equals(column.Name, "Id", StringComparison.Ordinal);

    private static void ValidateFilterTargetsEntity(FilterExpression? filter, EntityMetadata entity)
    {
        switch (filter)
        {
            case null:
                return;

            case ComparisonFilter comparison when !Equals(comparison.Column.Entity, entity):
                throw new InvalidOperationException(
                    $"Cannot plan a mutation Filter on '{comparison.Column.Entity.Name}' while " +
                    $"mutating '{entity.Name}': a mutation's Filter may only reference columns " +
                    "on the entity being mutated.");

            case CompositeFilter composite:
                foreach (var operand in composite.Operands)
                    ValidateFilterTargetsEntity(operand, entity);
                return;
        }
    }

    private static ColumnMetadata FindColumnOrThrow(EntityMetadata entity, ushort columnId)
    {
        foreach (var column in entity.Columns)
        {
            if (column.Id.Value == columnId)
                return column;
        }

        throw new InvalidOperationException(
            $"Cannot plan a mutation over column id {columnId} on entity '{entity.Name}': no " +
            "such column is registered. The planner can only reason about columns that domain " +
            "metadata has described.");
    }

    private EntityMetadata GetEntityOrThrow(EntityId id)
    {
        if (!_metadata.TryGet(id, out var entity))
        {
            throw new InvalidOperationException(
                $"Cannot plan a mutation over entity id {id.Value}: it is not registered in the " +
                $"{nameof(MetadataRegistry)}. The planner can only reason about entities that " +
                "domain metadata has described.");
        }

        return entity;
    }
}
