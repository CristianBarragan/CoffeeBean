namespace Graphgine.Execution
{
    /// <summary>
    /// Runtime-injectable boundary over the generated enum conversion table
    /// (EntityMeta.g.cs's EnumConversions class). Same reasoning as
    /// IMutationMetadataProvider: Graphgine cannot reference the generated
    /// static class directly, so consuming applications inject a real
    /// implementation (see GeneratedEnumConversionProvider) instead.
    /// </summary>
    public interface IEnumConversionProvider
    {
        /// <summary>
        /// Returns the converted (enum-mapped) SQL literal for a field
        /// value, or null if the column has no enum conversion registered
        /// and the raw value should be emitted as-is.
        /// </summary>
        string? TryConvert(ushort storageEntityId, ushort columnId, string value);
    }
}
