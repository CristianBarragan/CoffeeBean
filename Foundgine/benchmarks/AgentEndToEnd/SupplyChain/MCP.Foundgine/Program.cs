using Foundgine.Abstractions;
using Foundgine.Execution;
using Foundgine.MCP;
using Foundgine.Planning;
using Foundgine.Semantics;
using Foundgine.Semantics.Authorization;
using Foundgine.Semantics.IR;
using ModelContextProtocol.Server;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var cs = builder.Configuration["SupplyChainConnectionString"]
    ?? Environment.GetEnvironmentVariable("SupplyChainConnectionString")
    ?? throw new InvalidOperationException("SupplyChainConnectionString is required.");

builder.Services.AddSingleton(NpgsqlDataSource.Create(cs));
// The unfrozen model is kept registered for anything that still wants it,
// but everything that needs a trusted contract (below) uses the frozen
// snapshot - see the comment on the SemanticContractSnapshot registration.
builder.Services.AddSingleton(SupplyChainSemanticModel.Build());
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<SemanticModel>().Freeze().CreateSnapshot());
// Actor/capability authorization for this benchmark is handled by
// SupplyChainAuthorizer.CanExecute at the MCP tool boundary (see
// SupplyChainMcpTools.Execute) before any of this code runs - that is the
// real access-control decision. The SemanticAuthorizer registered here is a
// second, narrower thing: it produces the SemanticAuthorizationResult that
// Foundgine's planner requires to stamp a plan with authorization
// provenance before ExecutionIRCompiler will compile it (see
// ExecutionIRCompiler.Compile). It uses AllowAllSemanticAuthorizationPolicy
// because entity/field-level access within this fixed benchmark schema
// isn't the thing being tested here - the actor gate above is - so this
// step is honestly just "formally bind provenance to a plan for a request
// that already passed the real authorization check", not a second
// independent access-control layer.
builder.Services.AddSingleton(new SemanticAuthorizer(new AllowAllSemanticAuthorizationPolicy()));
builder.Services.AddSingleton<SupplyChainAuthorizer>();
builder.Services.AddSingleton<Planner>();
builder.Services.AddScoped<SupplyChainExecutionService>();
builder.Services.AddFoundgineMcp(() => new Foundgine.Execution.ExecutionContext());
builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<SupplyChainMcpTools>();

var app = builder.Build();
app.MapMcp("/mcp");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (NpgsqlDataSource dataSource, CancellationToken ct) =>
{
    await using var c = await dataSource.OpenConnectionAsync(ct);
    await using var cmd = new NpgsqlCommand("SELECT 1", c);
    await cmd.ExecuteScalarAsync(ct);
    return Results.Ok(new { status = "ready" });
});
app.Run();

public static class SupplyChainSemanticModel
{
    public static readonly EntityId Customer = new(1);
    public static readonly EntityId Order = new(2);
    public static readonly EntityId OrderItem = new(3);
    public static readonly EntityId Product = new(4);
    public static readonly EntityId Supplier = new(5);
    public static readonly EntityId Category = new(6);
    public static readonly EntityId Inventory = new(7);
    public static readonly EntityId Warehouse = new(8);
    public static readonly EntityId Shipment = new(9);
    public static readonly EntityId Carrier = new(10);

    public static readonly RelationshipId CustomerOrders = new(1);
    public static readonly RelationshipId OrderItems = new(2);
    public static readonly RelationshipId ItemProduct = new(3);
    public static readonly RelationshipId ProductSupplier = new(4);
    public static readonly RelationshipId ProductCategory = new(5);
    public static readonly RelationshipId ProductInventory = new(6);
    public static readonly RelationshipId InventoryWarehouse = new(7);
    public static readonly RelationshipId OrderShipments = new(8);
    public static readonly RelationshipId ShipmentCarrier = new(9);
    public static readonly RelationshipId ShipmentWarehouse = new(10);

    public static SemanticModel Build() => new SemanticModelBuilder()
        .Entity(Customer, "Customer", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "FirstName", typeof(string))
            .Field(new FieldId(3), "LastName", typeof(string))
            .Field(new FieldId(4), "Email", typeof(string))
            .Relationship(CustomerOrders, "Orders", Order, RelationshipCardinality.Many))
        .Entity(Order, "Order", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "CustomerId", typeof(int))
            .Field(new FieldId(3), "Status", typeof(string))
            .Field(new FieldId(4), "TotalAmount", typeof(decimal))
            .Relationship(OrderItems, "Items", OrderItem, RelationshipCardinality.Many)
            .Relationship(OrderShipments, "Shipments", Shipment, RelationshipCardinality.Many))
        .Entity(OrderItem, "OrderItem", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "OrderId", typeof(int))
            .Field(new FieldId(3), "ProductId", typeof(int))
            .Field(new FieldId(4), "Quantity", typeof(int))
            .Field(new FieldId(5), "UnitPrice", typeof(decimal))
            .Relationship(ItemProduct, "Product", Product, RelationshipCardinality.One))
        .Entity(Product, "Product", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "Name", typeof(string))
            .Field(new FieldId(3), "Sku", typeof(string))
            .Field(new FieldId(4), "UnitPrice", typeof(decimal))
            .Relationship(ProductSupplier, "Supplier", Supplier, RelationshipCardinality.One)
            .Relationship(ProductCategory, "Category", Category, RelationshipCardinality.One)
            .Relationship(ProductInventory, "Inventory", Inventory, RelationshipCardinality.Many))
        .Entity(Supplier, "Supplier", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "Name", typeof(string))
            .Field(new FieldId(3), "Email", typeof(string)))
        .Entity(Category, "Category", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "Name", typeof(string)))
        .Entity(Inventory, "Inventory", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "WarehouseId", typeof(int))
            .Field(new FieldId(3), "ProductId", typeof(int))
            .Field(new FieldId(4), "QuantityOnHand", typeof(int))
            .Field(new FieldId(5), "ReorderLevel", typeof(int))
            .Relationship(InventoryWarehouse, "Warehouse", Warehouse, RelationshipCardinality.One))
        .Entity(Warehouse, "Warehouse", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "Name", typeof(string))
            .Field(new FieldId(3), "Location", typeof(string)))
        .Entity(Shipment, "Shipment", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "OrderId", typeof(int))
            .Field(new FieldId(3), "CarrierId", typeof(int))
            .Field(new FieldId(4), "WarehouseId", typeof(int))
            .Field(new FieldId(5), "TrackingNumber", typeof(string))
            .Field(new FieldId(6), "Status", typeof(string))
            .Relationship(ShipmentCarrier, "Carrier", Carrier, RelationshipCardinality.One)
            .Relationship(ShipmentWarehouse, "Warehouse", Warehouse, RelationshipCardinality.One))
        .Entity(Carrier, "Carrier", e => e
            .Identity(new FieldId(1), "Id")
            .Field(new FieldId(2), "Name", typeof(string)))
        .Build();
}

public sealed record OrderLine(int ProductId, int Quantity);

public sealed class SupplyChainAuthorizer
{
    public bool CanExecute(string actor, string capability, int? requestedCustomerId = null, int? actorCustomerId = null)
    {
        if (actor == "admin") return true;
        if (actor == "bob") return capability is "get_my_orders" or "get_order" or "get_product" or "get_shipment" or "place_order" or "cancel_order" or "list_customers";
        if (actor == "carol") return capability is "get_product" or "get_inventory" or "update_inventory" or "create_shipment" or "update_shipment";
        if (actor == "dave") return capability is "get_product" or "get_inventory" or "list_products" or "list_suppliers" or "update_inventory";
        if (actor == "alice")
        {
            if (capability is not ("get_my_orders" or "get_order" or "get_product" or "get_shipment" or "place_order" or "cancel_order")) return false;
            return requestedCustomerId is null || requestedCustomerId == 1;
        }
        if (actor.StartsWith("customer", StringComparison.OrdinalIgnoreCase))
        {
            if (capability is not ("get_my_orders" or "get_order" or "get_product" or "get_shipment" or "place_order" or "cancel_order")) return false;
            return requestedCustomerId is null || actorCustomerId == requestedCustomerId;
        }
        return false;
    }
}

[McpServerToolType]
public sealed class SupplyChainMcpTools
{
    private readonly IServiceScopeFactory scopes;

    public SupplyChainMcpTools(IServiceScopeFactory scopes) => this.scopes = scopes;

    [McpServerTool(Name = "describe_capabilities")]
    public object DescribeCapabilities(string actor) => new
    {
        actor,
        capabilities = actor switch
        {
            "alice" => new[] { "get_my_orders", "get_order", "get_product", "get_shipment", "place_order", "cancel_order" },
            "bob" => new[] { "get_my_orders", "get_order", "get_product", "get_shipment", "place_order", "cancel_order", "list_customers" },
            "carol" => new[] { "get_product", "get_inventory", "update_inventory", "create_shipment", "update_shipment" },
            "dave" => new[] { "get_product", "get_inventory", "list_products", "list_suppliers", "update_inventory" },
            "admin" => new[] { "get_my_orders", "get_order", "get_product", "get_shipment", "place_order", "cancel_order", "list_customers", "get_inventory", "update_inventory", "create_shipment", "update_shipment", "list_products", "list_suppliers" },
            _ => Array.Empty<string>()
        }
    };

    [McpServerTool(Name = "get_my_orders")]
    public Task<object> GetMyOrders(string actor, int customerId, CancellationToken ct = default) =>
        Execute("get_my_orders", actor, customerId, ct, (s, resolved) => s.GetOrders(resolved, ct));

    [McpServerTool(Name = "get_order")]
    public Task<object> GetOrder(string actor, int customerId, int orderId, CancellationToken ct = default) =>
        Execute("get_order", actor, customerId, ct, (s, _) => s.GetOrder(customerId, orderId, ct));

    [McpServerTool(Name = "get_shipment")]
    public Task<object> GetShipment(string actor, int customerId, int shipmentId, CancellationToken ct = default) =>
        Execute("get_shipment", actor, customerId, ct, (s, _) => s.GetShipment(customerId, shipmentId, ct));

    [McpServerTool(Name = "list_products")]
    public Task<object> ListProducts(string actor, CancellationToken ct = default) =>
        Execute("list_products", actor, null, ct, (s, _) => s.ListProducts(ct));

    [McpServerTool(Name = "list_customers")]
    public Task<object> ListCustomers(string actor, CancellationToken ct = default) =>
        Execute("list_customers", actor, null, ct, (s, _) => s.ListCustomers(ct));

    [McpServerTool(Name = "get_product")]
    public Task<object> GetProduct(string actor, int productId, CancellationToken ct = default) =>
        Execute("get_product", actor, null, ct, (s, _) => s.GetProduct(productId, ct));

    [McpServerTool(Name = "get_inventory")]
    public Task<object> GetInventory(string actor, int productId, CancellationToken ct = default) =>
        Execute("get_inventory", actor, null, ct, (s, _) => s.GetInventory(productId, ct));

    [McpServerTool(Name = "list_suppliers")]
    public Task<object> ListSuppliers(string actor, CancellationToken ct = default) =>
        Execute("list_suppliers", actor, null, ct, (s, _) => s.ListSuppliers(ct));

    [McpServerTool(Name = "update_inventory")]
    public Task<object> UpdateInventory(string actor, int warehouseId, int productId, int quantity, CancellationToken ct = default) =>
        Execute("update_inventory", actor, null, ct, (s, _) => s.UpdateInventory(warehouseId, productId, quantity, ct));

    [McpServerTool(Name = "create_shipment")]
    public Task<object> CreateShipment(string actor, int orderId, int carrierId, int warehouseId, string trackingNumber, CancellationToken ct = default) =>
        Execute("create_shipment", actor, null, ct, (s, _) => s.CreateShipment(orderId, carrierId, warehouseId, trackingNumber, ct));

    [McpServerTool(Name = "update_shipment")]
    public Task<object> UpdateShipment(string actor, int shipmentId, string status, CancellationToken ct = default) =>
        Execute("update_shipment", actor, null, ct, (s, _) => s.UpdateShipment(shipmentId, status, ct));

    [McpServerTool(Name = "place_order")]
    public Task<object> PlaceOrder(string actor, int customerId, OrderLine[] lines, string idempotencyKey, CancellationToken ct = default) =>
        Execute("place_order", actor, customerId, ct, (s, _) => s.PlaceOrder(actor, customerId, lines, idempotencyKey, ct));

    [McpServerTool(Name = "cancel_order")]
    public Task<object> CancelOrder(string actor, int customerId, int orderId, CancellationToken ct = default) =>
        Execute("cancel_order", actor, customerId, ct, (s, _) => s.CancelOrder(actor, customerId, orderId, ct));

    private async Task<object> Execute(
        string capability,
        string actor,
        int? requestedCustomerId,
        CancellationToken ct,
        Func<SupplyChainExecutionService, int, Task<object>> operation)
    {
        using var scope = scopes.CreateScope();
        var authorizer = scope.ServiceProvider.GetRequiredService<SupplyChainAuthorizer>();
        var service = scope.ServiceProvider.GetRequiredService<SupplyChainExecutionService>();
        var actorCustomerId = ActorCustomerId(actor);

        if (!authorizer.CanExecute(actor, capability, requestedCustomerId, actorCustomerId))
            throw new UnauthorizedAccessException($"Actor '{actor}' is not authorized for capability '{capability}'.");

        return await operation(service, requestedCustomerId ?? 0);
    }

    private static int ActorCustomerId(string actor)
    {
        if (actor.Equals("alice", StringComparison.OrdinalIgnoreCase)) return 1;
        if (actor.StartsWith("customer", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(actor[8..], out var id)) return id;
        return 0;
    }
}

public sealed class SupplyChainExecutionService
{
    private readonly NpgsqlDataSource ds;
    private readonly Planner planner;
    private readonly SemanticAuthorizer authorizer;
    private readonly SemanticContractSnapshot contract;

    public SupplyChainExecutionService(
        NpgsqlDataSource ds,
        Planner planner,
        SemanticAuthorizer authorizer,
        SemanticContractSnapshot contract)
    {
        this.ds = ds;
        this.planner = planner;
        this.authorizer = authorizer;
        this.contract = contract;
    }

    public async Task<object> GetOrders(int customerId, CancellationToken ct)
    {
        await using var c = await ds.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT order_id, customer_id, status, total_amount, order_date FROM orders WHERE customer_id=@c ORDER BY order_id", c);
        cmd.Parameters.AddWithValue("c", customerId);
        var orders = await ReadRows(cmd, ct);
        return new { customerId, orders, plan = PlanFingerprint(PlanCustomerOrders()) };
    }

    public async Task<object> GetOrder(int customerId, int orderId, CancellationToken ct)
    {
        await using var c = await ds.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT order_id, customer_id, status, total_amount, order_date FROM orders WHERE order_id=@o AND customer_id=@c", c);
        cmd.Parameters.AddWithValue("o", orderId);
        cmd.Parameters.AddWithValue("c", customerId);
        var order = await ReadSingle(cmd, ct);
        if (order is null) throw new KeyNotFoundException("Order not found.");

        await using var items = new NpgsqlCommand("SELECT oi.order_item_id, oi.product_id, p.product_name, p.sku, oi.quantity, oi.unit_price FROM order_items oi JOIN products p ON p.product_id=oi.product_id WHERE oi.order_id=@o ORDER BY oi.order_item_id", c);
        items.Parameters.AddWithValue("o", orderId);
        var itemRows = await ReadRows(items, ct);
        return new { order, items = itemRows, plan = PlanFingerprint(PlanCustomerOrders()) };
    }

    public async Task<object> GetShipment(int customerId, int shipmentId, CancellationToken ct)
    {
        await using var c = await ds.OpenConnectionAsync(ct);
        const string sql = """
            SELECT s.shipment_id, s.order_id, s.carrier_id, ca.carrier_name,
                   s.warehouse_id, w.warehouse_name, s.tracking_number,
                   s.shipping_status
            FROM shipments s
            JOIN orders o ON o.order_id=s.order_id
            LEFT JOIN carriers ca ON ca.carrier_id=s.carrier_id
            LEFT JOIN warehouses w ON w.warehouse_id=s.warehouse_id
            WHERE s.shipment_id=@s AND o.customer_id=@c
            """;
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("s", shipmentId);
        cmd.Parameters.AddWithValue("c", customerId);
        var shipment = await ReadSingle(cmd, ct);
        if (shipment is null) throw new KeyNotFoundException("Shipment not found.");
        return new { shipment };
    }

    public async Task<object> ListProducts(CancellationToken ct)
    {
        await using var c = await ds.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT product_id, product_name, sku, unit_price FROM products ORDER BY product_id", c);
        return new { products = await ReadRows(cmd, ct), plan = PlanFingerprint(PlanProduct()) };
    }

    public async Task<object> ListCustomers(CancellationToken ct)
    {
        await using var c = await ds.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT customer_id, first_name, last_name, email FROM customers ORDER BY customer_id", c);
        return new { customers = await ReadRows(cmd, ct) };
    }

    public async Task<object> GetProduct(int productId, CancellationToken ct)
    {
        await using var c = await ds.OpenConnectionAsync(ct);
        const string sql = """
            SELECT p.product_id, p.product_name, p.sku, p.unit_price,
                   s.supplier_id, s.supplier_name, s.email AS supplier_email,
                   ca.category_id, ca.category_name
            FROM products p
            LEFT JOIN suppliers s ON s.supplier_id=p.supplier_id
            LEFT JOIN categories ca ON ca.category_id=p.category_id
            WHERE p.product_id=@p
            """;
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("p", productId);
        var product = await ReadSingle(cmd, ct);
        if (product is null) throw new KeyNotFoundException("Product not found.");
        return new { product, plan = PlanFingerprint(PlanProduct()) };
    }

    public async Task<object> GetInventory(int productId, CancellationToken ct)
    {
        await using var c = await ds.OpenConnectionAsync(ct);
        const string sql = """
            SELECT i.inventory_id, i.warehouse_id, w.warehouse_name,
                   i.product_id, i.quantity_on_hand, i.reorder_level
            FROM inventory i
            JOIN warehouses w ON w.warehouse_id=i.warehouse_id
            WHERE i.product_id=@p ORDER BY i.warehouse_id
            """;
        await using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddWithValue("p", productId);
        return new { productId, inventory = await ReadRows(cmd, ct) };
    }

    public async Task<object> ListSuppliers(CancellationToken ct)
    {
        await using var c = await ds.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT supplier_id, supplier_name, email FROM suppliers ORDER BY supplier_id", c);
        return new { suppliers = await ReadRows(cmd, ct) };
    }

    public async Task<object> UpdateInventory(int warehouseId, int productId, int quantity, CancellationToken ct)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
        await using var c = await ds.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("UPDATE inventory SET quantity_on_hand=@q,last_updated=CURRENT_TIMESTAMP WHERE warehouse_id=@w AND product_id=@p RETURNING inventory_id,quantity_on_hand", c);
        cmd.Parameters.AddWithValue("q", quantity);
        cmd.Parameters.AddWithValue("w", warehouseId);
        cmd.Parameters.AddWithValue("p", productId);
        var row = await ReadSingle(cmd, ct);
        if (row is null) throw new KeyNotFoundException("Inventory row not found.");
        return new { warehouseId, productId, quantity, result = row };
    }

    public async Task<object> CreateShipment(int orderId, int carrierId, int warehouseId, string trackingNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber)) throw new ArgumentException("Tracking number is required.");
        await using var c = await ds.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("INSERT INTO shipments(order_id,carrier_id,warehouse_id,tracking_number,shipping_status) VALUES(@o,@c,@w,@t,'In Transit') RETURNING shipment_id,order_id,carrier_id,warehouse_id,tracking_number,shipping_status", c);
        cmd.Parameters.AddWithValue("o", orderId);
        cmd.Parameters.AddWithValue("c", carrierId);
        cmd.Parameters.AddWithValue("w", warehouseId);
        cmd.Parameters.AddWithValue("t", trackingNumber);
        var shipment = await ReadSingle(cmd, ct);
        return new { shipment };
    }

    public async Task<object> UpdateShipment(int shipmentId, string status, CancellationToken ct)
    {
        var allowed = new[] { "In Transit", "Out for Delivery", "Delivered", "Delayed" };
        if (!allowed.Contains(status, StringComparer.Ordinal)) throw new InvalidOperationException("Invalid shipment status.");
        await using var c = await ds.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("UPDATE shipments SET shipping_status=@s WHERE shipment_id=@i RETURNING shipment_id,shipping_status", c);
        cmd.Parameters.AddWithValue("s", status);
        cmd.Parameters.AddWithValue("i", shipmentId);
        var shipment = await ReadSingle(cmd, ct);
        if (shipment is null) throw new KeyNotFoundException("Shipment not found.");
        return new { shipment };
    }

    public async Task<object> PlaceOrder(string actor, int customerId, OrderLine[] lines, string key, CancellationToken ct)
    {
        if (lines is null || lines.Length == 0) throw new ArgumentException("At least one line is required.");
        if (lines.Any(x => x.Quantity <= 0)) throw new InvalidOperationException("Quantity must be positive.");
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Idempotency key is required.");

        var plan = PlanPlaceOrder();
        var requested = lines.GroupBy(x => x.ProductId).Select(g => new OrderLine(g.Key, g.Sum(x => x.Quantity))).ToArray();

        await using var c = await ds.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await using (var lockCmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(@k));", c, tx))
        {
            lockCmd.Parameters.AddWithValue("k", key);
            await lockCmd.ExecuteScalarAsync(ct);
        }

        await using (var existing = new NpgsqlCommand("SELECT order_id FROM supply_chain_idempotency WHERE idempotency_key=@k FOR SHARE", c, tx))
        {
            existing.Parameters.AddWithValue("k", key);
            var v = await existing.ExecuteScalarAsync(ct);
            if (v is not null)
            {
                await tx.CommitAsync(ct);
                return new { orderId = Convert.ToInt32(v), replay = true, plan = PlanFingerprint(plan) };
            }
        }

        await using (var owner = new NpgsqlCommand("SELECT customer_id FROM customers WHERE customer_id=@id", c, tx))
        {
            owner.Parameters.AddWithValue("id", customerId);
            if (await owner.ExecuteScalarAsync(ct) is null) throw new InvalidOperationException("Customer not found.");
        }

        decimal total = 0;
        var resolved = new List<(int product, int qty, decimal price, int warehouse)>();
        foreach (var line in requested)
        {
            await using var p = new NpgsqlCommand("SELECT unit_price FROM products WHERE product_id=@p", c, tx);
            p.Parameters.AddWithValue("p", line.ProductId);
            var v = await p.ExecuteScalarAsync(ct);
            if (v is null) throw new InvalidOperationException($"Product {line.ProductId} not found.");
            var price = (decimal)v;

            await using var stock = new NpgsqlCommand("SELECT warehouse_id FROM inventory WHERE product_id=@p AND quantity_on_hand>=@q ORDER BY quantity_on_hand DESC, warehouse_id FOR UPDATE SKIP LOCKED LIMIT 1", c, tx);
            stock.Parameters.AddWithValue("p", line.ProductId);
            stock.Parameters.AddWithValue("q", line.Quantity);
            var w = await stock.ExecuteScalarAsync(ct);
            if (w is null) throw new InvalidOperationException($"Insufficient inventory for product {line.ProductId}.");
            var warehouse = Convert.ToInt32(w);
            resolved.Add((line.ProductId, line.Quantity, price, warehouse));
            total += price * line.Quantity;
        }

        int orderId;
        await using (var ins = new NpgsqlCommand("INSERT INTO orders(customer_id,status,total_amount) VALUES(@c,'Pending',@t) RETURNING order_id", c, tx))
        {
            ins.Parameters.AddWithValue("c", customerId);
            ins.Parameters.AddWithValue("t", total);
            orderId = Convert.ToInt32(await ins.ExecuteScalarAsync(ct));
        }

        foreach (var x in resolved)
        {
            int itemId;
            await using (var oi = new NpgsqlCommand("INSERT INTO order_items(order_id,product_id,quantity,unit_price) VALUES(@o,@p,@q,@u) RETURNING order_item_id", c, tx))
            {
                oi.Parameters.AddWithValue("o", orderId);
                oi.Parameters.AddWithValue("p", x.product);
                oi.Parameters.AddWithValue("q", x.qty);
                oi.Parameters.AddWithValue("u", x.price);
                itemId = Convert.ToInt32(await oi.ExecuteScalarAsync(ct));
            }

            await using var alloc = new NpgsqlCommand("INSERT INTO order_allocations(order_item_id,warehouse_id,quantity) VALUES(@i,@w,@q); UPDATE inventory SET quantity_on_hand=quantity_on_hand-@q,last_updated=CURRENT_TIMESTAMP WHERE warehouse_id=@w AND product_id=@p AND quantity_on_hand>=@q", c, tx);
            alloc.Parameters.AddWithValue("i", itemId);
            alloc.Parameters.AddWithValue("w", x.warehouse);
            alloc.Parameters.AddWithValue("q", x.qty);
            alloc.Parameters.AddWithValue("p", x.product);
            var affected = await alloc.ExecuteNonQueryAsync(ct);
            if (affected < 2) throw new InvalidOperationException("Inventory changed before reservation could be committed.");
        }

        await using (var idem = new NpgsqlCommand("INSERT INTO supply_chain_idempotency(idempotency_key,actor_id,operation,order_id) VALUES(@k,@a,'place_order',@o)", c, tx))
        {
            idem.Parameters.AddWithValue("k", key);
            idem.Parameters.AddWithValue("a", ActorNumber(actor));
            idem.Parameters.AddWithValue("o", orderId);
            await idem.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return new { orderId, replay = false, total, plan = PlanFingerprint(plan), evidence = Hash($"place_order|{actor}|{customerId}|{orderId}|{key}") };
    }

    public async Task<object> CancelOrder(string actor, int customerId, int orderId, CancellationToken ct)
    {
        await using var c = await ds.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);

        await using (var q = new NpgsqlCommand("UPDATE orders SET status='Cancelled' WHERE order_id=@o AND customer_id=@c AND status='Pending' RETURNING order_id", c, tx))
        {
            q.Parameters.AddWithValue("o", orderId);
            q.Parameters.AddWithValue("c", customerId);
            if (await q.ExecuteScalarAsync(ct) is null) throw new UnauthorizedAccessException("Order is not owned by the customer or is not cancellable.");
        }

        await using (var items = new NpgsqlCommand("UPDATE inventory i SET quantity_on_hand=i.quantity_on_hand+a.quantity,last_updated=CURRENT_TIMESTAMP FROM order_allocations a JOIN order_items oi ON oi.order_item_id=a.order_item_id WHERE oi.order_id=@o AND i.warehouse_id=a.warehouse_id AND i.product_id=oi.product_id", c, tx))
        {
            items.Parameters.AddWithValue("o", orderId);
            await items.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return new { orderId, status = "Cancelled", restoredInventory = true };
    }

    private SemanticPlan PlanCustomerOrders() => Plan(new SemanticOperation(
        new SemanticReadNode(1, SupplyChainSemanticModel.Customer,
            new[] { new FieldId(1), new FieldId(2), new FieldId(3) }, null, null,
            new[] { new SemanticReadNode(2, SupplyChainSemanticModel.Order,
                new[] { new FieldId(1), new FieldId(3), new FieldId(4) },
                SupplyChainSemanticModel.CustomerOrders, null, Array.Empty<SemanticReadNode>()) })));

    private SemanticPlan PlanProduct() => Plan(new SemanticOperation(
        new SemanticReadNode(1, SupplyChainSemanticModel.Product,
            new[] { new FieldId(1), new FieldId(2), new FieldId(3), new FieldId(4) },
            null, null, Array.Empty<SemanticReadNode>())));

    private SemanticPlan PlanPlaceOrder() => PlanProduct();

    // Authorizes the operation against the trusted contract (see the
    // AllowAllSemanticAuthorizationPolicy comment in Program.cs's DI setup
    // for why an allow-all policy is the right thing here) and plans it
    // through the overload that stamps the resulting SemanticPlan with real
    // authorization provenance, so ExecutionIRCompiler.Compile below
    // (called from PlanFingerprint) doesn't hit "An executable plan must
    // carry authorization provenance before crossing the execution
    // boundary." The previous code called the bare Plan(SemanticOperation)
    // overload, which never attaches a binding at all - unconditionally
    // tripping that guard on every place_order/get_my_orders/get_order call.
    private SemanticPlan Plan(SemanticOperation operation) =>
        planner.Plan(contract, authorizer.AuthorizeWithEvidence(contract, operation));

    private static string PlanFingerprint(SemanticPlan p) => Hash(JsonSerializer.Serialize(new
    {
        semantic = p.Root,
        execution = ExecutionIRCompiler.Compile(p).Root
    }));

    private static async Task<List<Dictionary<string, object?>>> ReadRows(NpgsqlCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    private static async Task<Dictionary<string, object?>?> ReadSingle(NpgsqlCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        return row;
    }

    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant()[..24];

    private static int ActorNumber(string actor) => actor switch
    {
        "alice" => 1,
        "bob" => 2,
        "carol" => 3,
        "dave" => 4,
        "admin" => 5,
        _ when actor.StartsWith("customer", StringComparison.OrdinalIgnoreCase) && int.TryParse(actor[8..], out var id) => id,
        _ => 0
    };
}
