using Foundgine.Semantics.Security;

namespace Foundgine.Planning;

/// <summary>
/// Derives the minimum plan-level security contract from the authorized plan
/// shape. Capability-specific requirements can be supplied explicitly and are
/// preserved rather than replaced.
/// </summary>
public static class SecurityInvariantPlanRequirements
{
    public static SemanticPlan Attach(SemanticPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var ids = new HashSet<string>(
            plan.RequiredSecurityInvariants ?? [],
            StringComparer.Ordinal);

        Collect(plan.Root, ids);

        return plan with
        {
            RequiredSecurityInvariants = ids
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static void Collect(SemanticPlanNode node, ISet<string> ids)
    {
        ids.Add(SecurityInvariantIds.AuthorizationRequired);
        ids.Add(SecurityInvariantIds.ParameterizedValues);
        ids.Add(SecurityInvariantIds.PlanCacheContextIsolation);

        if (node.Fields.Count > 0)
            ids.Add(SecurityInvariantIds.FieldVisibility);

        if (node.Children.Count > 0)
            ids.Add(SecurityInvariantIds.RelationshipVisibility);

        if (node.Authorization is not null)
        {
            ids.Add(SecurityInvariantIds.RuntimeAuthorization);
        }

        foreach (var child in node.Children)
            Collect(child, ids);
    }
}
