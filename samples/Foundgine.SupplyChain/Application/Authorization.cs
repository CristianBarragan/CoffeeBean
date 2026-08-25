namespace Foundgine.SupplyChain.Application;

public interface ICapabilityAuthorizer
{
    void Demand(string actor, string capability, int? customerId = null);
}

public sealed class SupplyChainAuthorizer : ICapabilityAuthorizer
{
    public void Demand(string actor, string capability, int? customerId = null)
    {
        var allowed = actor switch
        {
            "alice" => new[]
                { "get_my_orders", "get_order", "get_product", "get_shipment", "place_order", "cancel_order" },
            "bob" => new[]
            {
                "get_my_orders", "get_order", "get_product", "get_shipment", "place_order", "cancel_order",
                "list_customers"
            },
            "carol" => new[]
                { "get_product", "get_inventory", "update_inventory", "create_shipment", "update_shipment" },
            "dave" => new[]
                { "get_product", "get_inventory", "list_products", "list_suppliers", "update_inventory" },
            "admin" => new[]
            {
                "get_my_orders", "get_order", "get_product", "get_shipment", "place_order", "cancel_order",
                "list_customers", "get_inventory", "update_inventory", "create_shipment", "update_shipment",
                "list_products", "list_suppliers"
            },
            _ => actor.StartsWith("customer", StringComparison.OrdinalIgnoreCase)
                ? new[]
                {
                    "get_my_orders", "get_order", "get_product", "get_shipment", "place_order", "cancel_order"
                }
                : Array.Empty<string>()
        };
        if (!allowed.Contains(capability, StringComparer.Ordinal))
            throw new UnauthorizedAccessException($"Actor '{actor}' is not authorized for capability '{capability}'.");
        if (customerId is not null && actor.Equals("alice", StringComparison.OrdinalIgnoreCase) && customerId != 1)
            throw new UnauthorizedAccessException("Actor is not authorized for the requested customer.");
        if (customerId is not null && actor.StartsWith("customer", StringComparison.OrdinalIgnoreCase) &&
            ActorCustomerId(actor) != customerId)
            throw new UnauthorizedAccessException("Actor is not authorized for the requested customer.");
    }

    public static int ActorCustomerId(string actor) => actor.Equals("alice", StringComparison.OrdinalIgnoreCase)
        ? 1
        : (actor.StartsWith("customer", StringComparison.OrdinalIgnoreCase) && int.TryParse(actor[8..], out var id)
            ? id
            : 0);
}