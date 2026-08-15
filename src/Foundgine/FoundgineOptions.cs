using Foundgine.Semantics;
using Foundgine.Execution;
using Foundgine.Semantics.Authorization;
using Foundgine.Abstractions;

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

    /// <summary>Optional mutation schema and provider for the semantic mutation pipeline.</summary>
    public IMutationSchema? MutationSchema { get; set; }
    public Foundgine.Execution.Mutation.IMutationBatchExecutionProvider? MutationProvider { get; set; }
}
