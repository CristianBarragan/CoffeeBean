using CoffeeBeanery.GraphQL.Core.Sql;

namespace CoffeeBeanery.GraphQL.Core.Mapping
{
    public abstract class BaseMappingRegistration<TModel> : IMappingRegistration
        where TModel : class
    {
        protected readonly string Prefix;
        protected readonly string Model;
        protected readonly string RegistrationKey;

        // Default path: no manual alias needed. RegistrationKey is just the
        // canonical model name; per-instance disambiguation (Customer{1}, Customer{2}, ...)
        // now happens in NodeBuilder via AliasOccurrenceAllocator at tree-build time,
        // not here at registration time.
        protected BaseMappingRegistration()
            : this(alias: null, model: typeof(TModel).Name)
        {
        }

        protected BaseMappingRegistration(string? alias, string? model = null)
        {
            Prefix = alias ?? string.Empty;
            Model = model ?? typeof(TModel).Name;

            RegistrationKey = string.IsNullOrWhiteSpace(Prefix)
                ? typeof(TModel).Name
                : $"{Prefix}{typeof(TModel).Name}";
        }

        protected string A(string name) =>
            string.IsNullOrWhiteSpace(Prefix) ? name : $"{Prefix}{name}";

        protected string G(string graphName) =>
            string.IsNullOrWhiteSpace(Prefix) ? graphName : $"Graph{Prefix}{graphName}";

        public int Id { get; protected set; }

        protected abstract NodeMap BuildMap();
        protected virtual void ApplyGeneratedMappings(NodeMap map) { }

        public void Register()
        {
            var map = BuildMap();
            ApplyGeneratedMappings(map);

            map.IsModel = true;
            map.IsEntity = map.EntityType is not null;
            map.Prefix = Prefix;
            map.Alias = RegistrationKey;

            if (string.IsNullOrEmpty(map.ModelName))
                map.ModelName = Model;

            MappingRegistry.Register(typeof(TModel), map.EntityType, map, RegistrationKey);
        }
    }
}