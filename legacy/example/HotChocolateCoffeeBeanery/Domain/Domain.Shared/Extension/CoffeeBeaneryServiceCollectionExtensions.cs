using CoffeeBeanery.GraphQL.Core.Foundation;
using CoffeeBeanery.GraphQL.Core.Foundation.Metadata;
using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Runtime;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.Service;
using Domain.Model;
using FASTER.core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Domain.Shared.Extension
{
    public static class CoffeeBeaneryServiceCollectionExtensions
    {
        public static IServiceCollection AddCoffeeBeanery<TContext>(
            this IServiceCollection services,
            string postgresConnectionString,
            // Action<CoffeeBeaneryBuilder>? configure = null,
            Action<CoffeeBeaneryOptions>? options = null)
            where TContext : DbContext
        {
            var opts = new CoffeeBeaneryOptions();
            options?.Invoke(opts);

            // ---- Generated metadata ----
            services.AddSingleton<IEntityMetaProvider, GeneratedEntityMetaProvider>();
            services.AddSingleton<IPlannerRegistry, GeneratedPlannerRegistry>();
            services.AddSingleton(AdapterTables.Build());

            // ---- Graph strategy ----
                        services.AddSingleton<IGraphStrategy, ApacheAgeGraphStrategy>();

            // ---- SQL writer ----
                        services.AddSingleton<PostgresSqlWriter>();

            // ---- Database ----
            services.AddSingleton(_ => NpgsqlDataSource.Create(postgresConnectionString));

            // ---- Cache ----
            services.AddSingleton<IFasterKV<string, string>>(_ =>
            {
                var store = new FasterKV<string, string>(
                    128,
                    new LogSettings
                    {
                        LogDevice = Devices.CreateLogDevice(opts.CachePath),
                        ObjectLogDevice = new ManagedLocalStorageDevice(opts.CachePath)
                    });
                store.TakeHybridLogCheckpointAsync(CheckpointType.FoldOver);
                return store;
            });

            // ---- Generated metadata ----
            services.AddSingleton<IEntityMetaProvider, GeneratedEntityMetaProvider>();
            services.AddSingleton<IPlannerRegistry, GeneratedPlannerRegistry>();
            services.AddSingleton(AdapterTables.Build());

            // IMetadataProvider is the Foundation-facing abstraction over the
            // same generated data IEntityMetaProvider exposes above.
            // GeneratedMetadataProvider.Instance forwards to the generated
            // static GeneratedMetadata class, so registering it here doesn't
            // change any behavior -- it just gives DI consumers (ProcessService,
            // Filtering/MutationOperationBuilder overloads) something to
            // resolve instead of reaching for the static class directly.
            services.AddSingleton<IMetadataProvider>(GeneratedMetadataProvider.Instance);

            // ---- SQL writer ----
            services.AddSingleton<PostgresSqlWriter>();

            // ---- Process service ----
            // services.AddScoped(typeof(IProcessService<>), typeof(ProcessService<>));
            
            services.AddScoped<IProcessService<Wrapper>>(sp =>
                new ProcessService<CustomerCustomerEdge, Wrapper>(
                    sp.GetRequiredService<NpgsqlDataSource>(),
                    sp.GetRequiredService<IFasterKV<string, string>>(),
                    sp.GetRequiredService<IEntityMetaProvider>(),
                    sp.GetRequiredService<PostgresSqlWriter>(),
                    sp.GetRequiredService<IPlannerRegistry>(),
                    wrap: edges => new List<Wrapper>
                    {
                        new Wrapper
                        {
                            CustomerCustomerEdge = edges,
                            Model = Model.Model.CustomerCustomerEdge
                        }
                    },
                    metadataProvider: sp.GetRequiredService<IMetadataProvider>()));
            
            return services;
        }
    }

    public sealed class CoffeeBeaneryOptions
    {
        /// <summary>Path for FASTER KV log files.</summary>
        public string CachePath { get; set; } = "C:/database";
    }
}