using Foundgine.Testing;
using Foundgine.MCP;
using Foundgine.Semantics.Security.Execution;
using Foundgine.Semantics.Security.Warrants;
using Xunit;

namespace Foundgine.MCP.Tests;

public sealed class McpCapabilityDiscoverySecurityTests
{
    [Fact]
    public void Discovery_requires_host_security_context()
    {
        var tools = new FoundgineMcpTools(new CapabilityOnlyFoundgine(), securityContextFactory: () => null);

        Assert.Throws<UnauthorizedAccessException>(() => tools.DescribeCapabilities());
    }

    [Fact]
    public void Discovery_does_not_accept_agent_supplied_security_context()
    {
        var context = CreateContext("host-subject", "tenant-1");
        var tools = new FoundgineMcpTools(new CapabilityOnlyFoundgine(), securityContextFactory: () => context);

        // The MCP tool has no parameter through which an agent can replace the host context.
        var json = tools.DescribeCapabilities();
        Assert.Contains("orders.read", json, StringComparison.Ordinal);
        Assert.DoesNotContain("customers.read", json, StringComparison.Ordinal);
    }

    private static SecurityExecutionContext CreateContext(string subject, string tenant) =>
        new(
            new SecurityWarrant(
                "warrant-discovery", "issuer", subject, "mcp",
                [new CapabilityGrant("orders.read", "read", [])],
                SecurityWarrantConstraints.Unrestricted,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddMinutes(5),
                "nonce-discovery", "key-1", null, []),
            subject, "mcp", tenant);


}
