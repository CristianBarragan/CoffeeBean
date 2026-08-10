using Foundgine.Metadata;

namespace Foundgine.Aot;

/// <summary>
/// Contract for compile-time metadata output. The runtime engine consumes the
/// resulting metadata; it does not depend on the generator implementation.
/// </summary>
public interface IMetadataSource
{
    IReadOnlyCollection<EntityMetadata> Entities { get; }
    IReadOnlyCollection<RelationshipMetadata> Relationships { get; }
}
