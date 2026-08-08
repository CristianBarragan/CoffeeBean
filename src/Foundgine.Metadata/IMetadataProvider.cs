namespace Foundgine.Metadata;

/// <summary>
/// Provider-agnostic access to entity/model/join metadata. Mirrors the
/// shape the Mapping.Generators source generator already emits as static
/// methods on the generated `GeneratedMetadata` class (GetEntity/GetModel/
/// GetJoin) -- this interface exists so Runtime code can depend on an
/// abstraction instead of calling that generated static class directly.
///
/// `GeneratedMetadataProvider` (emitted alongside `GeneratedMetadata` by
/// IdEmitter) is the standard implementation: a thin instance wrapper that
/// forwards to the same generated static lookups, so there is exactly one
/// source of truth for the generated data.
/// </summary>
public interface IMetadataProvider
{
    EntityMetadata GetEntity(ushort storageEntityId);

    ModelMetadata GetModel(ushort modelEntityId);

    JoinMetadata? GetJoin(ushort fromEntityId, ushort toEntityId);
}
