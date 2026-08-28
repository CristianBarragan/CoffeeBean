using Foundgine.Abstractions;
using Foundgine.Semantics.Capabilities;
using Foundgine.Semantics.Mapping;

namespace Foundgine.SupplyChain.Semantics;

/// <summary>
/// Declarative, provider-neutral capability metadata for the SupplyChain sample,
/// expressed with the Step 5/6 capability-definition API
/// (<see cref="SemanticCapabilityMapping"/> → <see cref="SemanticCapabilityDefinition"/>
/// with <see cref="SemanticCapabilityAuthorizationRequirement"/> metadata).
///
/// This is metadata only. It documents, for every capability the sample exposes,
/// which requirement categories execution-time authorization must satisfy - it
/// does not perform authorization itself and does not replace the
/// SupplyChainAuthorizer in the Application project, which remains the only thing
/// that actually permits or denies a call.
/// </summary>

public static class SupplyChainCapabilities
{
    public const string Schema = "SupplyChain";
    private const string ImplementationType = "Foundgine.SupplyChain.Application.SupplyChainApplication";

    // Capabilities whose access must be scoped to the caller's own customerId
    // unless the caller is "admin" - mirrors SupplyChainAuthorizer.CustomerScopedCapabilities.
    private static readonly SemanticCapabilityTenantRequirement CustomerScope = new("customerId");

    private static SemanticCapabilityMapping Map(string id, string methodName, string operation, string description) =>
        new(
            Id: id,
            Schema: Schema,
            TargetEntityId: TargetEntity(id),
            ImplementationType: ImplementationType,
            MethodName: methodName,
            Operation: operation,
            Description: description);

    private static EntityId TargetEntity(string capabilityId) => capabilityId switch
    {
        "get_my_orders" or "place_order" or "cancel_order" => SupplyChainSemanticModel.SalesOrder,
        "get_order" => SupplyChainSemanticModel.SalesOrder,
        "get_shipment" or "create_shipment" or "update_shipment" => SupplyChainSemanticModel.Shipment,
        "list_products" or "get_product" => SupplyChainSemanticModel.CatalogProduct,
        "list_customers" => SupplyChainSemanticModel.Customer,
        "get_inventory" or "update_inventory" => SupplyChainSemanticModel.InventoryPosition,
        "list_suppliers" => SupplyChainSemanticModel.Supplier,
        _ => throw new ArgumentOutOfRangeException(nameof(capabilityId), capabilityId, "Unknown SupplyChain capability id.")
    };

    // Method names are plain strings (not nameof(Application.SupplyChainApplication.X)):
    // Foundgine.SupplyChain.Application already references this Semantics project
    // (for entity/relationship ids), so a reference back from Semantics to
    // Application would be circular. The mapping's ImplementationType/MethodName
    // are descriptive metadata only - see SemanticCapabilityMapping's doc comment -
    // so a string is the correct shape here, not a compile-time dependency.
    private static readonly IReadOnlyList<SemanticCapabilityMapping> Mappings =
    [
        Map("get_my_orders", "GetMyOrders", SemanticCapabilityOperations.Read, "List the caller's own sales orders."),
        Map("get_order", "GetOrder", SemanticCapabilityOperations.Read, "Read a single sales order."),
        Map("get_shipment", "GetShipment", SemanticCapabilityOperations.Read, "Read a single shipment."),
        Map("list_products", "ListProducts", SemanticCapabilityOperations.Read, "List catalog products."),
        Map("list_customers", "ListCustomers", SemanticCapabilityOperations.Read, "List customers (cross-customer)."),
        Map("get_product", "GetProduct", SemanticCapabilityOperations.Read, "Read a single catalog product."),
        Map("get_inventory", "GetInventory", SemanticCapabilityOperations.Read, "Read inventory position for a product."),
        Map("list_suppliers", "ListSuppliers", SemanticCapabilityOperations.Read, "List suppliers."),
        Map("update_inventory", "UpdateInventory", SemanticCapabilityOperations.Update, "Adjust an inventory position."),
        Map("create_shipment", "CreateShipment", SemanticCapabilityOperations.Create, "Create a shipment for an order."),
        Map("update_shipment", "UpdateShipment", SemanticCapabilityOperations.Update, "Update a shipment's status."),
        Map("place_order", "PlaceOrder", SemanticCapabilityOperations.Create, "Place a new sales order for the caller's own customer."),
        Map("cancel_order", "CancelOrder", SemanticCapabilityOperations.Update, "Cancel the caller's own sales order."),
    ];

    // Capability ids that are scoped to the caller's own customerId (mirrors
    // SupplyChainAuthorizer.CustomerScopedCapabilities exactly, so the two
    // declarations can be cross-checked in tests instead of only living in
    // one hand-typed set).
    public static readonly IReadOnlySet<string> CustomerScopedCapabilityIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "get_my_orders", "get_order", "get_shipment", "place_order", "cancel_order"
    };

    private static IReadOnlyList<SemanticCapabilityAuthorizationRequirement> RequirementsFor(string id)
    {
        List<SemanticCapabilityAuthorizationRequirement> requirements =
        [
            new SemanticCapabilityPolicyRequirement($"SupplyChain.{id}")
        ];

        if (CustomerScopedCapabilityIds.Contains(id))
            requirements.Add(CustomerScope);

        return requirements;
    }

    /// <summary>The authoritative capability definitions for the SupplyChain sample.</summary>
    public static readonly IReadOnlyList<SemanticCapabilityDefinition> Definitions =
        Mappings
            .Select(mapping => mapping.ToDefinition(
                AuthorizationDecision.Allowed,
                authorizationRequirements: RequirementsFor(mapping.Id)))
            .ToArray();

    /// <summary>
    /// Single authoritative registry of SupplyChain capability definitions,
    /// shared by every host (canonical API, PenTest GraphQL/MCP hosts) so
    /// they describe the same capability surface instead of each re-deriving
    /// their own list.
    /// </summary>
    public static SemanticCapabilityRegistry Registry { get; } =
        new SemanticCapabilityRegistry().RegisterRange(Definitions.Select(d => d.Capability));
}
