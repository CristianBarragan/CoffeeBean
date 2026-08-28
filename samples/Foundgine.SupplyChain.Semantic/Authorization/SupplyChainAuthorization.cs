using Foundgine.Abstractions;
using Foundgine.Semantics.Authorization;
using Foundgine.SupplyChain.Semantic.Semantics;

namespace Foundgine.SupplyChain.Semantic.Authorization;

public enum SupplyChainRole
{
    Customer,
    Analyst,
    WarehouseOperator,
    SupplyChainManager
}

/// <summary>
/// Sample-only application configuration. The authorization mechanics live in
/// Foundgine.Semantics; this file contains only Supply Chain policy data and
/// actor-specific values.
/// </summary>
public static class SupplyChainAuthorization
{
    public static ConfiguredSemanticAuthorizationPolicy Create(
        string tenantId,
        SupplyChainRole role,
        IReadOnlyDictionary<string, string>? claims = null)
    {
        var context = new SemanticAuthorizationContext(tenantId, role.ToString(), claims);
        var config = new SemanticAuthorizationConfiguration()
            .AddEntityRule((ctx, id, operation) =>
                operation == AuthorizationOperation.Read
                    ? CanReadEntity(id, role)
                    : CanWriteEntity(id, role, ctx.SafeClaims))
            .AddFieldRule((ctx, entity, field, operation) =>
                operation == AuthorizationOperation.Read
                    ? CanReadField(entity, field, role)
                    : CanWriteField(entity, field, role, ctx.SafeClaims))
            .AddRelationshipRule((ctx, entity, relationship, operation) =>
                operation == AuthorizationOperation.Read
                    ? CanReadRelationship(entity, relationship, role)
                    : !IsReadOnly(ctx.SafeClaims) && role == SupplyChainRole.SupplyChainManager)
            .AddPredicateRule((ctx, entity, operation) => GetPredicate(ctx, entity, operation))
            .AddOperationRule((ctx, entity, operation, name) => GetNamedOperation(ctx, entity, operation, name, role));

        return new ConfiguredSemanticAuthorizationPolicy(config, context);
    }

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

    private static bool CanReadEntity(EntityId id, SupplyChainRole role) => id switch
    {
        var x when x == SupplyChainSemanticModel.Product => true,
        var x when x == SupplyChainSemanticModel.Supplier => role != SupplyChainRole.Customer,
        var x when x == SupplyChainSemanticModel.Certification => role != SupplyChainRole.Customer,
        var x when x == SupplyChainSemanticModel.ComplianceIncident => role is SupplyChainRole.Analyst or SupplyChainRole.SupplyChainManager,
        var x when x == SupplyChainSemanticModel.Warehouse => role != SupplyChainRole.Customer,
        var x when x == SupplyChainSemanticModel.InventoryLot => role != SupplyChainRole.Customer,
        _ => role != SupplyChainRole.Customer
    };

    private static bool CanReadField(EntityId entity, FieldId field, SupplyChainRole role)
    {
        if (entity == SupplyChainSemanticModel.InventoryLot && field == FieldIds.InventoryQuarantined)
            return role is SupplyChainRole.WarehouseOperator or SupplyChainRole.SupplyChainManager;
        if (entity == SupplyChainSemanticModel.Supplier && field == FieldIds.SupplierRiskScore)
            return role is SupplyChainRole.Analyst or SupplyChainRole.SupplyChainManager;
        return true;
    }

    private static bool CanReadRelationship(EntityId entity, RelationshipId relationship, SupplyChainRole role) => relationship switch
    {
        var x when x == RelationshipIds.SupplierCertifications => role != SupplyChainRole.Customer,
        var x when x == RelationshipIds.SupplierIncidents => role is SupplyChainRole.Analyst or SupplyChainRole.SupplyChainManager,
        var x when x == RelationshipIds.WarehouseInventory => role != SupplyChainRole.Customer,
        _ => true
    };

    private static bool CanWriteEntity(EntityId _, SupplyChainRole role, IReadOnlyDictionary<string, string> claims) =>
        !IsReadOnly(claims) && role is SupplyChainRole.WarehouseOperator or SupplyChainRole.SupplyChainManager;

    private static bool CanWriteField(EntityId entity, FieldId field, SupplyChainRole role, IReadOnlyDictionary<string, string> claims)
    {
        if (IsReadOnly(claims)) return false;
        if (entity == SupplyChainSemanticModel.InventoryLot)
            return field == FieldIds.InventoryOnHand || field == FieldIds.InventoryReserved;
        return role == SupplyChainRole.SupplyChainManager;
    }

    private static AuthorizationPredicate? GetPredicate(SemanticAuthorizationContext context, EntityId entity, AuthorizationOperation operation)
    {
        AuthorizationPredicate? predicate = null;
        if (operation == AuthorizationOperation.Read &&
            (entity == SupplyChainSemanticModel.Supplier || entity == SupplyChainSemanticModel.Warehouse))
            predicate = TenantPredicate("TenantId");

        if (operation == AuthorizationOperation.Read && context.SafeClaims.TryGetValue("warehouse", out var warehouse))
        {
            var scope = entity == SupplyChainSemanticModel.Warehouse
                ? WarehouseIdPredicate("Id", warehouse)
                : entity == SupplyChainSemanticModel.InventoryLot
                    ? WarehouseIdPredicate("WarehouseId", warehouse)
                    : null;
            if (scope is not null)
                predicate = predicate is null ? scope : AuthorizationPredicate.And(predicate, scope);
        }
        return predicate;
    }

    private static AuthorizationDecision? GetNamedOperation(
        SemanticAuthorizationContext context,
        EntityId _,
        AuthorizationOperation operation,
        AuthorizationOperationName? name,
        SupplyChainRole role)
    {
        if (operation != AuthorizationOperation.Write || name is null ||
            !name.Value.Value.Equals("inventory.reconcile", StringComparison.OrdinalIgnoreCase))
            return null;

        if (role != SupplyChainRole.SupplyChainManager)
            return AuthorizationDecision.Denied;

        return context.SafeClaims.ContainsKey("reason") && context.SafeClaims.ContainsKey("change_ticket")
            ? AuthorizationDecision.Allowed
            : AuthorizationDecision.Denied;
    }

    private static bool IsReadOnly(IReadOnlyDictionary<string, string> claims) =>
        claims.TryGetValue("scope", out var scope) && scope.Equals("read-only", StringComparison.OrdinalIgnoreCase);

    private static AuthorizationPredicate TenantPredicate(string property) =>
        AuthorizationPredicate.Equal(
            AuthorizationPredicate.Member(AuthorizationPredicate.ResourceParameter("resource"), property),
            AuthorizationPredicate.Member(AuthorizationPredicate.ContextParameter("context"), "TenantId"));

    private static AuthorizationPredicate WarehouseIdPredicate(string property, string warehouseId) =>
        AuthorizationPredicate.Equal(
            AuthorizationPredicate.Member(AuthorizationPredicate.ResourceParameter("resource"), property),
            AuthorizationPredicate.Constant(warehouseId));
}
