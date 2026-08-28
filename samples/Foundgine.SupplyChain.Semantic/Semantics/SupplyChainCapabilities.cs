using Foundgine.Abstractions;
using Foundgine.Semantics.Capabilities;
using Foundgine.Semantics.Mapping;

namespace Foundgine.SupplyChain.Semantic.Semantics;

/// <summary>
/// Declarative, provider-neutral capability metadata for this sample's two
/// scenario operations, expressed with the same Step 5/6 capability-definition
/// API (<see cref="SemanticCapabilityMapping"/> → <see cref="SemanticCapabilityDefinition"/>
/// with <see cref="SemanticCapabilityAuthorizationRequirement"/> metadata) used
/// by the canonical <c>Foundgine.SupplyChain</c> sample's
/// <c>SupplyChainCapabilities</c>.
///
/// This is metadata only - it documents what
/// <see cref="Scenarios.SupplyChainScenarios"/>'s existing
/// <see cref="Scenarios.AuthorizationContext"/> already establishes
/// (<c>TenantId</c>, <c>AllowedWarehouses</c>, <c>CanReadSupplierRisk</c>,
/// <c>CanWritePurchasing</c>) as first-class requirement metadata, without
/// replacing or re-implementing that runtime check.
/// </summary>
public static class SupplyChainCapabilities
{
    public const string Schema = "SupplyChain.Semantic";
    private const string ImplementationType = "Foundgine.SupplyChain.Semantic.Scenarios.SupplyChainScenarios";

    private static readonly SemanticCapabilityMapping ReadSupplierRiskMapping = new(
        Id: "read_supplier_risk",
        Schema: Schema,
        TargetEntityId: SupplyChainSemanticModel.Supplier,
        ImplementationType: ImplementationType,
        MethodName: "RecursiveSupplierRisk",
        Operation: SemanticCapabilityOperations.Read,
        Description: "Recursively walk a product's BOM/supplier graph for risk exposure, scoped to the caller's tenant.");

    private static readonly SemanticCapabilityMapping WritePurchasingMapping = new(
        Id: "write_purchasing",
        Schema: Schema,
        TargetEntityId: SupplyChainSemanticModel.PurchaseOrder,
        ImplementationType: ImplementationType,
        MethodName: "FulfillmentPlanning",
        Operation: SemanticCapabilityOperations.Write,
        Description: "Produce/act on fulfillment-planning recommendations, scoped to the caller's tenant and allowed warehouses.");

    /// <summary>
    /// Requirement set mirroring <c>AuthorizationContext</c>: every scenario
    /// operation requires establishing the caller's tenant; each also requires
    /// its corresponding <c>CanReadSupplierRisk</c>/<c>CanWritePurchasing</c>
    /// policy flag, and (since warehouse scoping matters for both scenarios)
    /// a resource requirement naming the entity the caller must be scoped to.
    /// </summary>
    private static IReadOnlyList<SemanticCapabilityAuthorizationRequirement> RequirementsFor(string id) =>
    [
        new SemanticCapabilityTenantRequirement("tenantId"),
        new SemanticCapabilityResourceRequirement("Warehouse"),
        new SemanticCapabilityPolicyRequirement(id switch
        {
            "read_supplier_risk" => "SupplyChain.Semantic.CanReadSupplierRisk",
            "write_purchasing" => "SupplyChain.Semantic.CanWritePurchasing",
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown SupplyChain.Semantic capability id.")
        })
    ];

    public static readonly IReadOnlyList<SemanticCapabilityDefinition> Definitions =
    [
        ReadSupplierRiskMapping.ToDefinition(AuthorizationDecision.Allowed, authorizationRequirements: RequirementsFor(ReadSupplierRiskMapping.Id)),
        WritePurchasingMapping.ToDefinition(AuthorizationDecision.Allowed, authorizationRequirements: RequirementsFor(WritePurchasingMapping.Id)),
    ];

    public static SemanticCapabilityRegistry Registry { get; } =
        new SemanticCapabilityRegistry().RegisterRange(Definitions.Select(d => d.Capability));
}
