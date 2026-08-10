namespace Foundgine.Metadata;

/// <summary>
/// Static description of a domain entity. Metadata describes what exists;
/// it does not describe a request and does not know about GraphQL or SQL.
/// </summary>
public sealed record EntityMetadata(
    EntityId EntityId,
    string Name,
    IReadOnlyList<ColumnMetadata> Columns,
    string? StorageName = null)
{
    public string EffectiveStorageName => StorageName ?? Name;
}
