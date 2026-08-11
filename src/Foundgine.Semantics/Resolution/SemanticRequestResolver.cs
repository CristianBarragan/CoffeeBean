using Foundgine.Abstractions;
using Foundgine.Semantics.Query;

namespace Foundgine.Semantics.Resolution;

/// <summary>
/// Resolves a protocol-neutral SemanticRequest against the static semantic
/// model and produces the request SemanticGraph. This is deliberately
/// provider- and protocol-independent.
/// </summary>
public sealed class SemanticRequestResolver
{
    private readonly SemanticModel _model;

    public SemanticRequestResolver(SemanticModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public SemanticGraph Resolve(SemanticRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var root = _model.Get(request.Root);

        if (request.Selections.Count == 0)
            throw InvalidSelection("A semantic request must contain at least one selection.");

        SemanticFilterValidator.Validate(request.Options?.Filter, root, _model);
        ValidateOrdering(request.Options?.EffectiveOrder ?? [], root, request.Selections);

        var graph = new SemanticGraph { Options = request.Options };

        ResolveSelections(root, request.Selections, graph, null, null, isRoot: true);

        return graph;
    }

    private void ResolveSelections(
        SemanticEntity entity,
        IReadOnlyList<SemanticSelection> selections,
        SemanticGraph graph,
        SemanticGraphNode? parent,
        RelationshipId? viaRelationship,
        bool isRoot = false)
    {
        var fields = new List<FieldId>();
        var relationships = new List<(SemanticRelationship Relationship, IReadOnlyList<SemanticSelection> Children)>();
        var relationshipIds = new HashSet<RelationshipId>();

        foreach (var selection in selections)
        {
            if (selection.Field is not null && selection.Relationship is not null)
                throw InvalidSelection("A selection cannot contain both a field and a relationship.");

            if (selection.Field is null && selection.Relationship is null)
                throw InvalidSelection("A selection must contain a field or a relationship.");

            if (selection.Field is { } fieldId)
            {
                if (selection.Children.Count != 0)
                    throw InvalidSelection($"Field '{fieldId}' cannot have child selections.");

                if (!IsDeclaredField(entity, fieldId))
                    throw InvalidSelection($"Entity '{entity.Name}' does not declare field '{fieldId}'.");

                if (!fields.Contains(fieldId))
                    fields.Add(fieldId);

                continue;
            }

            var relationshipId = selection.Relationship!.Value;

            if (!relationshipIds.Add(relationshipId))
                throw InvalidSelection($"Relationship '{relationshipId}' is selected more than once on '{entity.Name}'. Merge repeated selections in the adapter before resolution.");

            var relationship = entity.Relationships.FirstOrDefault(r => r.Id == relationshipId);

            if (relationship is null)
                throw InvalidSelection(
                    $"Entity '{entity.Name}' does not declare relationship '{relationshipId}'.");

            relationships.Add((relationship, selection.Children));
        }

        var node = isRoot
            ? graph.AddRoot(entity.Id, fields)
            : graph.Add(entity.Id, viaRelationship, parent, fields);

        foreach (var (relationship, children) in relationships)
        {
            var target = _model.Get(relationship.Target);

            if (children.Count == 0)
                throw InvalidSelection(
                    $"Relationship '{entity.Name}.{relationship.Name}' requires child selections.");

            ResolveSelections(
                target,
                children,
                graph,
                node,
                relationship.Id);
        }
    }

    private void ValidateOrdering(
        IReadOnlyList<SemanticOrderTerm> terms,
        SemanticEntity root,
        IReadOnlyList<SemanticSelection> selections)
    {
        foreach (var term in terms)
        {
            var entity = root;
            var currentSelections = selections;
            SemanticRelationship? finalRelationship = null;

            foreach (var relationshipId in term.EffectivePath)
            {
                var relationship = entity.Relationships.FirstOrDefault(r => r.Id == relationshipId)
                    ?? throw InvalidSelection(
                        $"Order relationship '{relationshipId}' is not defined on '{entity.Name}'.");

                if (relationship.Cardinality == RelationshipCardinality.Many)
                {
                    if (relationshipId != term.EffectivePath[^1] || !term.IsAggregate)
                    {
                        throw new NotSupportedException(
                            $"Ordering through collection relationship '{entity.Name}.{relationship.Name}' requires an explicit aggregate semantics (COUNT, MIN, or MAX).");
                    }

                    var target = _model.Get(relationship.Target);
                    if (!IsDeclaredField(target, term.Field))
                        throw InvalidSelection($"Aggregate order field '{term.Field}' is not defined on '{target.Name}'.");
                }

                finalRelationship = relationship;

                var aggregateCollectionHop = relationship.Cardinality == RelationshipCardinality.Many &&
                                             term.IsAggregate &&
                                             relationshipId == term.EffectivePath[^1];

                if (!aggregateCollectionHop && !IsRelationshipSelected(currentSelections, relationshipId))
                {
                    throw new InvalidOperationException(
                        $"Order path '{string.Join(".", term.EffectivePath)}' requires relationship '{entity.Name}.{relationship.Name}' to be selected. " +
                        "The provider will not introduce an implicit join solely for ordering.");
                }

                entity = _model.Get(relationship.Target);
                currentSelections = FindRelationshipSelections(currentSelections, relationshipId);
            }

            if (term.IsAggregate)
            {
                if (term.EffectivePath.Count == 0)
                    throw InvalidSelection("Aggregate ordering requires a relationship path.");

                if (finalRelationship is null)
                    throw InvalidSelection($"Aggregate order relationship '{term.EffectivePath[^1]}' is not defined.");

                if (finalRelationship.Cardinality != RelationshipCardinality.Many)
                    throw InvalidSelection("Aggregate ordering is only valid on collection relationships.");
            }
            else if (!IsDeclaredField(entity, term.Field))
            {
                throw InvalidSelection(
                    $"Order field '{term.Field}' is not defined on '{entity.Name}'.");
            }
        }
    }

    private static bool IsDeclaredField(SemanticEntity entity, FieldId fieldId) =>
        entity.Identity.FieldId == fieldId || entity.Fields.Any(f => f.Id == fieldId);

    private static bool IsRelationshipSelected(
        IReadOnlyList<SemanticSelection> selections,
        RelationshipId relationshipId) =>
        selections.Any(s => s.Relationship == relationshipId);

    private static IReadOnlyList<SemanticSelection> FindRelationshipSelections(
        IReadOnlyList<SemanticSelection> selections,
        RelationshipId relationshipId)
    {
        var selection = selections.FirstOrDefault(s => s.Relationship == relationshipId);
        return selection?.Children ?? [];
    }

    private static InvalidOperationException InvalidSelection(string message) =>
        new($"Invalid semantic request: {message}");
}
