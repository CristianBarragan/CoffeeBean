namespace Foundgine.Metadata;
public sealed record FieldMetadata(FieldId Id, string Name, Type ClrType, ColumnReference? Column = null);