# Infrastructure boundary

The semantic sample's infrastructure exposes structural metadata through `SupplyChainMetadataProducer`.

The producer currently delegates to `Foundgine.Generated.GeneratedMetadata.Registry`, which is generated directly from
the CLR domain declarations in `Domain/Domain.cs`.

There is deliberately no hand-maintained `SupplyChainStructuralModels` graph. Replacing the AOT producer with an
EF/database/other metadata producer should not require changes to semantic configuration.
