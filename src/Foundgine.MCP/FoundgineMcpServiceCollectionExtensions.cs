using Foundgine.Execution;
using Foundgine.Intent.Json;
using Microsoft.Extensions.DependencyInjection;
using ExecutionContext = Foundgine.Execution.ExecutionContext;

namespace Foundgine.MCP;

/// <summary>
/// DI registration for the Foundgine MCP adapter. The application remains
/// responsible for configuring the MCP transport and for supplying execution
/// context such as tenant and caller identity.
/// </summary>
public static class FoundgineMcpServiceCollectionExtensions
{
    public static IServiceCollection AddFoundgineMcp(
        this IServiceCollection services,
        Func<ExecutionContext>? contextFactory = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<JsonReadIntentAdapter>();
        services.AddSingleton<FoundgineMcpTools>();
        services.AddSingleton<FoundgineMcpMutationTools>();

        if (contextFactory is not null)
            services.AddSingleton(contextFactory);

        return services;
    }
}
