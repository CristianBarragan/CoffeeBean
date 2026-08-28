using Foundgine.SupplyChain.Application;
using Foundgine.SupplyChain.Semantics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Foundgine.SupplyChain.Infrastructure;

/// <summary>
/// Bundled setup shared by every SupplyChain host (the canonical
/// <c>Foundgine.SupplyChain.Api</c>, and the PenTest sample's
/// <c>Graph.Api</c>/<c>Mcp.Api</c>). Before this existed, each host
/// independently repeated the same connection-string resolution, the same
/// <c>AddSupplyChainApplication().AddSupplyChainInfrastructure(cs)</c> pair,
/// and (for two of the three hosts) the same <c>/health</c>/<c>/health/ready</c>
/// mapping. Consolidating that here means a change to how the sample wires
/// itself up - e.g. adding the new capability registry - only has to happen
/// in one place.
/// </summary>
public static class SupplyChainHost
{
    /// <summary>
    /// One call replacing the previously-duplicated
    /// <c>AddSupplyChainApplication().AddSupplyChainInfrastructure(cs)</c> pair.
    /// Also registers the shared <see cref="Foundgine.Semantics.Capabilities.SemanticCapabilityRegistry"/>
    /// (the new Step 5/6 capability-definition API) so every host resolves the
    /// same authoritative capability metadata.
    /// </summary>
    public static IServiceCollection AddSupplyChainCore(this IServiceCollection services, string connectionString) =>
        services
            .AddSupplyChainApplication()
            .AddSupplyChainInfrastructure(connectionString)
            .AddSupplyChainCapabilityRegistry();

    /// <summary>
    /// Resolves the SupplyChain connection string the same way every host needs
    /// it resolved, checking (in order) the flat configuration key the canonical
    /// API and the PenTest MCP host use, the <c>ConnectionStrings</c> section the
    /// PenTest GraphQL host uses, and the environment variable the canonical API
    /// falls back to. This is a strict superset of the three previously-separate,
    /// slightly-inconsistent lookup snippets, so no host's existing configuration
    /// (env vars, appsettings, docker-compose) needs to change.
    /// </summary>
    public static string ResolveSupplyChainConnectionString(this IConfiguration configuration) =>
        configuration["SupplyChainConnectionString"]
        ?? configuration.GetConnectionString("SupplyChainConnectionString")
        ?? Environment.GetEnvironmentVariable("SupplyChainConnectionString")
        ?? throw new InvalidOperationException(
            "Connection string 'SupplyChainConnectionString' was not configured " +
            "(checked configuration key, ConnectionStrings section, and environment variable).");

    /// <summary>
    /// Maps the <c>/health</c> and <c>/health/ready</c> endpoints previously
    /// copy-pasted identically into the canonical API's and the PenTest MCP
    /// host's <c>Program.cs</c>.
    /// </summary>
    public static WebApplication MapSupplyChainHealthChecks(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/health/ready", async (NpgsqlDataSource ds, CancellationToken ct) =>
        {
            await using var connection = await ds.OpenConnectionAsync(ct);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(ct);
            return Results.Ok(new { status = "ready" });
        });
        return app;
    }
}
