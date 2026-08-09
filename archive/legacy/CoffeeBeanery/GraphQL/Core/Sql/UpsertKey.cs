namespace CoffeeBeanery.GraphQL.Core.Sql
{
    public sealed class UpsertKey
    {
        public string Entity { get; set; }
        public string Key { get; set; }

        public UpsertKey(string entity, string key)
        {
            Entity = entity;
            Key = key;
        }
    }

}