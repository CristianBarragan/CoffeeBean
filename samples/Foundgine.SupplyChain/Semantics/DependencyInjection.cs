using Foundgine.Semantics.Capabilities;
using Microsoft.Extensions.DependencyInjection;

namespace Foundgine.SupplyChain.Semantics;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the single, authoritative <see cref="SemanticCapabilityRegistry"/>
    /// for the SupplyChain sample so every host (canonical API, PenTest GraphQL/MCP)
    /// resolves the same capability metadata instead of each host re-declaring it.
    /// </summary>
    public static IServiceCollection AddSupplyChainCapabilityRegistry(this IServiceCollection services)
    {
        services.AddSingleton(SupplyChainCapabilities.Registry);
        return services;
    }
}
