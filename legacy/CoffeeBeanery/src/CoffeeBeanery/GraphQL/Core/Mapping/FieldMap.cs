
namespace CoffeeBeanery.GraphQL.Core.Mapping
{
    public sealed class FieldMap
    {
        public string SourceAlias { get; set; } = "";
        public string SourceModel { get; set; } = "";
        public string SourceName { get; set; } = "";

        public string DestinationAlias { get; set; } = "";
        public string DestinationEntity { get; set; } = "";
        public string DestinationName { get; set; } = "";
        
        public Dictionary<string, int> FromEnum { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        public Dictionary<string, int> ToEnum   { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}