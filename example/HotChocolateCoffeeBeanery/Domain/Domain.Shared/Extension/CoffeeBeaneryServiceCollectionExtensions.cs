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

// using CoffeeBeanery.GraphQL.Core.Mapping;
// using CoffeeBeanery.GraphQL.Core.Runtime;
// using CoffeeBeanery.GraphQL.Core.Sql;
// using CoffeeBeanery.Service;
// using FASTER.core;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.DependencyInjection;
// using Npgsql;
//
// namespace CoffeeBeanery.GraphQL.Core.Extensions
// {
//     public static class CoffeeBeaneryServiceCollectionExtensions
//     {
//         /// <summary>
//         /// Registers all CoffeeBeanery services.
//         ///
//         /// Minimal setup (generated mappings only):
//         ///   services.AddCoffeeBeanery&lt;BankingDbContext&gt;(connectionString);
//         ///
//         /// With custom mappings and overrides:
//         ///   services.AddCoffeeBeanery&lt;BankingDbContext&gt;(connectionString, cb =>
//         ///   {
//         ///       cb.AddMappings&lt;CustomerMappingSet&gt;();
//         ///       cb.AddMappings&lt;ProductMappingSet&gt;();
//         ///       cb.OverrideMapping&lt;Customer&gt;(map =>
//         ///       {
//         ///           map.FieldMaps.Add(new FieldMap { ... });
//         ///       });
//         ///       cb.AddQueryContributor&lt;MyCustomContributor&gt;();
//         ///   });
//         /// </summary>
//         public static IServiceCollection AddCoffeeBeanery<TContext>(
//             this IServiceCollection services,
//             string postgresConnectionString,
//             Action<CoffeeBeaneryBuilder>? configure = null,
//             Action<CoffeeBeaneryOptions>? options = null)
//             where TContext : DbContext
//         {
//             var opts = new CoffeeBeaneryOptions();
//             options?.Invoke(opts);
//
//             var builder = new CoffeeBeaneryBuilder();
//             configure?.Invoke(builder);
//
//             // ---- Database ----
//             services.AddSingleton(_ => NpgsqlDataSource.Create(postgresConnectionString));
//
//             // ---- Cache ----
//             services.AddSingleton<IFasterKV<string, string>>(_ =>
//             {
//                 var store = new FasterKV<string, string>(
//                     128,
//                     new LogSettings
//                     {
//                         LogDevice = Devices.CreateLogDevice(opts.CachePath),
//                         ObjectLogDevice = new ManagedLocalStorageDevice(opts.CachePath)
//                     });
//                 store.TakeHybridLogCheckpointAsync(CheckpointType.FoldOver);
//                 return store;
//             });
//
//             // ---- Generated metadata ----
//             services.AddSingleton<IEntityMetaProvider, GeneratedEntityMetaProvider>();
//             services.AddSingleton<IPlannerRegistry, GeneratedPlannerRegistry>();
//
//             // ---- Adapter lookup ----
//             services.AddSingleton<AdapterLookup>(_ =>
//             {
//                 var fieldLinks = Enumerable
//                     .Range(0, EntityId.Count)
//                     .SelectMany(entityId =>
//                         EntityMeta.FieldName[entityId]
//                             .Select((fieldName, fieldId) =>
//                                 ((ushort)entityId, fieldName, (ushort)fieldId)));
//
//                 var entityNameToId = Enumerable
//                     .Range(0, EntityId.Count)
//                     .ToDictionary(e => EntityMeta.Table[e], e => (ushort)e);
//
//                 return AdapterLookup.BuildFromGeneratedMetadata(
//                     AdapterTables.ChildLinks,
//                     fieldLinks,
//                     entityNameToId);
//             });
//
//             // ---- SQL writer ----
//             services.AddSingleton<PostgresSqlWriter>();
//
//             // ---- Process service ----
//             services.AddScoped(typeof(IProcessService<>), typeof(ProcessService<>));
//
//             // ---- User-supplied mapping sets ----
//             // Registered as a singleton list so NodeBuilder (or equivalent)
//             // can resolve them all at startup.
//             services.AddSingleton<IReadOnlyList<IMappingSet>>(_ => builder.MappingSets);
//
//             // ---- User-supplied NodeMap overrides ----
//             // Registered as a singleton list; applied after all mapping sets
//             // have registered their maps.
//             services.AddSingleton<IReadOnlyList<Action<NodeMap>>>(_ => builder.Overrides);
//
//             // ---- User-supplied service hooks ----
//             foreach (var hook in builder.ServiceHooks)
//                 hook(services);
//
//             return services;
//         }
//     }
//
//     public sealed class CoffeeBeaneryOptions
//     {
//         /// <summary>Path for FASTER KV log files.</summary>
//         public string CachePath { get; set; } = "C:/database";
//     }
// }