using CoffeeBeanery.GraphQL.Core.Mapping.Generators.Model;

namespace CoffeeBeanery.GraphQL.Core.Mapping.Generators.Emit;

/// <summary>
/// FieldId identity is the GraphQL-facing field name (SourceName) — one
/// constant per client-visible field, even when a composite model fans
/// that single field out to multiple destination entities/columns
/// (e.g. Product.Amount -> Contract.Amount AND Transaction.Amount).
/// Do NOT key this by DestinationEntity/DestinationName — that creates
/// a separate constant per destination, which breaks AdapterEmitter
/// (which resolves FieldId purely from the model's C# property name)
/// and silently orphans one-to-many fields in the materializer.
/// </summary>
internal static class FieldIdNameHelper
{
    public static string GetName(FieldInfo field)
    {
        return field.SourceName;
    }
}

internal static class GeneratedIdentifierHelper
{
    public static string Field(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "_";

        if (name.Length == 1)
            return name.ToUpperInvariant();

        return char.ToUpperInvariant(name[0]) +
               name.Substring(1);
    }
}