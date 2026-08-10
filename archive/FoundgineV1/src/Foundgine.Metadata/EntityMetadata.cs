namespace Foundgine.Metadata;

/// <summary>
/// The domain-facing description of an entity: its identity, its display
/// <see cref="Name"/>, and its columns. Everything upstream of
/// <see cref="Foundgine.Providers"/> — <see cref="Foundgine.Planning"/>,
/// <see cref="Foundgine.Builders"/>, the <see cref="JoinGraph"/> — reasons
/// only about <see cref="Name"/> and never needs to know
/// <see cref="StorageName"/> exists at all.
///
/// <see cref="StorageName"/> is the one place a physical detail is allowed
/// to leak in: the actual table (or vertex label, or collection) this
/// entity is stored as, when that differs from <see cref="Name"/> — e.g.
/// domain entity <c>Customer</c> physically stored as table
/// <c>crm_customer</c>. Only <see cref="Foundgine.Providers.SqlTextTranslator"/>
/// reads it, and only when generating SQL text; everything else keeps
/// using <see cref="Name"/>, including error messages, so a bad intent
/// still fails with a message a domain author recognizes rather than a
/// physical table name they've never seen.
///
/// Defaults to <see cref="Name"/> when null, so entities whose storage
/// name happens to match their domain name (the common case) don't need
/// to say so twice.
/// </summary>
public sealed record EntityMetadata(
    EntityId EntityId,
    string Name,
    IReadOnlyList<ColumnMetadata> Columns,
    string? StorageName = null
)
{
    /// <summary>The physical table/label this entity is stored as.</summary>
    public string EffectiveStorageName => StorageName ?? Name;
}
