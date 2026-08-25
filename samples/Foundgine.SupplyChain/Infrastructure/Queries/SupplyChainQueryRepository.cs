using Foundgine.Abstractions;
using Foundgine.Semantics.IR;
using Foundgine.Semantics.Query;
using Foundgine.SupplyChain.Application;
using Foundgine.SupplyChain.Semantics;
using Foundgine.Generated;

namespace Foundgine.SupplyChain.Infrastructure.Queries;

public sealed class SupplyChainQueryRepository : ISupplyChainQueries
{
    private readonly SemanticSqlQueryExecutor _sql;
    public SupplyChainQueryRepository(SemanticSqlQueryExecutor sql) => _sql = sql;

    public async Task<object> GetOrders(int customerId, CancellationToken ct)
    {
        var operation = Read(SupplyChainSemanticFields.SalesOrder.Entity, SupplyChainSemanticFields.SalesOrder.All,
            SupplyChainSemanticFields.SalesOrder.CustomerId.Eq(customerId),
            [SupplyChainSemanticFields.SalesOrder.Id.Asc()]);
        var result = await _sql.ExecuteAsync(operation, ct);
        return new { customerId, orders = result.Rows.Select(x => x.Values).ToArray(), plan = result.Fingerprint };
    }

    public async Task<object> GetOrder(int customerId, int orderId, CancellationToken ct)
    {
        var line = new SemanticReadNode(2, SupplyChainSemanticFields.SalesOrderLine.Entity,
            SupplyChainSemanticFields.SalesOrderLine.All,
            SupplyChainSemanticModel.OrderLines, null, []);
        var filter = new SemanticAndFilter([
            SupplyChainSemanticFields.SalesOrder.Id.Eq(orderId),
            SupplyChainSemanticFields.SalesOrder.CustomerId.Eq(customerId)]);
        var operation = new SemanticOperation(new SemanticReadNode(1, SupplyChainSemanticFields.SalesOrder.Entity,
            SupplyChainSemanticFields.SalesOrder.All, null, null, [line],
            new SemanticQueryOptions(filter)));
        var result = await _sql.ExecuteAsync(operation, ct);
        var row = result.Rows.FirstOrDefault() ?? throw new KeyNotFoundException("Sales order not found.");
        return new { order = row.Values, lines = result.Rows.Select(x => x.Values).ToArray(), plan = result.Fingerprint };
    }

    public async Task<object> GetShipment(int customerId, int shipmentId, CancellationToken ct)
    {
        var filter = new SemanticAndFilter([
            SupplyChainSemanticFields.Shipment.Id.Eq(shipmentId),
            new SemanticRelationshipFilter(SupplyChainSemanticModel.ShipmentOrder, SemanticRelationshipQuantifier.Some,
                SupplyChainSemanticFields.SalesOrder.CustomerId.Eq(customerId))]);
        var result = await _sql.ExecuteAsync(Read(SupplyChainSemanticFields.Shipment.Entity, SupplyChainSemanticFields.Shipment.All, filter), ct);
        var row = result.Rows.FirstOrDefault() ?? throw new KeyNotFoundException("Shipment not found.");
        return new { shipment = row.Values, plan = result.Fingerprint };
    }

    public Task<object> ListProducts(CancellationToken ct) => Query(SupplyChainSemanticFields.CatalogProduct.Entity, SupplyChainSemanticFields.CatalogProduct.All, "products", ct);
    public Task<object> ListCustomers(CancellationToken ct) => Query(SupplyChainSemanticFields.Customer.Entity, SupplyChainSemanticFields.Customer.All, "customers", ct);
    public Task<object> ListSuppliers(CancellationToken ct) => Query(SupplyChainSemanticFields.Supplier.Entity, SupplyChainSemanticFields.Supplier.All, "suppliers", ct);
    public Task<object> GetProduct(int productId, CancellationToken ct) => ExecuteNamed(Read(SupplyChainSemanticFields.CatalogProduct.Entity, SupplyChainSemanticFields.CatalogProduct.All, SupplyChainSemanticFields.CatalogProduct.Id.Eq(productId)), "product", ct);
    public Task<object> GetInventory(int productId, CancellationToken ct) => ExecuteNamed(Read(SupplyChainSemanticFields.InventoryPosition.Entity, SupplyChainSemanticFields.InventoryPosition.All, SupplyChainSemanticFields.InventoryPosition.ProductId.Eq(productId), [SupplyChainSemanticFields.InventoryPosition.WarehouseId.Asc()]), "inventory", ct, productId);

    private static SemanticOperation Read(EntityId entity, IReadOnlyList<FieldId> fields, SemanticFilterExpression? filter = null, IReadOnlyList<SemanticOrderTerm>? order = null) =>
        new(new SemanticReadNode(1, entity, fields, null, null, [], new SemanticQueryOptions(filter, order ?? [])));

    private async Task<object> Query(EntityId entity, IReadOnlyList<FieldId> fields, string name, CancellationToken ct)
    {
        var result = await _sql.ExecuteAsync(Read(entity, fields), ct);
        return new Dictionary<string, object?> { [name] = result.Rows.Select(x => x.Values).ToArray(), ["plan"] = result.Fingerprint };
    }

    private async Task<object> ExecuteNamed(SemanticOperation operation, string name, CancellationToken ct, object? extra = null)
    {
        var result = await _sql.ExecuteAsync(operation, ct);
        var dict = new Dictionary<string, object?> { [name] = result.Rows.FirstOrDefault()?.Values, ["plan"] = result.Fingerprint };
        if (extra is not null) dict["productId"] = extra;
        return dict;
    }
}
