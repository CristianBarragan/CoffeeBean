using System.Reflection;
using Xunit;

namespace Foundgine.Semantics.Tests;

public sealed class SemanticsDependencyBoundaryTests
{
    [Fact]
    public void SemanticAssembly_does_not_reference_metadata_assembly()
    {
        var assembly = typeof(Foundgine.Semantics.SemanticModel).Assembly;

        Assert.DoesNotContain(
            assembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, "Foundgine.Metadata", StringComparison.Ordinal));
    }
}
