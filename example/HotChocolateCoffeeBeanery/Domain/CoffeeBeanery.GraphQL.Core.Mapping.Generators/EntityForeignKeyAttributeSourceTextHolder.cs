using Microsoft.CodeAnalysis.Text;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators
{
    internal static class EntityForeignKeyAttributeSourceText
    {
        public static readonly SourceText Value = SourceText.From(@"namespace CoffeeBeanery.GraphQL.Core.Mapping
{
    /// <summary>
    /// Compile-time escape hatch for entity navigations the MappingNodeGenerator's
    /// default convention (a ""{NavigationName}Key"" or ""{RelatedEntity}Key"" scalar
    /// sibling property) cannot resolve - e.g. relationships only expressed via
    /// fluent EF configuration in OnModelCreating, or ambiguous multi-navigation
    /// cases like CustomerCustomerRelationship.InnerCustomer / OuterCustomer.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public sealed class EntityForeignKeyAttribute : System.Attribute
    {
        public EntityForeignKeyAttribute(
            System.Type relatedEntityType,
            string foreignKeyProperty,
            string principalKeyProperty,
            string? navigationName = null)
        {
            RelatedEntityType = relatedEntityType;
            ForeignKeyProperty = foreignKeyProperty;
            PrincipalKeyProperty = principalKeyProperty;
            NavigationName = navigationName;
        }

        public System.Type RelatedEntityType { get; }
        public string ForeignKeyProperty { get; }
        public string PrincipalKeyProperty { get; }
        public string? NavigationName { get; }
    }
}
", System.Text.Encoding.UTF8);
    }
}
