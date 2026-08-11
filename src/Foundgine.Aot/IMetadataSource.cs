using Foundgine.Metadata;
using Foundgine.Abstractions;

namespace Foundgine.Aot;

/// <summary>
/// Contract for compile-time metadata output. The runtime engine consumes the
/// resulting metadata; it does not depend on the generator implementation.
/// </summary>
public interface IMetadataSource
{
    IReadOnlyCollection<EntityMetadata> Entities { get; }
    IReadOnlyCollection<RelationshipMetadata> Relationships { get; }
    IReadOnlyCollection<ModelMetadata> Models { get; }
    IReadOnlyCollection<ConnectionMetadata> Connections { get; }
    IReadOnlyCollection<ConversionMetadata> Conversions { get; }
    IReadOnlyCollection<AuthorizationMetadata> Authorizations { get; }
}
