namespace Foundgine.Metadata;

public sealed record ColumnMetadata(
    ColumnId Id,
    string Name,
    string? StorageName = null)
{
    public string EffectiveStorageName => StorageName ?? Name;
}
