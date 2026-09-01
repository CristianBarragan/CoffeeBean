using Foundgine.Abstractions;
using Foundgine.GraphQL.HotChocolate;
using Xunit;

namespace Foundgine.GraphQL.HotChocolate.Tests;

public sealed class AdapterBoundaryTests
{
    [Fact]
    public void Query_adapter_does_not_reference_planning_execution_or_sql()
    {
        var references = typeof(HotChocolateSemanticAdapter).Assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name)
            .Where(x => x is not null)
            .ToArray();

        Assert.DoesNotContain("Foundgine.Planning", references);
        Assert.DoesNotContain("Foundgine.Execution", references);
        Assert.DoesNotContain("Foundgine.Sql", references);
    }
}
