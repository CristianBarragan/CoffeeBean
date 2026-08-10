using Foundgine.Semantic.Resolution;

namespace Foundgine.Semantic.Intent;

/// <summary>
/// Milestone 3: turns a <see cref="ReadIntent"/> into a
/// <see cref="ResolvedReadPlan"/> by driving Milestone 2's
/// <see cref="EntityResolver"/> one hop at a time --
///
/// <list type="number">
/// <item><description>Resolve <see cref="ReadIntent.AnchorPhrase"/> against <see cref="ReadIntent.AnchorEntity"/> by free text (<see cref="EntityResolver.ResolveBySearch"/>).</description></item>
/// <item><description>Walk each name in <see cref="ReadIntent.ThroughRelationships"/> in order (<see cref="EntityResolver.ResolveByRelationship"/>), each one narrowing to a single instance.</description></item>
/// <item><description>Look up <see cref="ReadIntent.TargetRelationship"/> from wherever that chain ends -- this last hop is deliberately *not* resolved to one instance; it's the bulk query the read is actually asking for.</description></item>
/// </list>
///
/// The same rule Milestone 2 enforces applies transitively here: any step
/// that comes back <see cref="ResolutionOutcome.Ambiguous"/> or
/// <see cref="ResolutionOutcome.NotFound"/> stops the whole plan rather
/// than guessing a path forward, and the evidence gathered up to that
/// point is still returned.
/// </summary>
public sealed class ReadPlanner
{
    private readonly SemanticModel _model;
    private readonly EntityResolver _resolver;

    public ReadPlanner(SemanticModel model, EntityResolver resolver)
    {
        _model = model;
        _resolver = resolver;
    }

    public ReadPlanResult Plan(ReadIntent intent)
    {
        var evidence = new List<ResolutionEvidence>();

        var anchor = _resolver.ResolveBySearch(intent.AnchorEntity, intent.AnchorPhrase);
        evidence.AddRange(anchor.Evidence);

        if (anchor.Outcome != ResolutionOutcome.Resolved)
        {
            return ReadPlanResult.Unresolved(
                $"Could not resolve anchor '{intent.AnchorPhrase}': {anchor.UnresolvedReason}",
                evidence);
        }

        var current = anchor.Resolved!;
        var chain = new List<ResolvedReference> { current };

        foreach (var relationshipName in intent.ThroughRelationships)
        {
            var step = _resolver.ResolveByRelationship(current, relationshipName);
            evidence.AddRange(step.Evidence);

            if (step.Outcome != ResolutionOutcome.Resolved)
            {
                var fromName = _model.Get(current.EntityType).Name;

                return ReadPlanResult.Unresolved(
                    $"Could not resolve '{relationshipName}' from {fromName}: {step.UnresolvedReason}",
                    evidence);
            }

            current = step.Resolved!;
            chain.Add(current);
        }

        var currentEntity = _model.Get(current.EntityType);
        var target = currentEntity.Relationships.FirstOrDefault(
            r => string.Equals(r.Name, intent.TargetRelationship, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            return ReadPlanResult.Unresolved(
                $"{currentEntity.Name} has no relationship named '{intent.TargetRelationship}'.",
                evidence);
        }

        var targetEntity = _model.Get(target.Target);

        evidence.Add(new ResolutionEvidence(
            $"Target: query {targetEntity.Name} via {currentEntity.Name}.{target.Name}" +
            (intent.OrderBy is null ? "" : ", ordered") +
            (intent.Limit is null ? "" : $", limited to {intent.Limit}") +
            "."));

        return ReadPlanResult.Success(new ResolvedReadPlan(
            chain,
            target.Target,
            target.Id,
            intent.OrderBy,
            intent.Descending,
            intent.Limit,
            evidence));
    }
}
