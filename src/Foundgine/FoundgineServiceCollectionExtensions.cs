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
    /// <summary>
    /// Registers Foundgine with the supplied semantic model and the default allow-all policy.
    /// Applications that require authorization should use the overload that supplies an explicit policy.
    /// </summary>
    public static IServiceCollection AddFoundgine(
        this IServiceCollection services,
        SemanticModel model)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(model);

        return services.AddFoundgine(options =>
        {
            options.Model = model;
            options.AuthorizationPolicy = new AllowAllSemanticAuthorizationPolicy();
        });
    }

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

        if (options.Model is null && options.Metadata is not null)
        {
            var builder = SemanticModelBuilder.FromMetadata(options.Metadata);
            options.SemanticConfiguration?.Invoke(builder);
            options.Model = builder.Build();
        }

        if (options.Model is null)
            throw new InvalidOperationException(
                "Foundgine requires a SemanticModel or structural metadata via UseMetadata().");

        if (options.AuthorizationPolicy is null && options.AuthorizationConfiguration is not null)
        {
            options.AuthorizationPolicy = new ConfiguredSemanticAuthorizationPolicy(
                options.AuthorizationConfiguration,
                options.AuthorizationContext);
        }

        if (options.AuthorizationPolicy is null)
            throw new InvalidOperationException(
                "Foundgine requires an ISemanticAuthorizationPolicy or configured authorization.");

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
