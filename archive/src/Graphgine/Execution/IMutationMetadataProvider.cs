namespace Graphgine.Execution
{
    /// <summary>
    /// Runtime-injectable boundary over the generated mutation metadata
    /// (MutationMetadataRegistry.g.cs). Graphgine itself does not attach
    /// Graphgine.SourceGenerators as an analyzer, so it cannot reference
    /// that generated static class directly -- consuming applications that
    /// do attach the generator get a real implementation of this interface
    /// (see GeneratedMutationMetadataProvider) to inject instead.
    /// </summary>
    public interface IMutationMetadataProvider
    {
        /// <summary>
        /// Resolves the mutation field metadata for a given entity/field
        /// pair. Mirrors MutationEntityMetadata.TryResolveField, throwing
        /// (rather than returning a default) when the field is unknown --
        /// silently proceeding with missing field metadata would produce a
        /// mutation that writes to the wrong column or drops a value.
        /// </summary>
        MutationFieldMetadata ResolveField(ushort entityId, ushort fieldId);
    }
}
