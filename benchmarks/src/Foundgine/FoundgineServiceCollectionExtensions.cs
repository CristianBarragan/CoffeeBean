using Foundgine.Execution;
using Foundgine.Semantics.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Foundgine;

/// <summary>
/// Dependency-injection registration for the application-facing Foundgine API.
/// Provider adapters register IProviderPlanCompiler and IExecutionProvider.
/// </summary>
public static class FoundgineServiceCollectionExtensions
{
    public static IServiceCollection AddFoundgine(
        this IServiceCollection services,
        Action<FoundgineOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new FoundgineOptions();
        configure(options);

        if (options.Model is null)
            throw new InvalidOperationException("Foundgine requires a SemanticModel.");

        if (options.AuthorizationPolicy is null)
            throw new InvalidOperationException(
                "Foundgine requires an ISemanticAuthorizationPolicy.");

        services.AddSingleton(options);
        services.AddSingleton<IFoundgine, FoundgineEngine>();

        return services;
    }
}
