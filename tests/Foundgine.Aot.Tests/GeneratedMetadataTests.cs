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
    }
}
