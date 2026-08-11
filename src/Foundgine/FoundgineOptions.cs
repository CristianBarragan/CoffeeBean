using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;

namespace Foundgine;

/// <summary>
/// Application-level Foundgine configuration. Provider-specific services
/// remain outside this object and are registered by the provider adapter.
/// </summary>
public sealed class FoundgineOptions
{
    public SemanticModel? Model { get; set; }

    public ISemanticAuthorizationPolicy? AuthorizationPolicy { get; set; }
}
