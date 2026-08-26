using Foundgine.Semantics;
using Foundgine.Execution;
using Foundgine.Semantics.Authorization;
using Foundgine.Abstractions;
using Foundgine.Semantics.Security.Warrants;
using Foundgine.Semantics.Security.Execution;

namespace Foundgine;

/// <summary>
/// Application-level Foundgine configuration. Provider-specific services
/// remain outside this object and are registered by the provider adapter.
/// </summary>
public sealed class FoundgineOptions
{
    public SemanticModel? Model { get; set; }

    public ISemanticAuthorizationPolicy? AuthorizationPolicy { get; set; }

    /// <summary>
    /// Optional cache for compiled provider plans. Authorization is always evaluated
    /// before a cache lookup, and authorization predicates remain part of the cached plan.
    /// </summary>
    public IProviderPlanCache? PlanCache { get; set; }

    /// <summary>Optional trusted key resolver for signed semantic security warrants.</summary>
    public ISecurityWarrantKeyResolver? WarrantKeyResolver { get; set; }

    /// <summary>Optional trusted issuer expected on incoming warrants.</summary>
    public string? ExpectedWarrantIssuer { get; set; }

    /// <summary>Optional replay store used when executing warrant-backed requests.</summary>
    public ISecurityWarrantReplayStore? WarrantReplayStore { get; set; }

    /// <summary>Canonical engine-side bounds for untrusted semantic request complexity.</summary>
    public SecurityResourceLimits SecurityResourceLimits { get; set; } = new();

    /// <summary>Optional mutation schema and provider for the semantic mutation pipeline.</summary>
    public IMutationSchema? MutationSchema { get; set; }
    public Foundgine.Execution.Mutation.IMutationBatchExecutionProvider? MutationProvider { get; set; }
}
