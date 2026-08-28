using Foundgine.Metadata;
using Foundgine.Planning;
using Foundgine.SupplyChain.Application;
using Foundgine.SupplyChain.Infrastructure.Mutations;
using Foundgine.SupplyChain.Infrastructure.Queries;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Foundgine.SupplyChain.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSupplyChainInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IMetadataProvider>(_ => SupplyChainSemanticConfiguration.Metadata);
        services.AddSingleton(SupplyChainSemanticConfiguration.Metadata);
        services.AddSingleton(SupplyChainSemanticConfiguration.Model);
        services.AddSingleton<Planner>();
        services.AddSingleton<SemanticSqlQueryExecutor>();
        services.AddScoped<ISupplyChainQueries, SupplyChainQueryRepository>();
        services.AddScoped<ISupplyChainMutations, SupplyChainMutationRepository>();
        return services;
    }
}
