using Foundgine.Semantic.Resolution;

namespace Foundgine.Semantic.Intent;

/// <summary>
/// Milestone 4: turns an <see cref="ActionIntent"/> into a
/// <see cref="ResolvedAction"/> by driving Milestone 2's
/// <see cref="EntityResolver"/>, then validating the requested action and
/// its arguments against the semantic model --
///
/// <list type="number">
/// <item><description>Resolve <see cref="ActionIntent.AnchorPhrase"/> against <see cref="ActionIntent.AnchorEntity"/> by free text (<see cref="EntityResolver.ResolveBySearch"/>).</description></item>
/// <item><description>Walk each name in <see cref="ActionIntent.ThroughRelationships"/> in order (<see cref="EntityResolver.ResolveByRelationship"/>), each one narrowing to a single instance.</description></item>
/// <item><description>If <see cref="ActionIntent.TargetRelationship"/> is set, take the single most recent instance reached from there (<see cref="EntityResolver.ResolveLatestByRelationship"/>) as the action's target; otherwise the target is wherever the chain above ended.</description></item>
/// <item><description>Look up <see cref="ActionIntent.ActionName"/> on the declared <see cref="Foundgine.Semantic.ActionDescriptor"/> list of the entity the chain resolved to <em>before</em> any <see cref="ActionIntent.TargetRelationship"/> traversal -- never anything else, and never on the narrowed target itself. <see cref="ActionIntent.TargetRelationship"/> only picks which instance the action acts on (e.g. "her last transaction"); it does not relocate which entity's action list governs "IssueRefund", which stays declared on Account. This is Milestone 4's "No arbitrary method invocation" rule made concrete.</description></item>
/// <item><description>Validate every supplied <see cref="ActionArgument"/> against the action's declared <see cref="Foundgine.Semantic.ActionParameter"/> list: no undeclared parameter is accepted, every required parameter must be present, and a supplied value's runtime type must match.</description></item>
/// </list>
///
/// The same rule Milestone 2 enforces applies transitively here: any
/// resolution step that comes back <see cref="ResolutionOutcome.Ambiguous"/>
/// or <see cref="ResolutionOutcome.NotFound"/> stops the whole plan rather
/// than guessing a path forward, and so does an action name or argument
/// that doesn't match what the model declares. The evidence gathered up
/// to that point is always returned.
///
/// What this deliberately does *not* do -- see docs/CURRENT-STATUS.md's
/// "what is intentionally not complete" -- is evaluate policy (Milestone
/// 5), preview/approve (Milestone 6), execute, verify, or produce
/// evidence for an audit trail (Milestone 7/8). A <see cref="ResolvedAction"/>
/// is the validated input to those later stages, not a substitute for
/// them.
/// </summary>
public sealed class ActionPlanner
{
    private readonly SemanticModel _model;
    private readonly EntityResolver _resolver;

    public ActionPlanner(SemanticModel model, EntityResolver resolver)
    {
        _model = model;
        _resolver = resolver;
    }

    public ActionPlanResult Plan(ActionIntent intent)
    {
        var evidence = new List<ResolutionEvidence>();

        var anchor = _resolver.ResolveBySearch(intent.AnchorEntity, intent.AnchorPhrase);
        evidence.AddRange(anchor.Evidence);

        if (anchor.Outcome != ResolutionOutcome.Resolved)
        {
            return ActionPlanResult.Unresolved(
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

                return ActionPlanResult.Unresolved(
                    $"Could not resolve '{relationshipName}' from {fromName}: {step.UnresolvedReason}",
                    evidence);
            }

            current = step.Resolved!;
            chain.Add(current);
        }

        // The action is declared on the entity the chain resolved to *before* any
        // TargetRelationship traversal (e.g. IssueRefund lives on Account, not on
        // Transaction). TargetRelationship only narrows which specific instance the
        // action acts on (e.g. "her last transaction") -- it does not change which
        // entity's action list is consulted. Conflating the two meant "refund her
        // last transaction" incorrectly looked for IssueRefund on Transaction, where
        // it was never declared, and failed to resolve even though the action is
        // perfectly valid on the resolved Account.
        var actionEntity = _model.Get(current.EntityType);
        var target = current;

        if (intent.TargetRelationship is not null)
        {
            if (intent.TargetOrderBy is null)
            {
                return ActionPlanResult.Unresolved(
                    $"ActionIntent specifies TargetRelationship '{intent.TargetRelationship}' " +
                    $"without a {nameof(ActionIntent.TargetOrderBy)}: selecting a target via a " +
                    "to-many relationship requires a deterministic ordering, e.g. \"her last " +
                    "transaction\" orders Transaction by date descending.",
                    evidence);
            }

            var latest = _resolver.ResolveLatestByRelationship(
                current, intent.TargetRelationship, intent.TargetOrderBy.Value, intent.TargetDescending);
            evidence.AddRange(latest.Evidence);

            if (latest.Outcome != ResolutionOutcome.Resolved)
            {
                var fromName = _model.Get(current.EntityType).Name;

                return ActionPlanResult.Unresolved(
                    $"Could not resolve target '{intent.TargetRelationship}' from {fromName}: " +
                    $"{latest.UnresolvedReason}",
                    evidence);
            }

            target = latest.Resolved!;
            chain.Add(target);
        }

        var action = actionEntity.Actions.FirstOrDefault(
            a => string.Equals(a.Name, intent.ActionName, StringComparison.OrdinalIgnoreCase));

        if (action is null)
        {
            return ActionPlanResult.Unresolved(
                $"{actionEntity.Name} declares no action named '{intent.ActionName}'. An agent may " +
                "only invoke an action the semantic model explicitly exposes, never an arbitrary " +
                "method.",
                evidence);
        }

        evidence.Add(new ResolutionEvidence($"Selected action {actionEntity.Name}.{action.Name}."));

        var (error, arguments, argumentEvidence) = ValidateArguments(action, intent.Arguments, actionEntity.Name);
        evidence.AddRange(argumentEvidence);

        if (error is not null)
            return ActionPlanResult.Unresolved(error, evidence);

        return ActionPlanResult.Success(new ResolvedAction(chain, target, action, arguments!, evidence));
    }

    /// <summary>
    /// Validates <paramref name="supplied"/> against <paramref name="action"/>'s declared
    /// <see cref="ActionParameter"/> list: rejects any argument the action doesn't declare, requires
    /// every required parameter to be present, and requires a supplied value's runtime type to match
    /// the declared <see cref="ActionParameter.ClrType"/>. Never partially accepts an invalid
    /// argument set -- either every argument is valid, or nothing is executed.
    /// </summary>
    private static (string? Error, IReadOnlyDictionary<string, object?>? Arguments, List<ResolutionEvidence> Evidence)
        ValidateArguments(ActionDescriptor action, IReadOnlyList<ActionArgument> supplied, string entityName)
    {
        var evidence = new List<ResolutionEvidence>();
        var declaredNames = action.Inputs
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var argument in supplied)
        {
            if (!declaredNames.Contains(argument.ParameterName))
            {
                return (
                    $"{entityName}.{action.Name} has no parameter named '{argument.ParameterName}'. " +
                    "Foundgine never accepts an argument an action doesn't declare.",
                    null,
                    evidence);
            }
        }

        var byName = supplied.ToDictionary(
            a => a.ParameterName, a => a.Value, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, object?>();

        foreach (var parameter in action.Inputs)
        {
            var hasValue = byName.TryGetValue(parameter.Name, out var value);

            if (parameter.IsRequired && !hasValue)
            {
                return (
                    $"{entityName}.{action.Name} requires parameter '{parameter.Name}', which was " +
                    "not supplied.",
                    null,
                    evidence);
            }

            if (hasValue && value is not null && !parameter.ClrType.IsInstanceOfType(value))
            {
                return (
                    $"{entityName}.{action.Name} parameter '{parameter.Name}' expects " +
                    $"{parameter.ClrType.Name} but received {value.GetType().Name}.",
                    null,
                    evidence);
            }

            evidence.Add(new ResolutionEvidence(
                hasValue
                    ? $"Argument {parameter.Name} = '{value}' accepted."
                    : $"Optional argument {parameter.Name} not supplied."));

            if (hasValue)
                result[parameter.Name] = value;
        }

        return (null, result, evidence);
    }
}