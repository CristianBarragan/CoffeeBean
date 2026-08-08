namespace CoffeeBeanery.GraphQL.Core.Mapping
{
    public abstract class BaseMappingRegistration<TModel, TEntity> : IMappingRegistration
        where TModel : class
        where TEntity : class
    {
        protected readonly string Prefix;
        protected readonly string Model;
        protected readonly string RegistrationKey;

        protected BaseMappingRegistration(string alias, string model)
        {
            Prefix = alias;
            Model = model;
            RegistrationKey = string.IsNullOrWhiteSpace(alias)
                ? typeof(TModel).Name
                : $"{alias}{typeof(TModel).Name}";
        }

        protected string A(string name) =>
            string.IsNullOrWhiteSpace(Prefix) ? name : $"{Prefix}{name}";
        
        protected string A(string appendix,string name) =>
            string.IsNullOrWhiteSpace(appendix) ? name : $"{appendix}{name}";
        
        protected string G(string graphName) =>
            string.IsNullOrWhiteSpace(Prefix) ? graphName : $"Graph{Prefix}{graphName}";

        protected bool     IsEntity => true;
        protected bool     IsModel  => true;
        protected bool     IsGraph  => false;
        
        protected EnumMap? EnumMap  => null;

        protected abstract NodeMap BuildMap();

        public void Register()
        {
            var map      = BuildMap();
            map.IsEntity = IsEntity;
            map.IsModel  = IsModel;
            map.IsGraph  = IsGraph;
            map.Prefix = Prefix;
            map.Alias    = RegistrationKey;
            map.ModelName = string.IsNullOrEmpty(Prefix) ? Model : Prefix;

            MappingRegistry.Register(typeof(TModel), typeof(TEntity), map, RegistrationKey);
        }
    }

    public abstract class BaseModelMappingRegistration<TModel> : IMappingRegistration
        where TModel : class
    {
        protected readonly string Prefix;
        
        protected readonly string Model;
        
        protected readonly string RegistrationKey;
        
        protected virtual EnumMap? EnumMap => null;

        protected abstract NodeMap BuildMap();

        protected BaseModelMappingRegistration(string alias, string model)
        {
            Prefix = alias;
            Model = model;
            RegistrationKey = string.IsNullOrWhiteSpace(alias)
                ? typeof(TModel).Name
                : $"{alias}{typeof(TModel).Name}";
        }

        protected string A(string name) =>
            string.IsNullOrWhiteSpace(Prefix) ? name : $"{Prefix}{name}";
        
        protected string A(string appendix,string name) =>
            string.IsNullOrWhiteSpace(appendix) ? name : $"{appendix}{name}";
        
        protected string G(string graphName) =>
            string.IsNullOrWhiteSpace(Prefix) ? graphName : $"Graph{Prefix}{graphName}";

        public void Register()
        {
            var map      = BuildMap();
            map.IsModel  = true;
            map.IsEntity = false;
            map.Prefix = Prefix;
            map.Alias    = RegistrationKey;
            map.ModelName = string.IsNullOrEmpty(Prefix) ? Model : Prefix;

            MappingRegistry.Register(typeof(TModel), entityType: null, map, RegistrationKey);
        }
    }
}