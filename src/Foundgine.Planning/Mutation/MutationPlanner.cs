using Foundgine.Abstractions;
using Foundgine.Semantics.Query;

namespace Foundgine.Planning.Mutation;

/// <summary>
/// Converts mutation intent into a provider-neutral mutation plan.
/// Physical storage and SQL are deliberately outside this planner.
/// </summary>
public sealed class MutationPlanner
{
    private readonly IMutationSchema _schema;

    public MutationPlanner(IMutationSchema schema) =>
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));

    public MutationPlan Plan(MutationIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var entity = _schema.GetEntity(intent.Entity);

        if (intent.Kind is MutationKind.Update or MutationKind.Delete &&
            intent.Filter is null)
        {
            throw new InvalidOperationException(
                $"Unfiltered {intent.Kind} mutations are not permitted for '{entity.Name}'.");
        }

        if (intent.Kind == MutationKind.Delete && intent.Fields.Count != 0)
            throw new InvalidOperationException("Delete mutations cannot contain field values.");

        if (intent.Kind != MutationKind.Delete && intent.Fields.Count == 0)
            throw new InvalidOperationException(
                $"{intent.Kind} mutations must contain at least one field value.");

        foreach (var field in intent.Fields)
        {
            if (!entity.Columns.Contains(field.Column))
                throw new InvalidOperationException(
                    $"Column '{field.Column.Value}' is not registered on '{entity.Name}'.");
        }

        ValidateFilter(intent.Filter, entity);

        var returnFields = intent.ReturnFields?.ToArray()
            ?? (intent.Kind == MutationKind.Delete
                ? Array.Empty<FieldId>()
                : entity.Fields
                    .Where(f => f.Value is not null)
                    .Select(f => f.Key)
                    .ToArray());

        foreach (var field in returnFields)
        {
            if (!entity.Fields.ContainsKey(field))
                throw new InvalidOperationException(
                    $"Return field '{field.Value}' is not registered on '{entity.Name}'.");
        }

        return new MutationPlan(
            [new MutationOperation(
                entity,
                intent.Kind,
                intent.Fields,
                intent.Filter,
                null,
                returnFields)]);
    }

    public MutationPlan Plan(UpsertIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var entity = _schema.GetEntity(intent.Entity);
        if (intent.Fields.Count == 0)
            throw new InvalidOperationException(
                $"Upserts for '{entity.Name}' must contain at least one field value.");

        foreach (var field in intent.Fields)
        {
            if (!entity.Columns.Contains(field.Column))
                throw new InvalidOperationException(
                    $"Column '{field.Column.Value}' is not registered on '{entity.Name}'.");
        }

        var conflicts = intent.ConflictColumns?.ToArray()
            ?? (entity.PrimaryKeyColumn is { } pk ? [pk] : Array.Empty<ColumnId>());

        if (conflicts.Length == 0)
            throw new InvalidOperationException(
                $"Upsert for '{entity.Name}' requires conflict columns or a primary key.");

        foreach (var column in conflicts)
        {
            if (!entity.Columns.Contains(column))
                throw new InvalidOperationException(
                    $"Conflict column '{column.Value}' is not registered on '{entity.Name}'.");
        }

        var returnFields = intent.ReturnFields?.ToArray()
            ?? entity.Fields
                .Where(f => f.Value is not null)
                .Select(f => f.Key)
                .ToArray();

        foreach (var field in returnFields)
        {
            if (!entity.Fields.ContainsKey(field))
                throw new InvalidOperationException(
                    $"Return field '{field.Value}' is not registered on '{entity.Name}'.");
        }

        return new MutationPlan(
        [
            new MutationOperation(
                entity,
                MutationKind.Upsert,
                intent.Fields,
                null,
                conflicts,
                returnFields)
        ]);
    }

    public MutationBatchPlan Plan(MutationBatchIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (intent.Operations.Count == 0)
            throw new InvalidOperationException("A mutation batch must contain at least one operation.");

        var operations = new List<MutationOperation>(intent.Operations.Count);
        for (var i = 0; i < intent.Operations.Count; i++)
        {
            var source = intent.Operations[i];
            operations.Add(PlanSingle(source));
        }

        return new MutationBatchPlan(operations, BuildDependencies(operations));
    }

    /// <summary>
    /// Flattens a mutation tree into the existing dependency-aware batch model.
    /// A child receives the parent primary-key value through the relationship's
    /// provider-neutral join mapping.
    /// </summary>
    public MutationBatchPlan Plan(NestedMutationIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var intents = new List<IMutationIntent>();
        var bindings = new List<(int ParentIndex, int ChildIndex, MutationRelationshipSchema Relationship)>();

        void Visit(NestedMutationIntent node, int? parentIndex, MutationRelationshipSchema? relationship)
        {
            var index = intents.Count;
            intents.Add(node.Mutation);

            if (parentIndex is { } p)
                bindings.Add((p, index, relationship!));

            foreach (var child in node.Children)
            {
                var parentEntity = node.Mutation.Entity;
                var relationshipMetadata = _schema.GetRelationship(child.Relationship);

                if (relationshipMetadata.Source != parentEntity)
                    throw new InvalidOperationException(
                        $"Relationship '{relationshipMetadata.Name}' belongs to '{relationshipMetadata.Source.Value}', " +
                        $"but nested parent is '{parentEntity.Value}'.");

                if (relationshipMetadata.Target != child.Mutation.Mutation.Entity)
                    throw new InvalidOperationException(
                        $"Relationship '{relationshipMetadata.Name}' targets '{relationshipMetadata.Target.Value}', " +
                        $"but nested mutation targets '{child.Mutation.Mutation.Entity.Value}'.");

                Visit(child.Mutation, index, relationshipMetadata);
            }
        }

        Visit(intent, null, null);

        var operations = new List<MutationOperation>(intents.Count);
        foreach (var source in intents)
        {
            operations.Add(PlanSingle(source));
        }

        foreach (var binding in bindings)
        {
            var parent = operations[binding.ParentIndex];
            var child = operations[binding.ChildIndex];
            var relationship = binding.Relationship;
            var parentColumn = relationship.SourceColumn;
            var childColumn = relationship.TargetColumn;

            if (relationship.Source != parent.Entity.Id)
                throw new InvalidOperationException(
                    $"Relationship '{relationship.Name}' does not reference parent entity '{parent.Entity.Name}'.");

            if (relationship.Target != child.Entity.Id)
                throw new InvalidOperationException(
                    $"Relationship '{relationship.Name}' does not reference child entity '{child.Entity.Name}'.");

            if (relationship.Source == relationship.Target)
                throw new InvalidOperationException(
                    $"Relationship '{relationship.Name}' does not map distinct parent and child entities.");

            var primaryKey = parent.Entity.PrimaryKeyColumn
                ?? throw new InvalidOperationException(
                    $"Parent entity '{parent.Entity.Name}' requires a primary key for nested mutation propagation.");

            if (primaryKey != parentColumn)
                throw new InvalidOperationException(
                    $"Nested mutation relationship '{relationship.Name}' currently requires the parent join column " +
                    $"to be the parent primary key; found column '{parentColumn.Value}'.");

            if (child.Fields.Any(x => x.Column == childColumn && x.Source is null))
                throw new InvalidOperationException(
                    $"Child mutation '{child.Entity.Name}' explicitly supplies relationship column '{childColumn.Value}'. " +
                    "Nested mutation propagation must own that value.");

            var returns = parent.ReturnFields ?? Array.Empty<FieldId>();
            var parentPkField = parent.Entity.Fields.FirstOrDefault(f => f.Value == primaryKey).Key;
            if (parentPkField == default)
                throw new InvalidOperationException(
                    $"Primary key column '{primaryKey.Value}' has no field mapping on '{parent.Entity.Name}'.");

            if (!returns.Contains(parentPkField))
                throw new InvalidOperationException(
                    $"Parent mutation '{parent.Entity.Name}' must return its primary key field '{parentPkField.Value}' for nested propagation.");

            var fields = child.Fields.ToList();
            fields.Add(MutationFieldValue.FromPrevious(
                childColumn,
                binding.ParentIndex,
                parentPkField));
            operations[binding.ChildIndex] = child with { Fields = fields };
        }

        return new MutationBatchPlan(operations, BuildDependencies(operations));
    }

    private MutationOperation PlanSingle(IMutationIntent intent)
    {
        var plan = intent switch
        {
            MutationIntent mutation => Plan(mutation),
            UpsertIntent upsert => Plan(upsert),
            _ => throw new NotSupportedException(
                $"Unsupported mutation intent '{intent.GetType().Name}'.")
        };

        if (plan.Operations.Count != 1)
            throw new InvalidOperationException("Each mutation intent must produce exactly one mutation operation.");

        return plan.Operations[0];
    }

    private static IReadOnlyList<MutationDependency> BuildDependencies(
        IReadOnlyList<MutationOperation> operations)
    {
        var dependencies = new List<MutationDependency>();

        for (var targetIndex = 0; targetIndex < operations.Count; targetIndex++)
        {
            foreach (var field in operations[targetIndex].Fields)
            {
                if (field.Source is null)
                    continue;

                var sourceIndex = field.Source.SourceOperationIndex;
                if (sourceIndex < 0 || sourceIndex >= operations.Count || sourceIndex >= targetIndex)
                    throw new InvalidOperationException(
                        $"Mutation operation {targetIndex} must reference an earlier operation; source {sourceIndex} is invalid.");

                var sourceOperation = operations[sourceIndex];
                var sourceReturns = sourceOperation.ReturnFields ?? Array.Empty<FieldId>();
                if (!sourceReturns.Contains(field.Source.SourceField))
                    throw new InvalidOperationException(
                        $"Mutation operation {targetIndex} references field '{field.Source.SourceField.Value}' " +
                        $"from operation {sourceIndex}, but that field is not returned.");

                dependencies.Add(new MutationDependency(
                    sourceIndex,
                    targetIndex,
                    field.Source.SourceField,
                    field.Column));
            }
        }

        return dependencies;
    }

    private static void ValidateFilter(
        SemanticFilterExpression? filter,
        MutationEntitySchema entity)
    {
        switch (filter)
        {
            case null:
                return;

            case SemanticFieldFilter field:
                if (!entity.Fields.ContainsKey(field.Field))
                    throw new InvalidOperationException(
                        $"Filter field '{field.Field.Value}' is not registered on '{entity.Name}'.");
                return;

            case SemanticAggregateFilter:
                throw new NotSupportedException(
                    "Aggregate relationship filters are not valid mutation targets yet.");

            case SemanticRelationshipFilter:
                throw new NotSupportedException(
                    "Relationship filters are not valid mutation targets yet.");

            case SemanticAndFilter andFilter:
                foreach (var expression in andFilter.Expressions)
                    ValidateFilter(expression, entity);
                return;

            case SemanticOrFilter orFilter:
                foreach (var expression in orFilter.Expressions)
                    ValidateFilter(expression, entity);
                return;

            default:
                throw new NotSupportedException(
                    $"Unsupported mutation filter '{filter.GetType().Name}'.");
        }
    }
}
