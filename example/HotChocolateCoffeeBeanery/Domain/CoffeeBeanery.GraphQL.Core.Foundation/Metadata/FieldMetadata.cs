namespace CoffeeBeanery.GraphQL.Core.Foundation.Metadata;
public sealed record FieldMetadata(FieldId Id, string Name, Type ClrType, ColumnReference? Column = null);