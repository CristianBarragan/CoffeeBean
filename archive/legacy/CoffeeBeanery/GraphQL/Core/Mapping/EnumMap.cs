namespace CoffeeBeanery.GraphQL.Core.Mapping
{
    public sealed class EnumMap
    {
        public Dictionary<string, string> FromEnum { get; set; } = new();
        public Dictionary<string, string> ToEnum { get; set; } = new();
    }
}