using Foundgine.Metadata;

namespace Foundgine.Semantic.Resolution;

/// <summary>
/// Milestone 2: maps ambiguous human language to explicit domain
/// references. Three resolution strategies, one per shape of phrase in
/// the milestone's acceptance examples:
///
/// <list type="bullet">
/// <item><description><see cref="ResolveByIdentity"/> -- "account 10": an explicit identity literal.</description></item>
/// <item><description><see cref="ResolveBySearch"/> -- "Ada Lovelace": free text against a declared <see cref="SearchCapability"/>.</description></item>
/// <item><description><see cref="ResolveByRelationship"/> -- "her checking account": walking a relationship from an already-resolved reference.</description></item>
/// </list>
///
/// Pronoun resolution ("her" -> a prior <see cref="ResolvedReference"/>)
/// is the caller's job -- that's intent parsing, which belongs to
/// Milestone 3, not here.
///
/// Every path is built around one rule: never silently invent an
/// identity. Zero candidates is <see cref="ResolutionOutcome.NotFound"/>,
/// more than one is <see cref="ResolutionOutcome.Ambiguous"/>, and both
/// outcomes still report every <see cref="ResolutionEvidence"/> gathered
/// along the way.
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

    /// <summary>"account 10": resolve by an explicit identity literal.</summary>
    public ResolutionResult ResolveByIdentity(EntityId entityType, string identityLiteral)
    {
        var entity = _model.Get(entityType);
        var matches = _candidates.FindByIdentity(entityType, identityLiteral);

        var evidence = new List<ResolutionEvidence>
        {
            new($"Looked up {entity.Name}.{entity.Identity.Name} = '{identityLiteral}': " +
                $"{matches.Count} match(es).")
        };

        if (matches.Count == 0)
        {
            return ResolutionResult.NotFound(
                $"No {entity.Name} found with {entity.Identity.Name} '{identityLiteral}'.",
                evidence);
        }

        if (matches.Count > 1)
        {
            return ResolutionResult.Ambiguous(
                $"{matches.Count} {entity.Name} records share identity '{identityLiteral}', " +
                "which should never happen for an identity field.",
                evidence);
        }

        var match = matches[0];

        return ResolutionResult.Success(new ResolvedReference(
            entityType,
            match.IdentityValue,
            Confidence: 1.0,
            Reason: $"Explicit identity literal matched {entity.Name}.{entity.Identity.Name} directly.",
            evidence));
    }

    /// <summary>"Ada Lovelace": resolve free text against a declared <see cref="SearchCapability"/>.</summary>
    public ResolutionResult ResolveBySearch(EntityId entityType, string freeText)
    {
        var entity = _model.Get(entityType);

        if (entity.Search is null)
        {
            return ResolutionResult.NotFound(
                $"{entity.Name} has no search capability declared -- it can only be reached " +
                "by explicit identity or relationship, never free text.",
                [new ResolutionEvidence($"{entity.Name} declares no SearchCapability.")]);
        }

        var evidence = new List<ResolutionEvidence>();
        var found = new Dictionary<string, IdentityCandidate>();

        foreach (var fieldId in entity.Search.SearchableFields)
        {
            var fieldName = entity.Fields.FirstOrDefault(f => f.Id == fieldId)?.Name ?? fieldId.ToString();
            var matches = _candidates.FindByField(entityType, fieldId, freeText, entity.Search.Strategy);

            evidence.Add(new ResolutionEvidence(
                $"Searched {entity.Name}.{fieldName} for '{freeText}' using " +
                $"{entity.Search.Strategy} strategy: {matches.Count} match(es)."));

            foreach (var candidate in matches)
                found[candidate.IdentityValue] = candidate;
        }

        if (found.Count == 0)
            return ResolutionResult.NotFound($"No {entity.Name} matched '{freeText}'.", evidence);

        if (found.Count > 1)
        {
            return ResolutionResult.Ambiguous(
                $"{found.Count} {entity.Name} candidates matched '{freeText}'; cannot resolve to one " +
                "without more information.",
                evidence);
        }

        var only = found.Values.Single();
        var confidence = entity.Search.Strategy switch
        {
            SearchStrategy.Exact => 1.0,
            SearchStrategy.Prefix => 0.85,
            SearchStrategy.Fuzzy => 0.7,
            _ => 0.5
        };

        return ResolutionResult.Success(new ResolvedReference(
            entityType,
            only.IdentityValue,
            confidence,
            $"Free text '{freeText}' uniquely matched {entity.Name} '{only.DisplayLabel}' via " +
            $"{entity.Search.Strategy} search.",
            evidence));
    }

    /// <summary>
    /// "her checking account": walk a named relationship from an
    /// already-resolved reference. This only ever narrows by the
    /// relationship itself -- if a phrase carries a qualifier the domain
    /// has no field for (e.g. "checking", when Account has no
    /// account-type field), that qualifier is silently unused rather than
    /// silently invented: the evidence trail says exactly what was
    /// traversed, and if more than one candidate comes back, resolution
    /// reports <see cref="ResolutionOutcome.Ambiguous"/> instead of
    /// guessing which one "checking" meant.
    /// </summary>
    public ResolutionResult ResolveByRelationship(ResolvedReference source, string relationshipName)
    {
        var sourceEntity = _model.Get(source.EntityType);
        var relationship = sourceEntity.Relationships.FirstOrDefault(
            r => string.Equals(r.Name, relationshipName, StringComparison.OrdinalIgnoreCase));

        if (relationship is null)
        {
            return ResolutionResult.NotFound(
                $"{sourceEntity.Name} has no relationship named '{relationshipName}'.",
                [new ResolutionEvidence(
                    $"No relationship '{relationshipName}' declared on {sourceEntity.Name}.")]);
        }

        var candidates = _candidates.FindByRelationship(relationship.Id, source.IdentityValue);
        var targetEntity = _model.Get(relationship.Target);

        var evidence = new List<ResolutionEvidence>
        {
            new($"Traversed {sourceEntity.Name}.{relationship.Name} from {sourceEntity.Name} " +
                $"'{source.IdentityValue}': {candidates.Count} candidate(s).")
        };

        if (candidates.Count == 0)
        {
            return ResolutionResult.NotFound(
                $"No {targetEntity.Name} found via {sourceEntity.Name}.{relationship.Name}.",
                evidence);
        }

        if (candidates.Count > 1)
        {
            return ResolutionResult.Ambiguous(
                $"{candidates.Count} {targetEntity.Name} found via " +
                $"{sourceEntity.Name}.{relationship.Name}; cannot resolve to one without more " +
                "information.",
                evidence);
        }

        var only = candidates[0];

        return ResolutionResult.Success(new ResolvedReference(
            relationship.Target,
            only.IdentityValue,
            Confidence: 0.9,
            Reason: $"Uniquely resolved via {sourceEntity.Name}.{relationship.Name}.",
            evidence));
    }
}
