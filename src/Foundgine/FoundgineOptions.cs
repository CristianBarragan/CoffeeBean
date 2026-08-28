using Foundgine.Semantics;
using Foundgine.Execution;
using Foundgine.Semantics.Authorization;
using Foundgine.Abstractions;
using Foundgine.Semantics.Security.Warrants;
using Foundgine.Semantics.Security.Execution;
using Foundgine.Metadata;

namespace Foundgine;

/// <summary>
/// Application-level Foundgine configuration. Provider-specific services
/// remain outside this object and are registered by the provider adapter.
/// </summary>
public sealed class FoundgineOptions
{
    /// <summary>
    /// Optional structural metadata source. When supplied, Foundgine discovers
    /// the ordinary semantic model from metadata before applying semantic
    /// configuration.
    /// </summary>
    public IMetadataCatalog? Metadata { get; private set; }

    /// <summary>Optional semantic enrichment applied after structural discovery.</summary>
    public Action<SemanticModelBuilder>? SemanticConfiguration { get; private set; }

    /// <summary>Optional application authorization configuration.</summary>
    public SemanticAuthorizationConfiguration? AuthorizationConfiguration { get; private set; }

    /// <summary>Execution identity consumed by configured authorization rules.</summary>
    public SemanticAuthorizationContext AuthorizationContext { get; private set; } = new();

    public SemanticModel? Model { get; set; }

    public ISemanticAuthorizationPolicy? AuthorizationPolicy { get; set; }

    public FoundgineOptions UseMetadata(IMetadataCatalog metadata)
    {
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        return this;
    }

    public FoundgineOptions ConfigureSemantics(Action<SemanticModelBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        SemanticConfiguration = SemanticConfiguration is null
            ? configure
            : Compose(SemanticConfiguration, configure);
        return this;
    }

    public FoundgineOptions ConfigureAuthorization(
        Action<SemanticAuthorizationConfiguration> configure,
        SemanticAuthorizationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        AuthorizationConfiguration ??= new SemanticAuthorizationConfiguration();
        configure(AuthorizationConfiguration);
        if (context is not null)
            AuthorizationContext = context;
        return this;
    }

    private static Action<SemanticModelBuilder> Compose(
        Action<SemanticModelBuilder> first,
        Action<SemanticModelBuilder> second) => builder =>
    {
        first(builder);
        second(builder);
    };

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
