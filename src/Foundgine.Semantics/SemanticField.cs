using Foundgine.Abstractions;

namespace Foundgine.Semantics;

/// <summary>
/// Domain-facing field. ClrType is retained for compatibility with existing
/// providers; SemanticType is the provider-neutral contract for semantic code.
/// </summary>
public sealed record SemanticField(
    FieldId Id,
    string Name,
    Type ClrType,
    SemanticType? SemanticType = null,
    SemanticFieldCapabilities Capabilities = SemanticFieldCapabilities.Default)
{
    public SemanticType EffectiveSemanticType => SemanticType ?? Foundgine.Semantics.SemanticType.FromClrType(ClrType);

    public bool IsNullable => !ClrType.IsValueType || Nullable.GetUnderlyingType(ClrType) is not null;
}

[Flags]
public enum SemanticFieldCapabilities : byte
{
    None = 0,
    Filterable = 1 << 0,
    Sortable = 1 << 1,
    Selectable = 1 << 2,
    Aggregatable = 1 << 3,
    Writable = 1 << 4,
    Computed = 1 << 5,
    Sensitive = 1 << 6,
    Deprecated = 1 << 7,
    Default = Filterable | Sortable | Selectable | Aggregatable
}
