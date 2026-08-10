using Foundgine.Metadata;

namespace Foundgine.Semantics;

/// <summary>
/// A domain-facing field. It deliberately contains no GraphQL or SQL type.
/// </summary>
public sealed record SemanticField(
    FieldId Id,
    string Name,
    Type ClrType);
