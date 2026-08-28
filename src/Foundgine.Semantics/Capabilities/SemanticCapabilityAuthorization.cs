namespace Foundgine.Semantics.Capabilities;

/// <summary>
/// Stable, provider-neutral discriminator for a
/// <see cref="SemanticCapabilityAuthorizationRequirement"/> kind. Adapters
/// should prefer pattern-matching on the concrete requirement type; this
/// enum exists for callers that need a serializable/loggable discriminator
/// without risking a hand-typed string mismatch.
/// </summary>
public enum SemanticCapabilityAuthorizationRequirementKind
{
    Policy,
    Tenant,
    Resource,
    State
}

/// <summary>
/// Provider-neutral declarative authorization requirement attached to a
/// semantic capability.
///
/// This type describes what execution-time authorization must establish.
/// It is deliberately not an authorization decision and contains no
/// request-specific authorization state.
/// </summary>
public abstract record SemanticCapabilityAuthorizationRequirement
{
    private protected SemanticCapabilityAuthorizationRequirement(SemanticCapabilityAuthorizationRequirementKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// Stable provider-neutral discriminator for the requirement kind.
    /// </summary>
    public SemanticCapabilityAuthorizationRequirementKind Kind { get; }
}

/// <summary>
/// Requires an application authorization policy to permit execution.
/// </summary>
public sealed record SemanticCapabilityPolicyRequirement : SemanticCapabilityAuthorizationRequirement
{
    public SemanticCapabilityPolicyRequirement(string policy)
        : base(SemanticCapabilityAuthorizationRequirementKind.Policy)
    {
        if (string.IsNullOrWhiteSpace(policy))
            throw new ArgumentException("Policy cannot be null or whitespace.", nameof(policy));

        Policy = policy;
    }

    public string Policy { get; }
}

/// <summary>
/// Requires execution to occur within the specified tenant context.
/// </summary>
public sealed record SemanticCapabilityTenantRequirement : SemanticCapabilityAuthorizationRequirement
{
    public SemanticCapabilityTenantRequirement(string tenantKey)
        : base(SemanticCapabilityAuthorizationRequirementKind.Tenant)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
            throw new ArgumentException("Tenant key cannot be null or whitespace.", nameof(tenantKey));

        TenantKey = tenantKey;
    }

    public string TenantKey { get; }
}

/// <summary>
/// Requires the target resource to match the specified semantic resource type.
/// </summary>
public sealed record SemanticCapabilityResourceRequirement : SemanticCapabilityAuthorizationRequirement
{
    public SemanticCapabilityResourceRequirement(string resourceType)
        : base(SemanticCapabilityAuthorizationRequirementKind.Resource)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type cannot be null or whitespace.", nameof(resourceType));

        ResourceType = resourceType;
    }

    public string ResourceType { get; }
}

/// <summary>
/// Requires the target resource to satisfy the specified semantic state.
/// </summary>
public sealed record SemanticCapabilityStateRequirement : SemanticCapabilityAuthorizationRequirement
{
    public SemanticCapabilityStateRequirement(string state)
        : base(SemanticCapabilityAuthorizationRequirementKind.State)
    {
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State cannot be null or whitespace.", nameof(state));

        State = state;
    }

    public string State { get; }
}
