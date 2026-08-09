
namespace CoffeeBeanery.GraphQL.Core.Mapping
{
    public sealed class MappingBuilder<TModel>
    {
        private readonly Action<MappingBuilder<TModel>> _configure;

        public MappingBuilder(Action<MappingBuilder<TModel>> configure)
        {
            _configure = configure;
        }

        public static MappingBuilder<TModel> Create(Action<MappingBuilder<TModel>> configure)
            => new MappingBuilder<TModel>(configure);

        public NodeMap EntityMap()
            => new NodeMap();

        public void Build()
        {
            _configure(this);
        }
    }
}