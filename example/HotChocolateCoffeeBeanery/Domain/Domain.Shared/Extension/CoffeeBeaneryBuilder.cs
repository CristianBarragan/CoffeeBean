using CoffeeBeanery.GraphQL.Core.Mapping;
using CoffeeBeanery.GraphQL.Core.Sql;
using CoffeeBeanery.Service;
using Microsoft.Extensions.DependencyInjection;

namespace Domain.Shared.Extension
{
    /// <summary>
    /// Fluent builder passed to the AddCoffeeBeanery callback.
    /// Lets users register mapping sets and override individual maps
    /// without touching the core registration logic.
    /// </summary>
    public sealed class CoffeeBeaneryBuilder
    {
        internal readonly List<IMappingSet> MappingSets = new();
        internal readonly List<Action<NodeMap>> Overrides = new();
        internal readonly List<Action<IServiceCollection>> ServiceHooks = new();

        /// <summary>
        /// Register a mapping set. All mappings in the set are registered
        /// with the NodeMap registry when the service container is built.
        /// </summary>
        public CoffeeBeaneryBuilder AddMappings<TMappingSet>()
            where TMappingSet : IMappingSet, new()
        {
            MappingSets.Add(new TMappingSet());
            return this;
        }

        /// <summary>
        /// Register a mapping set instance directly (useful when the set
        /// needs constructor arguments or is resolved from a factory).
        /// </summary>
        public CoffeeBeaneryBuilder AddMappings(IMappingSet mappingSet)
        {
            MappingSets.Add(mappingSet);
            return this;
        }

        /// <summary>
        /// Apply a post-registration override to a specific model's NodeMap.
        /// Runs after all mapping sets have registered, so it always wins.
        /// Use this to add extra FieldMaps, tweak aliases, or patch anything
        /// the source generator couldn't infer.
        /// </summary>
        public CoffeeBeaneryBuilder OverrideMapping<TModel>(Action<NodeMap> configure)
            where TModel : class
        {
            Overrides.Add(map =>
            {
                if (map.ModelType == typeof(TModel))
                    configure(map);
            });
            return this;
        }

        /// <summary>
        /// Escape hatch: register additional services into the DI container
        /// alongside the core CoffeeBeanery services (e.g. custom contributors).
        /// </summary>
        public CoffeeBeaneryBuilder AddServices(Action<IServiceCollection> configure)
        {
            ServiceHooks.Add(configure);
            return this;
        }

        /// <summary>
        /// Register a query plan contributor that augments plans at runtime.
        /// </summary>
        public CoffeeBeaneryBuilder AddQueryContributor<TContributor>()
            where TContributor : class, IQueryPlanContributor
        {
            ServiceHooks.Add(s => s.AddScoped<IQueryPlanContributor, TContributor>());
            return this;
        }

        /// <summary>
        /// Register a mutation plan contributor that augments plans at runtime.
        /// </summary>
        public CoffeeBeaneryBuilder AddMutationContributor<TContributor>()
            where TContributor : class, IMutationPlanContributor
        {
            ServiceHooks.Add(s => s.AddScoped<IMutationPlanContributor, TContributor>());
            return this;
        }
    }
}