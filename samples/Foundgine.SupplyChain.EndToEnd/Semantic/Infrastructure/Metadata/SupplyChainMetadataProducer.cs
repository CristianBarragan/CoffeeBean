using Foundgine.Generated;
using Foundgine.Core.Semantic.Metadata;

namespace Foundgine.SupplyChain.Semantic.Infrastructure.Metadata;

/// <summary>
/// The semantic sample consumes the same AOT metadata producer boundary used
/// by the application CLR model. No parallel structural model is maintained.
/// </summary>
public static class SupplyChainMetadataProducer
{
    public static IMetadataCatalog Catalog => GeneratedMetadata.Registry;
}
