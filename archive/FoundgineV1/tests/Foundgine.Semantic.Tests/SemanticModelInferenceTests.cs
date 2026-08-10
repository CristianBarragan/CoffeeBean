using Foundgine.Metadata;
using Foundgine.Semantic.Inference;
using Xunit;

namespace Foundgine.Semantic.Tests;

/// <summary>
/// Pins P2 (docs/CURRENT-STATUS.md: "semantic mapping simplification"):
/// <see cref="SemanticModelInference.InferEntity"/> should reproduce
/// exactly the same structural <see cref="SemanticEntity"/> a developer
/// would otherwise have to hand-author, from the physical
/// <see cref="EntityMetadata"/>/<see cref="RelationshipMetadata"/> an
/// application already registers -- while still requiring an explicit
/// <c>configure</c> callback for anything that's business meaning, not
/// structure (here: <see cref="SearchCapability"/>).
/// </summary>
public class SemanticModelInferenceTests
{
    private static readonly EntityId CustomerId = new(1);
    private static readonly EntityId AccountId = new(2);
    private static readonly RelationshipId CustomerAccounts = new(1);

    private static readonly EntityMetadata Customer = new(
        CustomerId,
        "Customer",
        [new ColumnMetadata(new ColumnId(1), "Id"), new ColumnMetadata(new ColumnId(2), "Name")]);

    private static readonly EntityMetadata Account = new(
        AccountId,
        "Account",
        [new ColumnMetadata(new ColumnId(1), "Id"), new ColumnMetadata(new ColumnId(3), "Balance")]);

    private static readonly RelationshipMetadata[] Relationships =
    [
        new(CustomerAccounts, CustomerId, AccountId, "Accounts")
    ];

    [Fact]
    public void InferEntity_Identity_UsesConventionalIdColumn()
    {
        var model = new SemanticModelBuilder().InferEntity(Customer, Relationships).Build();

        var customer = model.Get(CustomerId);

        Assert.Equal(new FieldId(1), customer.Identity.FieldId);
        Assert.Equal("Id", customer.Identity.Name);
    }

    [Fact]
    public void InferEntity_Fields_ExcludesIdentityColumn_AndUsesDefaultType()
    {
        var model = new SemanticModelBuilder().InferEntity(Customer, Relationships).Build();

        var customer = model.Get(CustomerId);
        var field = Assert.Single(customer.Fields);

        Assert.Equal(new FieldId(2), field.Id);
        Assert.Equal("Name", field.Name);
        Assert.Equal(SemanticModelInference.DefaultFieldType, field.ClrType);
    }

    [Fact]
    public void InferEntity_Fields_HonorsExplicitTypeOverride()
    {
        var model = new SemanticModelBuilder()
            .InferEntity(Account, Relationships, fieldTypes: new Dictionary<string, Type>
            {
                ["Balance"] = typeof(decimal)
            })
            .Build();

        var balance = Assert.Single(model.Get(AccountId).Fields);
        Assert.Equal(typeof(decimal), balance.ClrType);
    }

    [Fact]
    public void InferEntity_Relationships_OnlyAttachesRelationshipsSourcedFromThisEntity()
    {
        var model = new SemanticModelBuilder()
            .InferEntity(Customer, Relationships)
            .InferEntity(Account, Relationships)
            .Build();

        var accounts = Assert.Single(model.Get(CustomerId).Relationships);
        Assert.Equal("Accounts", accounts.Name);
        Assert.Equal(AccountId, accounts.Target);
        Assert.Equal(RelationshipCardinality.Many, accounts.Cardinality);

        Assert.Empty(model.Get(AccountId).Relationships);
    }

    [Fact]
    public void InferEntity_Relationships_HonorsCardinalityOverride()
    {
        var reverse = new RelationshipMetadata[]
        {
            new(new RelationshipId(2), AccountId, CustomerId, "Owner")
        };

        var model = new SemanticModelBuilder()
            .InferEntity(Account, reverse, cardinalityOverrides: new Dictionary<string, RelationshipCardinality>
            {
                ["Owner"] = RelationshipCardinality.One
            })
            .Build();

        var owner = Assert.Single(model.Get(AccountId).Relationships);
        Assert.Equal(RelationshipCardinality.One, owner.Cardinality);
    }

    [Fact]
    public void InferEntity_Configure_AddsSearchCapability_WhichStructureCannotInfer()
    {
        var model = new SemanticModelBuilder()
            .InferEntity(Customer, Relationships, customer => customer
                .Search(new SearchCapability([new FieldId(2)], SearchStrategy.Fuzzy)))
            .Build();

        var search = model.Get(CustomerId).Search;

        Assert.NotNull(search);
        Assert.Equal(SearchStrategy.Fuzzy, search!.Strategy);
        Assert.Equal(new FieldId(2), Assert.Single(search.SearchableFields));
    }

    [Fact]
    public void InferEntity_WithNoIdColumn_ThrowsWithAnActionableMessage()
    {
        var noId = new EntityMetadata(new EntityId(9), "Weird", [new ColumnMetadata(new ColumnId(1), "Name")]);

        var ex = Assert.Throws<InvalidOperationException>(
            () => new SemanticModelBuilder().InferEntity(noId, Relationships).Build());

        Assert.Contains("Weird", ex.Message);
        Assert.Contains("Id", ex.Message);
    }

    [Fact]
    public void InferAll_InfersEveryEntity_WithSharedConfigureCallback()
    {
        var configured = new List<string>();

        var model = new SemanticModelBuilder()
            .InferAll(
                [Customer, Account],
                Relationships,
                (entity, semantic) => configured.Add(entity.Name))
            .Build();

        Assert.Equal(2, model.Entities.Count);
        Assert.Equal(["Customer", "Account"], configured);
    }
}
