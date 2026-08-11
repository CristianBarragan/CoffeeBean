namespace CoffeeBeanery.GraphQL.Core.Sql
{
    public sealed class LinkKey
    {
        public string From { get; set; } = "";
        public string AliasFrom { get; set; } = "";
        public string FromColumn { get; set; }
        public string To { get; set; } = "";
        public string AliasTo { get; set; } = "";
        public string ToColumn { get; set; }
    }
}