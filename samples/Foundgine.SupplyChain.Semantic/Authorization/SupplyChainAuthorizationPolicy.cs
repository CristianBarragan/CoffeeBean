using Foundgine.Abstractions;
using Foundgine.Semantics.Authorization;
using Foundgine.SupplyChain.Semantic.Domain;
using Foundgine.SupplyChain.Semantic.Semantics;

namespace Foundgine.SupplyChain.Semantic.Authorization;

public enum StoreChainRole
{
    Customer,
    Analyst,
    WarehouseOperator,
    SupplyChainManager
}

/// <summary>
/// Reference policy used by the StoreChain semantic sample. It intentionally
/// demonstrates every authorization boundary exposed by Foundgine:
/// entity, field, relationship, conditional predicate, write, and named
/// operation refinement. The policy is constructed for a specific actor
/// context, so execution never trusts an earlier capability snapshot.
/// </summary>
public sealed class StoreChainAuthorizationPolicy : AllowAllSemanticAuthorizationPolicy
{
    /// <summary>
    /// Named operations that require explicit, format-validated evidence
    /// claims in addition to role. The role check still applies unchanged;
    /// the claims are an additional requirement layered on top, never a
    /// substitute for it. See <see cref="ClientClaimsValidator"/> for how
    /// "reason" and "change_ticket" are validated before they ever reach
    /// this policy.
    /// </summary>
    private static readonly IReadOnlyCollection<string> OperationsRequiringEvidence = ["inventory.reconcile"];

    public StoreChainAuthorizationPolicy(string tenantId, StoreChainRole role)
        : this(tenantId, role, ClaimsValidationResult.Empty)
    {
    }

    /// <summary>
    /// Constructs the policy with an already-validated claim set. Callers
    /// must pass the <see cref="ClaimsValidationResult.Accepted"/> claims
    /// only — never the raw, untrusted claims the MCP caller sent. Run
    /// <see cref="ClientClaimsValidator.Validate"/> first.
    /// </summary>
    public StoreChainAuthorizationPolicy(string tenantId, StoreChainRole role, ClaimsValidationResult validatedClaims)
    {
        TenantId = tenantId;
        Role = role;
        Claims = validatedClaims.Accepted;
    }

    public string TenantId { get; }
    public StoreChainRole Role { get; }

    /// <summary>Accepted (post-validation) claims only. Never the raw caller input.</summary>
    public IReadOnlyDictionary<string, string> Claims { get; }

    /// <summary>
    /// A caller can voluntarily narrow its own write access with
    /// <c>scope=read-only</c>. This can only ever remove privilege the role
    /// would otherwise have — it can never grant privilege the role lacks,
    /// because every write check below still ANDs the role-based result.
    /// </summary>
    private bool SelfRestrictedToReadOnly =>
        Claims.TryGetValue("scope", out var scope) && scope == "read-only";

    /// <summary>
    /// A caller can narrow reads/writes to a single warehouse with
    /// <c>warehouse=&lt;id&gt;</c>. Combined via AND with the existing
    /// tenant predicate, so it can only shrink the result set, never widen
    /// it beyond the tenant boundary the role already carries.
    /// </summary>
    private string? WarehouseScope =>
        Claims.TryGetValue("warehouse", out var warehouse) ? warehouse : null;

    public override bool CanAccessEntity(EntityId entityId) => entityId switch
    {
        var id when id == SupplyChainSemanticModel.Product => true,
        var id when id == SupplyChainSemanticModel.Supplier => Role != StoreChainRole.Customer,
        var id when id == SupplyChainSemanticModel.Certification => Role != StoreChainRole.Customer,
        var id when id == SupplyChainSemanticModel.ComplianceIncident => Role is StoreChainRole.Analyst or StoreChainRole.SupplyChainManager,
        var id when id == SupplyChainSemanticModel.Warehouse => Role != StoreChainRole.Customer,
        var id when id == SupplyChainSemanticModel.InventoryLot => Role != StoreChainRole.Customer,
        _ => Role != StoreChainRole.Customer
    };

    public override bool CanAccessField(EntityId entityId, FieldId fieldId)
    {
        // Field-level policy: inventory quarantine is restricted to operational roles.
        if (entityId == SupplyChainSemanticModel.InventoryLot &&
            fieldId == FieldIds.InventoryQuarantined)
            return Role is StoreChainRole.WarehouseOperator or StoreChainRole.SupplyChainManager;

        // Sensitive supplier risk is hidden from customers and warehouse operators.
        if (entityId == SupplyChainSemanticModel.Supplier &&
            fieldId == FieldIds.SupplierRiskScore)
            return Role is StoreChainRole.Analyst or StoreChainRole.SupplyChainManager;

        return true;
    }

    public override bool CanAccessRelationship(EntityId sourceEntityId, RelationshipId relationshipId) =>
        relationshipId switch
        {
            var id when id == RelationshipIds.SupplierCertifications => Role != StoreChainRole.Customer,
            var id when id == RelationshipIds.SupplierIncidents => Role is StoreChainRole.Analyst or StoreChainRole.SupplyChainManager,
            var id when id == RelationshipIds.WarehouseInventory => Role != StoreChainRole.Customer,
            _ => true
        };

    public override bool CanWriteEntity(EntityId entityId) =>
        !SelfRestrictedToReadOnly &&
        Role is StoreChainRole.WarehouseOperator or StoreChainRole.SupplyChainManager;

    public override bool CanWriteField(EntityId entityId, FieldId fieldId)
    {
        if (SelfRestrictedToReadOnly)
            return false;

        if (entityId == SupplyChainSemanticModel.InventoryLot)
            return fieldId == FieldIds.InventoryOnHand || fieldId == FieldIds.InventoryReserved;

        return Role == StoreChainRole.SupplyChainManager;
    }

    public override bool CanWriteRelationship(EntityId sourceEntityId, RelationshipId relationshipId) =>
        !SelfRestrictedToReadOnly && Role == StoreChainRole.SupplyChainManager;

    public override AuthorizationPredicate? GetPredicate(EntityId entityId, AuthorizationOperation operation)
    {
        // Conditional row policy: all tenant-owned resources must remain inside
        // the current actor tenant. The predicate is semantic IR, not executable code.
        AuthorizationPredicate? predicate = null;
        if (operation == AuthorizationOperation.Read &&
            (entityId == SupplyChainSemanticModel.Supplier ||
             entityId == SupplyChainSemanticModel.Warehouse))
        {
            predicate = TenantPredicate("TenantId");
        }

        // Claim-driven narrowing: a caller that supplied a validated
        // warehouse=<id> claim gets that constraint ANDed on top of whatever
        // the role-based predicate already produced. This can only shrink
        // the result set — there is no code path where a warehouse claim
        // widens access beyond the tenant boundary above, because the two
        // predicates are always combined with AND, never OR.
        if (operation == AuthorizationOperation.Read && WarehouseScope is { } warehouseId)
        {
            var scopePredicate = entityId == SupplyChainSemanticModel.Warehouse
                ? WarehouseIdPredicate("Id", warehouseId)
                : entityId == SupplyChainSemanticModel.InventoryLot
                    ? WarehouseIdPredicate("WarehouseId", warehouseId)
                    : null;

            if (scopePredicate is not null)
                predicate = predicate is null ? scopePredicate : AuthorizationPredicate.And(predicate, scopePredicate);
        }

        return predicate;
    }

    public override AuthorizationDecision GetEntityAccess(
        EntityId entityId,
        AuthorizationOperation operation,
        AuthorizationOperationName? name)
    {
        var coarse = base.GetEntityAccess(entityId, operation);
        if (!coarse.IsAllowed || name is null)
            return coarse;

        // Named operation policy: only managers may execute the high-assurance
        // inventory reconciliation operation, even if ordinary writes are allowed.
        if (name.Value.Value.Equals("inventory.reconcile", StringComparison.OrdinalIgnoreCase))
        {
            if (Role != StoreChainRole.SupplyChainManager)
                return AuthorizationDecision.Denied;

            // Evidence-claim requirement: a high-assurance named operation is
            // gated on role AND on the caller having supplied format-valid
            // "reason" and "change_ticket" claims (already validated by
            // ClientClaimsValidator before this policy ever saw them). Missing
            // or malformed evidence denies the operation even for a manager;
            // the claim can only make this stricter, never bypass the role
            // check above.
            if (RequiresEvidence(name.Value.Value) && !HasValidEvidence)
                return AuthorizationDecision.Denied;

            return coarse;
        }

        return coarse;
    }

    private static bool RequiresEvidence(string operationName) =>
        OperationsRequiringEvidence.Contains(operationName);

    private bool HasValidEvidence =>
        Claims.ContainsKey("reason") && Claims.ContainsKey("change_ticket");

    private static AuthorizationPredicate TenantPredicate(string property) =>
        AuthorizationPredicate.Equal(
            AuthorizationPredicate.Member(
                AuthorizationPredicate.ResourceParameter("resource"), property),
            AuthorizationPredicate.Member(
                AuthorizationPredicate.ContextParameter("context"), "TenantId"));

    private static AuthorizationPredicate WarehouseIdPredicate(string property, string warehouseId) =>
        AuthorizationPredicate.Equal(
            AuthorizationPredicate.Member(
                AuthorizationPredicate.ResourceParameter("resource"), property),
            AuthorizationPredicate.Constant(warehouseId));

    public static class FieldIds
    {
        public static readonly FieldId SupplierRiskScore = new(4);
        public static readonly FieldId InventoryOnHand = new(4);
        public static readonly FieldId InventoryReserved = new(5);
        public static readonly FieldId InventoryQuarantined = new(6);
    }

    public static class RelationshipIds
    {
        public static readonly RelationshipId SupplierCertifications = new(4);
        public static readonly RelationshipId SupplierIncidents = new(5);
        public static readonly RelationshipId WarehouseInventory = new(8);
    }
}
