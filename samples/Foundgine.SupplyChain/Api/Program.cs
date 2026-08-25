using Foundgine.SupplyChain.Application;
using Foundgine.SupplyChain.Infrastructure;
using ModelContextProtocol.Server;
using Foundgine.MCP;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var cs = builder.Configuration["SupplyChainConnectionString"] ??
         Environment.GetEnvironmentVariable("SupplyChainConnectionString") ??
         throw new InvalidOperationException("SupplyChainConnectionString is required.");
builder.Services.AddSupplyChainApplication().AddSupplyChainInfrastructure(cs)
    .AddFoundgineMcp(() => new Foundgine.Execution.ExecutionContext());
builder.Services.AddMcpServer().WithHttpTransport(o => o.Stateless = true).WithTools<SupplyChainMcpTools>();
var app = builder.Build();
app.MapMcp("/mcp");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (NpgsqlDataSource ds, CancellationToken ct) =>
{
    await using var c = await ds.OpenConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand("SELECT 1", c);
    await cmd.ExecuteScalarAsync(ct);
    return Results.Ok(new { status = "ready" });
});
app.Run();

[McpServerToolType]
public sealed class SupplyChainMcpTools
{
    private readonly IServiceScopeFactory _scopes;
    public SupplyChainMcpTools(IServiceScopeFactory scopes) => _scopes = scopes;

    private async Task<object> With(Func<SupplyChainApplication, Task<object>> f)
    {
        using var s = _scopes.CreateScope();
        return await f(s.ServiceProvider.GetRequiredService<SupplyChainApplication>());
    }

    [McpServerTool(Name = "describe_capabilities")]
    public Task<object> Describe(string actor) => With(a => Task.FromResult(a.DescribeCapabilities(actor)));

    [McpServerTool(Name = "get_my_orders")]
    public Task<object> GetMyOrders(string actor, int customerId, CancellationToken ct = default) =>
        With(a => a.GetMyOrders(actor, customerId, ct));

    [McpServerTool(Name = "get_order")]
    public Task<object> GetOrder(string actor, int customerId, int orderId, CancellationToken ct = default) =>
        With(a => a.GetOrder(actor, customerId, orderId, ct));

    [McpServerTool(Name = "get_shipment")]
    public Task<object> GetShipment(string actor, int customerId, int shipmentId, CancellationToken ct = default) =>
        With(a => a.GetShipment(actor, customerId, shipmentId, ct));

    [McpServerTool(Name = "list_products")]
    public Task<object> ListProducts(string actor, CancellationToken ct = default) =>
        With(a => a.ListProducts(actor, ct));

    [McpServerTool(Name = "list_customers")]
    public Task<object> ListCustomers(string actor, CancellationToken ct = default) =>
        With(a => a.ListCustomers(actor, ct));

    [McpServerTool(Name = "get_product")]
    public Task<object> GetProduct(string actor, int productId, CancellationToken ct = default) =>
        With(a => a.GetProduct(actor, productId, ct));

    [McpServerTool(Name = "get_inventory")]
    public Task<object> GetInventory(string actor, int productId, CancellationToken ct = default) =>
        With(a => a.GetInventory(actor, productId, ct));

    [McpServerTool(Name = "list_suppliers")]
    public Task<object> ListSuppliers(string actor, CancellationToken ct = default) =>
        With(a => a.ListSuppliers(actor, ct));

    [McpServerTool(Name = "update_inventory")]
    public Task<object> UpdateInventory(string actor, int warehouseId, int productId, int quantity,
        CancellationToken ct = default) => With(a => a.UpdateInventory(actor, warehouseId, productId, quantity, ct));

    [McpServerTool(Name = "create_shipment")]
    public Task<object> CreateShipment(string actor, int orderId, int carrierId, int warehouseId, string trackingNumber,
        CancellationToken ct = default) =>
        With(a => a.CreateShipment(actor, orderId, carrierId, warehouseId, trackingNumber, ct));

    [McpServerTool(Name = "update_shipment")]
    public Task<object> UpdateShipment(string actor, int shipmentId, string status, CancellationToken ct = default) =>
        With(a => a.UpdateShipment(actor, shipmentId, status, ct));

    [McpServerTool(Name = "place_order")]
    public Task<object> PlaceOrder(string actor, int customerId, OrderLine[] lines, string idempotencyKey,
        CancellationToken ct = default) => With(a => a.PlaceOrder(actor, customerId, lines, idempotencyKey, ct));

    [McpServerTool(Name = "cancel_order")]
    public Task<object> CancelOrder(string actor, int customerId, int orderId, CancellationToken ct = default) =>
        With(a => a.CancelOrder(actor, customerId, orderId, ct));
}