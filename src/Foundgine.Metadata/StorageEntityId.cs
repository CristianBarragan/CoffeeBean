namespace Foundgine.Metadata;

/// <summary>
/// Identity of a physical storage entity. It is intentionally distinct from
/// EntityId so logical/domain identity cannot accidentally be treated as a
/// database table identity.
/// </summary>
public readonly record struct StorageEntityId(ushort Value);
