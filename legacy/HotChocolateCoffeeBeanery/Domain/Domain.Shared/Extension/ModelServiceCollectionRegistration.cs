using CoffeeBeanery.CQRS;
using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.Service;
using Domain.Model;
using Domain.Shared.Mapping;
using FASTER.core;
using Npgsql;

namespace Domain.Shared.Extension
{
    public static class ModelServiceCollectionRegistration
    {
        public static IServiceCollection AddCoffeeBeanery(
            this IServiceCollection services,
            string postgresConnectionString)
        {
            services.AddSingleton(sp =>
                new NpgsqlConnection(postgresConnectionString));

            services = AddCache(services);

            services.AddScoped<
                IQuery<ProcessQueryParameters,
                    (List<Wrapper>, int?, int?, int?, int?)>,
                QueryHandler<Wrapper>>();
            
            services.Init<IMappingSet<CustomerMappingType, Domain.Model.Model>, CustomerMappingType, Domain.Model.Model>(
                typeof(ModelMappingRegistration).Assembly);
            SqlNodeBuilder.BuildFromMappings();

            services.AddScoped<IProcessService<Wrapper>, ProcessService<Wrapper>>();

            services.AddScoped<IQueryDispatcher, QueryDispatcher>();
            services.AddScoped<UnitOfWork, UnitOfWork>();
            services.AddScoped<IUnitOfWorkContext, UnitOfWorkContext>();

            return services;
        }

        private static IServiceCollection AddCache(this IServiceCollection services)
        {
            var store = new FasterKV<string, string>(128,
                new LogSettings
                {
                    LogDevice = Devices.CreateLogDevice("C:/database"),
                    ObjectLogDevice = new ManagedLocalStorageDevice("C:/database")
                });
            store.TakeHybridLogCheckpointAsync(CheckpointType.FoldOver);
            services.AddSingleton<IFasterKV<string, string>>(store);
            return services;
        }
    }
}