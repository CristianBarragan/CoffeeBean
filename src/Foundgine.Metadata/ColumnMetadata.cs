namespace Foundgine.Metadata;

/// <summary>
/// The domain-facing description of one column: its identity and its
/// display <see cref="Name"/>. See <see cref="EntityMetadata.StorageName"/>
/// for why <see cref="StorageName"/> exists and who's allowed to read it —
/// the same reasoning applies here at column granularity, e.g. domain
/// column <c>CustomerId</c> physically stored as <c>owner_customer_id</c>.
/// </summary>
public sealed record ColumnMetadata(
    ColumnId Id,
    string Name,
    string? StorageName = null
)
{
    /// <summary>The physical column this is stored as.</summary>
    public string EffectiveStorageName => StorageName ?? Name;
}
