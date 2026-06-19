namespace CoffeeBeanery.GraphQL.Core.Runtime
{
    public static class _NodeKeys
    {
        // Model-side identity (GraphQL → ModelNode)
        public static string Model(string alias, string model, string column)
            => $"{alias}~{model}~{column}";

        // Entity-side identity (SQL side)
        public static string Entity(string alias, string entity, string column)
            => $"{alias}~{entity}~{column}";

        // Bridge identity (ModelNode ↔ EntityNode mapping)
        public static string Relationship(string alias, string model, string column)
            => $"{alias}~{model}~{column}";
    }
}