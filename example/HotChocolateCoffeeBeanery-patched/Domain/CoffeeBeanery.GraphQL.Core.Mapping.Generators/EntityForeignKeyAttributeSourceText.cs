using Microsoft.CodeAnalysis.Text;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators
{
    /// <summary>
    /// Two attribute types are emitted here:
    ///
    /// 1. EntityForeignKeyAttribute - per-class, kept for backward compatibility /
    ///    any existing consumers that key off individual class decoration. Not
    ///    used by the graph-building path anymore.
    ///
    /// 2. EntityForeignKeyGraphAttribute - a single ASSEMBLY-level attribute
    ///    carrying the entire derived FK edge list as one delimited string.
    ///    This is what EntityForeignKeyGraph.Build actually reads. Using one
    ///    assembly attribute instead of one per-class attribute means:
    ///      - no [partial] requirement on entity classes
    ///      - no need to walk every named type in the compilation to find edges
    ///      - a single deserialize step instead of an attribute scan per type
    ///
    ///    Format: edges are joined with ';', each edge is
    ///    "{DependentTypeFullName}|{FkColumn}|{PrincipalTypeFullName}|{PkColumn}".
    ///    Plain delimited text is used instead of JSON to avoid depending on
    ///    System.Text.Json's generic Serialize&lt;T&gt; overload, which isn't
    ///    reliably available on the netstandard2.0 target analyzer projects
    ///    commonly use.
    /// </summary>
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

    [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public sealed class EntityForeignKeyGraphAttribute : System.Attribute
    {
        public EntityForeignKeyGraphAttribute(string serializedEdges)
        {
            SerializedEdges = serializedEdges;
        }

        public string SerializedEdges { get; }
    }
}
", System.Text.Encoding.UTF8);
    }
}