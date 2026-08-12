using Foundgine.Abstractions;

namespace Foundgine.Semantics.Resolution;

/// <summary>
/// Minimal resolver port from V1. It preserves the proven rule that
/// resolution never invents an identity: zero is NotFound and multiple
/// candidates are Ambiguous.
/// </summary>
public sealed class EntityResolver
{
    private readonly SemanticModel _model;
    private readonly ICandidateSource _candidates;

    public EntityResolver(SemanticModel model, ICandidateSource candidates)
    {
        _model = model;
        _candidates = candidates;
    }

    public ResolutionResult ResolveByIdentity(
        EntityId entityType,
        string identityLiteral)
    {
        var entity = _model.Get(entityType);
        var matches = _candidates.FindByIdentity(entityType, identityLiteral);

        var evidence = new[]
        {
            new ResolutionEvidence(
                $"Looked up {entity.Name}.{entity.Identity.Name} = '{identityLiteral}': {matches.Count} match(es).")
        };

        if (matches.Count == 0)
            return ResolutionResult.NotFound(
                $"No {entity.Name} found with {entity.Identity.Name} '{identityLiteral}'.",
                evidence);

        if (matches.Count > 1)
            return ResolutionResult.Ambiguous(
                $"{matches.Count} {entity.Name} records matched identity '{identityLiteral}'.",
                evidence);

        var match = matches[0];

        return ResolutionResult.Success(new ResolvedReference(
            entityType,
            match.IdentityValue,
            1.0,
            $"Explicit identity matched {entity.Name}.{entity.Identity.Name}.",
            evidence));
    }

    public ResolutionResult ResolveByRelationship(
        ResolvedReference source,
        string relationshipName)
    {
        var sourceEntity = _model.Get(source.EntityType);

        var relationship = sourceEntity.Relationships.FirstOrDefault(
            r => string.Equals(r.Name, relationshipName, StringComparison.OrdinalIgnoreCase));

        if (relationship is null)
            return ResolutionResult.NotFound(
                $"{sourceEntity.Name} has no relationship named '{relationshipName}'.",
                [new ResolutionEvidence(
                    $"No relationship '{relationshipName}' declared on {sourceEntity.Name}.")]);

        var matches = _candidates.FindByRelationship(
            relationship.Id,
            source.IdentityValue);

        var target = _model.Get(relationship.Target);

        var evidence = new[]
        {
            new ResolutionEvidence(
                $"Traversed {sourceEntity.Name}.{relationship.Name}: {matches.Count} candidate(s).")
        };

        if (matches.Count == 0)
            return ResolutionResult.NotFound(
                $"No {target.Name} found via {sourceEntity.Name}.{relationship.Name}.",
                evidence);

        if (matches.Count > 1)
            return ResolutionResult.Ambiguous(
                $"{matches.Count} {target.Name} found via {sourceEntity.Name}.{relationship.Name}.",
                evidence);

        return ResolutionResult.Success(new ResolvedReference(
            relationship.Target,
            matches[0].IdentityValue,
            0.9,
            $"Uniquely resolved via {sourceEntity.Name}.{relationship.Name}.",
            evidence));
    }
}
