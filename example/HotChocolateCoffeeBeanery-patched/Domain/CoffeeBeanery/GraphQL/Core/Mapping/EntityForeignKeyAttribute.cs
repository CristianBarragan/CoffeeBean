// namespace CoffeeBeanery.GraphQL.Core.Mapping;
//
// [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
// public sealed class EntityForeignKeyAttribute : Attribute
// {
//     public Type RelatedEntityType { get; }
//     public string ForeignKeyProperty { get; }
//     public string PrincipalKeyProperty { get; }
//     public string? NavigationName { get; }
//
//     public EntityForeignKeyAttribute(
//         Type relatedEntityType,
//         string foreignKeyProperty,
//         string principalKeyProperty,
//         string? navigationName = null)
//     {
//         RelatedEntityType = relatedEntityType;
//         ForeignKeyProperty = foreignKeyProperty;
//         PrincipalKeyProperty = principalKeyProperty;
//         NavigationName = navigationName;
//     }
// }