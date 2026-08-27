using Foundgine.Execution;
using Foundgine.Semantics;
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
        SemanticModel model,
        ISemanticAuthorizationPolicy authorizationPolicy)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(authorizationPolicy);

        return services.AddFoundgine(options =>
        {
            options.Model = model;
            options.AuthorizationPolicy = authorizationPolicy;
        });
    }

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
        services.AddSingleton<IFoundgine>(serviceProvider => new FoundgineEngine(
            serviceProvider.GetRequiredService<FoundgineOptions>(),
            serviceProvider.GetRequiredService<IProviderPlanCompiler>(),
            serviceProvider.GetRequiredService<IExecutionProvider>()));

        if (options.MutationSchema is not null && options.MutationProvider is not null)
            services.AddSingleton<IFoundgineMutations>(_ => new FoundgineMutationEngine(
                options.MutationSchema,
                options.AuthorizationPolicy,
                options.MutationProvider,
                options.Model,
                options.WarrantKeyResolver,
                options.ExpectedWarrantIssuer,
                options.WarrantReplayStore));

        return services;
    }
}
