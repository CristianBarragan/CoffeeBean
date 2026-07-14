using Microsoft.CodeAnalysis.Text;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators
{
    internal static class EntityForeignKeyAttributeSourceText
    {
        public static readonly SourceText Value = SourceText.From(@"namespace CoffeeBeanery.GraphQL.Core.Mapping
{
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class EntityForeignKeyAttribute : System.Attribute
    {
        public EntityForeignKeyAttribute(
            System.Type relatedEntityType,
            string foreignKeyProperty,
            string principalKeyProperty,
            string rawForeignKeyColumn,
            string rawPrincipalKeyColumn,
            string? navigationName = null)
        {
            RelatedEntityType = relatedEntityType;
            ForeignKeyProperty = foreignKeyProperty;
            PrincipalKeyProperty = principalKeyProperty;
            RawForeignKeyColumn = rawForeignKeyColumn;
            RawPrincipalKeyColumn = rawPrincipalKeyColumn;
            NavigationName = navigationName;
        }

        public System.Type RelatedEntityType { get; }
        public string ForeignKeyProperty { get; }
        public string PrincipalKeyProperty { get; }
        public string RawForeignKeyColumn { get; }
        public string RawPrincipalKeyColumn { get; }
        public string? NavigationName { get; }
    }
}
", System.Text.Encoding.UTF8);
    }
}
