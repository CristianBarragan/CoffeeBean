using System.Reflection;
using Xunit;

namespace Foundgine.MCP.Tests;

public sealed class McpMutationBoundaryTests
{
    [Fact]
    public void MutationToolsAreTransportOnly()
    {
        var assembly = typeof(FoundgineMcpMutationTools).Assembly;
        var source = string.Join("\n", assembly.GetTypes().Select(t => t.FullName));
        Assert.DoesNotContain("Foundgine.Sql", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HotChocolate", source, StringComparison.Ordinal);
    }
}
