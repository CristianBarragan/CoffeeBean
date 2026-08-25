using Foundgine.Abstractions;

namespace Foundgine.Metadata;

/// <summary>
/// Static semantic model metadata. A model is a description of application
/// data; it is not an entity and Foundgine never creates or populates it.
/// </summary>
public sealed record ModelMetadata(
    ModelId Id,
    string Name,
    EntityId? Entity = null);
