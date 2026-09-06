namespace Foundgine.Core.Semantic.Tests;

public sealed class SemanticsDependencyBoundaryTests
{
    [Fact]
    public void SemanticAssembly_does_not_reference_metadata_assembly()
    {
        var assembly = typeof(SemanticModel).Assembly;

        Assert.DoesNotContain(
            assembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, "Foundgine.Core.Semantic.Metadata", StringComparison.Ordinal));
    }
}