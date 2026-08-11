using Foundgine.Abstractions;
namespace Foundgine.Metadata;

/// <summary>
/// Static field metadata, including an optional provider-neutral storage
/// column reference. Providers decide how to translate that reference.
/// </summary>
public sealed record FieldMetadata(
    FieldId Id,
    string Name,
    Type ClrType,
    ColumnReference? Column = null);
