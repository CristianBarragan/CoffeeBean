using Foundgine.Abstractions;
using Foundgine.Semantics.Query;

namespace Foundgine.Semantics.Intent;

/// <summary>
/// Compiles human/agent-friendly structured intent into the canonical
/// SemanticRequest. It performs only semantic-name resolution; authorization,
/// planning, and execution remain later stages.
/// </summary>
public sealed class ReadIntentCompiler
{
    private readonly SemanticModel _model;

    public ReadIntentCompiler(SemanticModel model) =>
        _model = model ?? throw new ArgumentNullException(nameof(model));

    public SemanticRequest Compile(ReadIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var root = FindEntity(intent.RootEntity);
        var selections = intent.Selections.Select(selection =>
            CompileSelection(root, selection)).ToArray();

        var filter = intent.Filter is null ? null : CompileFilter(root, intent.Filter);
        var order = intent.Order?.Select(o => CompileOrder(root, o)).ToArray();

        return new SemanticRequest(
            root.Id,
            selections,
            new SemanticQueryOptions(filter, order, intent.Limit, intent.Offset, intent.After),
            intent.Security);
    }

    private SemanticSelection CompileSelection(SemanticEntity entity, ReadSelection selection)
    {
        if ((selection.Field is null) == (selection.Relationship is null))
            throw Invalid("A selection must specify exactly one field or relationship.");

        if (selection.Field is not null)
        {
            var field = FindField(entity, selection.Field);
            if (selection.EffectiveChildren.Count != 0)
                throw Invalid($"Field '{entity.Name}.{field.Name}' cannot have children.");
            return new SemanticSelection(field.Id, null, []);
        }

        var relationship = FindRelationship(entity, selection.Relationship!);
        if (selection.EffectiveChildren.Count == 0)
            throw Invalid($"Relationship '{entity.Name}.{relationship.Name}' requires child selections.");

        var target = _model.Get(relationship.Target);
        return new SemanticSelection(
            null,
            relationship.Id,
            selection.EffectiveChildren.Select(child => CompileSelection(target, child)).ToArray());
    }

    private SemanticFilterExpression CompileFilter(SemanticEntity entity, ReadFilter filter) => filter switch
    {
        ReadFieldFilter field => new SemanticFieldFilter(
            FindField(entity, field.Field).Id,
            field.Operator,
            field.Value),

        ReadRelationshipFilter relationship => new SemanticRelationshipFilter(
            FindRelationship(entity, relationship.Relationship).Id,
            relationship.Quantifier,
            CompileFilter(
                _model.Get(FindRelationship(entity, relationship.Relationship).Target),
                relationship.Predicate)),

        ReadAndFilter andFilter when andFilter.Expressions.Count > 0 =>
            new SemanticAndFilter(andFilter.Expressions.Select(x => CompileFilter(entity, x)).ToArray()),

        ReadOrFilter orFilter when orFilter.Expressions.Count > 0 =>
            new SemanticOrFilter(orFilter.Expressions.Select(x => CompileFilter(entity, x)).ToArray()),

        ReadAndFilter => throw Invalid("AND filter cannot be empty."),
        ReadOrFilter => throw Invalid("OR filter cannot be empty."),
        _ => throw Invalid($"Unsupported read filter '{filter.GetType().Name}'.")
    };

    private SemanticOrderTerm CompileOrder(SemanticEntity root, ReadOrder order)
    {
        var entity = root;
        var path = new List<RelationshipId>();

        foreach (var relationshipName in order.EffectivePath)
        {
            var relationship = FindRelationship(entity, relationshipName);
            path.Add(relationship.Id);
            entity = _model.Get(relationship.Target);
        }

        var fieldId = order.Aggregate == SemanticOrderAggregate.Count
            ? entity.Identity.FieldId
            : FindField(entity, order.Field).Id;

        return new SemanticOrderTerm(
            fieldId,
            order.Direction,
            path,
            order.Aggregate);
    }

    private SemanticEntity FindEntity(string name) =>
        _model.Entities.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw Invalid($"Unknown entity '{name}'.");

    private static SemanticField FindField(SemanticEntity entity, string name)
    {
        var field = entity.Fields.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (field is not null)
            return field;

        if (string.Equals(entity.Identity.Name, name, StringComparison.OrdinalIgnoreCase))
            return new SemanticField(entity.Identity.FieldId, entity.Identity.Name, typeof(object));

        throw Invalid($"Unknown field '{entity.Name}.{name}'.");
    }

    private static SemanticRelationship FindRelationship(SemanticEntity entity, string name) =>
        entity.Relationships.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw Invalid($"Unknown relationship '{entity.Name}.{name}'.");

    private static InvalidOperationException Invalid(string message) =>
        new($"Invalid read intent: {message}");
}
