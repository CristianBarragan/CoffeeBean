using System.Linq.Expressions;
using Foundgine.Aot;
using Foundgine.Generated;
using Foundgine.Metadata;
using Foundgine.Abstractions;
using Xunit;

namespace Foundgine.Aot.Tests;

[FoundgineEntity(Id = 1, StorageName = "customers")]
public sealed class Customer
{
    [FoundgineField(Id = 1, StorageName = "id", IsPrimaryKey = true)]
    public int Id { get; init; }

    [FoundgineField(Id = 2, StorageName = "name")]
    public string Name { get; init; } = string.Empty;

    [FoundgineRelationship(typeof(Account), "CustomerId", "Id", Id = 1, Name = "Accounts")]
    public IReadOnlyList<Account> Accounts { get; init; } = [];
}

[FoundgineEntity(Id = 2, StorageName = "accounts")]
public sealed class Account
{
    [FoundgineField(Id = 1, StorageName = "id")]
    public int Id { get; init; }

    [FoundgineField(Id = 2, StorageName = "customer_id")]
    public int CustomerId { get; init; }

    [FoundgineRelationship(typeof(Customer), "CustomerId", "Id", Id = 2, Name = "Customer")]
    public Customer Customer { get; init; } = null!;
}

public enum ProductType
{
    CreditCard,
    Mortgage,
    PersonalLoan
}

public enum ContractType
{
    CreditCard,
    Mortgage,
    PersonalLoan
}

public static class ProductConversions
{
    [FoundgineConversion(typeof(ProductType), typeof(ContractType))]
    public static ContractType ToContractType(ProductType value) => value switch
    {
        ProductType.CreditCard => ContractType.CreditCard,
        ProductType.Mortgage => ContractType.Mortgage,
        ProductType.PersonalLoan => ContractType.PersonalLoan,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

[FoundgineModel(Id = 10)]
public sealed class Product
{
    public int Id { get; init; }
    public ProductType ProductType { get; init; }

    [FoundgineConnection(typeof(Contract), Id = 10, Name = "Contract")]
    public static Expression<Func<Product, object>> ContractProjection =>
        product => new
        {
            product.Id,
            ContractType = ProductConversions.ToContractType(product.ProductType)
        };
}

public sealed class UserContext
{
    public int TenantId { get; init; }
}

public static class ProductAuthorization
{
    [FoundgineAuthorization(10, Id = 10, Name = "CanVisitContract")]
    public static Expression<Func<UserContext, Contract, bool>> CanVisitContract =>
        (user, contract) => user.TenantId == contract.TenantId;
}

[FoundgineEntity(Id = 3, StorageName = "contracts")]
public sealed class Contract
{
    [FoundgineField(Id = 1, StorageName = "id", IsPrimaryKey = true)]
    public int Id { get; init; }

    [FoundgineField(Id = 2, StorageName = "contract_type")]
    public ContractType ContractType { get; init; }

    [FoundgineField(Id = 3, StorageName = "tenant_id")]
    public int TenantId { get; init; }
}

public sealed class GeneratedMetadataTests
{
    [Fact]
    public void Generator_emits_entities_and_storage_mappings()
    {
        var customer = GeneratedMetadata.Registry.GetEntity(new EntityId(1));
        Assert.Equal("Customer", customer.Name);
        Assert.Equal("customers", customer.EffectiveStorageName);
        Assert.Equal("id", customer.Columns.Single(x => x.Id == new ColumnId(1)).EffectiveStorageName);
        Assert.Equal("name", customer.Columns.Single(x => x.Id == new ColumnId(2)).EffectiveStorageName);
        Assert.Equal(new ColumnId(1), customer.EffectiveFields.Single(x => x.Id == new FieldId(1)).Column!.ColumnId);
        Assert.Equal(new ColumnId(1), customer.PrimaryKey!.ColumnId);
    }

    [Fact]
    public void Generator_emits_relationship_join_mapping()
    {
        var relationship = GeneratedMetadata.Registry.GetRelationship(new RelationshipId(1));
        Assert.Equal(new EntityId(1), relationship.Source);
        Assert.Equal(new EntityId(2), relationship.Target);
        Assert.Equal(new EntityId(1), relationship.SourceKey.EntityId);
        Assert.Equal(new ColumnId(1), relationship.SourceKey.ColumnId);
        Assert.Equal(new EntityId(2), relationship.TargetKey.EntityId);
        Assert.Equal(new ColumnId(2), relationship.TargetKey.ColumnId);
    }

    [Fact]
    public void Generator_emits_correct_keys_when_source_owns_foreign_key()
    {
        var relationship = GeneratedMetadata.Registry.GetRelationship(new RelationshipId(2));
        Assert.Equal(new EntityId(2), relationship.Source);
        Assert.Equal(new EntityId(1), relationship.Target);
        Assert.Equal(new EntityId(2), relationship.SourceKey.EntityId);
        Assert.Equal(new ColumnId(2), relationship.SourceKey.ColumnId);
        Assert.Equal(new EntityId(1), relationship.TargetKey.EntityId);
        Assert.Equal(new ColumnId(1), relationship.TargetKey.ColumnId);
    }

    [Fact]
    public void Generated_provider_implements_runtime_contract()
    {
        IMetadataProvider provider = new GeneratedMetadataProvider();
        Assert.Equal("Account", provider.GetEntity(new EntityId(2)).Name);
        Assert.Equal("Product", provider.GetModel(new ModelId(10)).Name);
        Assert.Equal(new EntityId(3), provider.GetConnection(new ConnectionId(10)).Target);
    }
    [Fact]
    public void Generator_emits_model_connections_without_materialization_contracts()
    {
        var model = GeneratedMetadata.Registry.Get(new ModelId(10));
        Assert.Equal("Product", model.Name);

        var connection = GeneratedMetadata.Registry.Get(new ConnectionId(10));
        Assert.Equal(new ModelId(10), connection.Source);
        Assert.Equal(new EntityId(3), connection.Target);
        Assert.Equal("Contract", connection.Name);
        Assert.Equal(nameof(Product.ContractProjection), connection.SourceMember);
    }


    [Fact]
    public void Connection_uses_convention_fields_and_aot_enum_conversion()
    {
        var connection = GeneratedMetadata.Registry.GetConnection(new ConnectionId(10));

        Assert.NotNull(connection.Fields);
        Assert.Contains(connection.Fields!, x =>
            x.SourceMember == nameof(Product.Id) &&
            x.TargetMember == nameof(Contract.Id) &&
            x.Converter is null);

        var enumField = Assert.Single(connection.Fields!, x =>
            x.SourceMember == nameof(Product.ProductType) &&
            x.TargetMember == nameof(Contract.ContractType));

        Assert.Equal(typeof(ProductType), enumField.SourceType);
        Assert.Equal(typeof(ContractType), enumField.TargetType);
        Assert.Contains(nameof(ProductConversions.ToContractType), enumField.Converter);
    }

    [Fact]
    public void Connection_expression_is_anonymous_projection_not_entity_construction()
    {
        var connection = GeneratedMetadata.Registry.GetConnection(new ConnectionId(10));

        Assert.Equal(nameof(Product.ContractProjection), connection.SourceMember);
        Assert.Equal(2, connection.Fields!.Count);
        Assert.Contains(connection.Fields!, x =>
            x.SourceMember == nameof(Product.Id) &&
            x.TargetMember == nameof(Contract.Id) &&
            x.Converter is null);
        Assert.Contains(connection.Fields!, x =>
            x.SourceMember == nameof(Product.ProductType) &&
            x.TargetMember == nameof(Contract.ContractType) &&
            x.Converter is not null);
    }

    [Fact]
    public void Authorization_expression_is_emitted_as_aot_metadata()
    {
        var authorization = GeneratedMetadata.Registry.GetAuthorization(new AuthorizationId(10));

        Assert.Equal(new ConnectionId(10), authorization.ConnectionId);
        Assert.Equal(typeof(UserContext), authorization.ContextType);
        Assert.Equal(typeof(Contract), authorization.ResourceType);
        Assert.Equal(nameof(ProductAuthorization.CanVisitContract), authorization.SourceMember);
        Assert.Contains("user.TenantId == contract.TenantId", authorization.Expression);
        Assert.NotNull(authorization.Predicate);
        Assert.Equal(AuthorizationPredicateKind.Equal, authorization.Predicate!.Kind);
        Assert.Equal(AuthorizationPredicateKind.MemberAccess, authorization.Predicate.Left!.Kind);
        Assert.Equal("TenantId", authorization.Predicate.Left.Name);
        Assert.Equal(AuthorizationPredicateKind.Parameter, authorization.Predicate.Left.Left!.Kind);
        Assert.Equal("user", authorization.Predicate.Left.Left.Name);
        Assert.Equal(AuthorizationPredicateKind.MemberAccess, authorization.Predicate.Right!.Kind);
        Assert.Equal("TenantId", authorization.Predicate.Right.Name);
        Assert.Equal("contract", authorization.Predicate.Right.Left!.Name);
    }

    [Fact]
    public void Conversion_is_metadata_not_runtime_mapping()
    {
        var conversion = GeneratedMetadata.Registry.FindConversion(
            typeof(ProductType),
            typeof(ContractType));

        Assert.NotNull(conversion);
        Assert.Contains(nameof(ProductConversions.ToContractType), conversion!.Method);
    }

}
