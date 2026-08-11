using Foundgine.Abstractions;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.Query;

namespace Foundgine.Planning.Mutation;

/// <summary>
/// Applies semantic write authorization to a provider-independent mutation
/// plan. It deliberately sits outside MutationPlanner so planning remains a
/// structural concern while policy remains a semantic concern.
/// </summary>
public sealed class MutationAuthorizer
{
    private readonly IMutationSchema _schema;
    private readonly ISemanticAuthorizationPolicy _policy;

    public MutationAuthorizer(IMutationSchema schema, ISemanticAuthorizationPolicy policy)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public MutationPlan Authorize(MutationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        foreach (var operation in plan.Operations)
            Authorize(operation);
        return plan;
    }

    public MutationBatchPlan Authorize(MutationBatchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        foreach (var operation in plan.Operations)
            Authorize(operation);
        return plan;
    }

    private void Authorize(MutationOperation operation)
    {
        var entity = operation.Entity;
        RequireAllowed(
            _policy.GetEntityAccess(entity.Id, AuthorizationOperation.Write),
            $"write entity '{entity.Name}'");

        foreach (var field in operation.Fields)
        {
            var fieldMapping = entity.Fields.FirstOrDefault(pair => pair.Value == field.Column);
            if (fieldMapping.Equals(default(KeyValuePair<FieldId, ColumnId?>)))
                throw new InvalidOperationException(
                    $"Mutation field column '{field.Column.Value}' has no semantic field mapping on '{entity.Name}'.");

            var fieldId = fieldMapping.Key;

            RequireAllowed(
                _policy.GetFieldAccess(entity.Id, fieldId, AuthorizationOperation.Write),
                $"write field '{entity.Name}.{fieldId.Value}'");
        }

        foreach (var fieldId in operation.ReturnFields ?? Array.Empty<FieldId>())
        {
            RequireAllowed(
                _policy.GetFieldAccess(entity.Id, fieldId, AuthorizationOperation.Read),
                $"read return field '{entity.Name}.{fieldId.Value}'");
        }

        ValidateFilter(operation.Filter, entity);
    }

    private void ValidateFilter(SemanticFilterExpression? filter, MutationEntitySchema entity)
    {
        if (filter is null)
            return;

        switch (filter)
        {
            case SemanticFieldFilter field:
                RequireAllowed(
                    _policy.GetFieldAccess(entity.Id, field.Field, AuthorizationOperation.Read),
                    $"filter on field '{entity.Name}.{field.Field.Value}'");
                break;

            case SemanticRelationshipFilter relationship:
                RequireAllowed(
                    _policy.GetRelationshipAccess(entity.Id, relationship.Relationship, AuthorizationOperation.Read),
                    $"filter through relationship '{relationship.Relationship.Value}'");
                var relationshipSchema = _schema.GetRelationship(relationship.Relationship);
                ValidateFilter(relationship.Predicate, _schema.GetEntity(relationshipSchema.Target));
                break;

            case SemanticAggregateFilter aggregate:
                RequireAllowed(
                    _policy.GetRelationshipAccess(entity.Id, aggregate.Relationship, AuthorizationOperation.Read),
                    $"aggregate filter through relationship '{aggregate.Relationship.Value}'");
                if (aggregate.Field is { } aggregateField)
                {
                    var aggregateRelationshipSchema = _schema.GetRelationship(aggregate.Relationship);
                    RequireAllowed(
                        _policy.GetFieldAccess(aggregateRelationshipSchema.Target, aggregateField, AuthorizationOperation.Read),
                        $"aggregate filter field '{aggregateField.Value}'");
                }
                break;

            case SemanticAndFilter and:
                foreach (var expression in and.Expressions)
                    ValidateFilter(expression, entity);
                break;

            case SemanticOrFilter or:
                foreach (var expression in or.Expressions)
                    ValidateFilter(expression, entity);
                break;

            default:
                throw new NotSupportedException(
                    $"Mutation authorization does not support filter '{filter.GetType().Name}'.");
        }
    }

    private static void RequireAllowed(AuthorizationDecision decision, string resource)
    {
        if (!decision.IsAllowed)
            throw new SemanticAuthorizationException($"Access denied for {resource}.");
    }
}
