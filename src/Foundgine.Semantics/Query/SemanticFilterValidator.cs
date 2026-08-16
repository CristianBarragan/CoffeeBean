using Foundgine.Abstractions;

namespace Foundgine.Semantics.Query;

internal static class SemanticFilterValidator
{
    public static void Validate(
        SemanticFilterExpression? filter,
        SemanticEntity entity,
        SemanticModel model)
    {
        if (filter is null) return;
        Visit(filter, entity, model);
    }

    private static void Visit(
        SemanticFilterExpression expression,
        SemanticEntity entity,
        SemanticModel model)
    {
        switch (expression)
        {
            case SemanticFieldFilter field:
                if (!IsDeclaredField(entity, field.Field))
                    throw Invalid($"Entity '{entity.Name}' does not declare filter field '{field.Field}'.");
                if (field.Operator == SemanticFilterOperator.In && field.Value is null)
                    throw Invalid($"IN filter on '{entity.Name}.{field.Field}' requires a value list.");
                break;

            case SemanticRelationshipFilter relationshipFilter:
                var relationship = entity.Relationships.FirstOrDefault(x => x.Id == relationshipFilter.Relationship);
                if (relationship is null)
                    throw Invalid($"Entity '{entity.Name}' does not declare filter relationship '{relationshipFilter.Relationship}'.");

                var target = model.Get(relationship.Target);
                Visit(relationshipFilter.Predicate, target, model);
                break;

            case SemanticAggregateFilter aggregate:
                var aggregateRelationship = entity.Relationships.FirstOrDefault(x => x.Id == aggregate.Relationship);
                if (aggregateRelationship is null)
                    throw Invalid($"Entity '{entity.Name}' does not declare aggregate filter relationship '{aggregate.Relationship}'.");
                if (aggregateRelationship.Cardinality != RelationshipCardinality.Many)
                    throw Invalid("Aggregate filters are only valid on collection relationships.");

                var aggregateTarget = model.Get(aggregateRelationship.Target);
                if (aggregate.Predicate is not null)
                    Visit(aggregate.Predicate, aggregateTarget, model);
                if (aggregate.Aggregate == SemanticFilterAggregate.Count)
                {
                    if (aggregate.Field is not null)
                        throw Invalid("COUNT aggregate filters do not accept a target field.");
                }
                else
                {
                    if (aggregate.Field is null)
                        throw Invalid($"{aggregate.Aggregate} aggregate filters require a target field.");
                    if (!IsDeclaredField(aggregateTarget, aggregate.Field.Value))
                        throw Invalid($"Aggregate filter field '{aggregate.Field}' is not defined on '{aggregateTarget.Name}'.");
                }
                break;

            case SemanticAndFilter andFilter:
                if (andFilter.Expressions.Count == 0)
                    throw Invalid("AND filter cannot be empty.");
                foreach (var child in andFilter.Expressions)
                    Visit(child, entity, model);
                break;

            case SemanticOrFilter orFilter:
                if (orFilter.Expressions.Count == 0)
                    throw Invalid("OR filter cannot be empty.");
                foreach (var child in orFilter.Expressions)
                    Visit(child, entity, model);
                break;

            default:
                throw Invalid($"Unsupported semantic filter '{expression.GetType().Name}'.");
        }
    }

    private static bool IsDeclaredField(SemanticEntity entity, FieldId fieldId) =>
        entity.Identity.FieldId == fieldId || entity.Fields.Any(x => x.Id == fieldId);

    private static InvalidOperationException Invalid(string message) =>
        new($"Invalid semantic filter: {message}");
}
